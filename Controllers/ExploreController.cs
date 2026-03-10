using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InkVault.Data;
using InkVault.Models;
using InkVault.ViewModels;

namespace InkVault.Controllers
{
    [Authorize]
    public class ExploreController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ExploreController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, string? tag, string? sortBy = "recent")
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            var query = _context.Journals
                .Where(j => j.Status == JournalStatus.Published)
                .Include(j => j.User)
                .AsQueryable();

            // Get current user's friend IDs
            var friendIds = await _context.Friends
                .Where(f => f.UserId == currentUser.Id)
                .Select(f => f.FriendUserId)
                .ToListAsync();

            // Exclude journals from blocked users (users I've blocked)
            var blockedUserIds = await _context.BlockedUsers
                .Where(b => b.BlockerId == currentUser.Id)
                .Select(b => b.BlockedId)
                .ToListAsync();

            if (blockedUserIds.Count > 0)
                query = query.Where(j => !blockedUserIds.Contains(j.UserId));

            // Filter by search query
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(j => 
                    j.Title.ToLower().Contains(searchLower) ||
                    j.Content.ToLower().Contains(searchLower) ||
                    j.Topic!.ToLower().Contains(searchLower) ||
                    (j.User!.FirstName + " " + j.User.LastName).ToLower().Contains(searchLower) ||
                    (!string.IsNullOrEmpty(j.Tags) && j.Tags.ToLower().Contains(searchLower))
                );
            }

            // Filter by specific tag
            if (!string.IsNullOrWhiteSpace(tag))
            {
                var tagLower = tag.ToLower();
                query = query.Where(j => !string.IsNullOrEmpty(j.Tags) && j.Tags.ToLower().Contains($"\"{tagLower}\""));
            }

            // Separate public journals only
            var allJournals = await query.ToListAsync();

            var publicJournals = allJournals
                .Where(j => j.PrivacyLevel == PrivacyLevel.Public)
                .ToList();

            // Sort journals
            switch (sortBy)
            {
                case "oldest":
                    publicJournals = publicJournals.OrderBy(j => j.CreatedAt).ToList();
                    break;
                case "views":
                    publicJournals = publicJournals.OrderByDescending(j => j.ViewCount).ToList();
                    break;
                case "recent":
                default:
                    publicJournals = publicJournals.OrderByDescending(j => j.CreatedAt).ToList();
                    break;
            }

            // Helper method to strip HTML tags
            string StripHtmlTags(string input)
            {
                if (string.IsNullOrEmpty(input)) return input;
                var regexRemoveHtmlTags = new System.Text.RegularExpressions.Regex("<[^>]*>");
                return regexRemoveHtmlTags.Replace(input, string.Empty).Trim();
            }

            var viewModel = new ExploreListViewModel
            {
                PublicJournals = publicJournals.Select(j => {
                    // Parse tags from JSON for display
                    List<string>? tags = null;
                    if (!string.IsNullOrEmpty(j.Tags))
                    {
                        try
                        {
                            tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(j.Tags);
                        }
                        catch
                        {
                            tags = null;
                        }
                    }

                    return new ExploreViewModel
                    {
                        JournalId = j.JournalId,
                        Title = j.Title,
                        Content = j.Content,
                        UserId = j.UserId,
                        AuthorName = $"{j.User!.FirstName} {j.User.LastName}",
                        AuthorProfilePicture = j.User.ProfilePicturePath,
                        CreatedAt = j.CreatedAt,
                        UpdatedAt = j.UpdatedAt,
                        ViewCount = j.ViewCount,
                        Topic = j.Topic,
                        Tags = tags,
                        PreviewText = !string.IsNullOrEmpty(j.Content)
                            ? (StripHtmlTags(j.Content).Length > 150 
                                ? StripHtmlTags(j.Content).Substring(0, 150) + "..." 
                                : StripHtmlTags(j.Content))
                            : !string.IsNullOrEmpty(j.Abstract)
                                ? (j.Abstract.Length > 150
                                    ? j.Abstract.Substring(0, 150) + "..."
                                    : j.Abstract)
                                : string.Empty,
                        IsFriendsOnly = false,
                        IsPublic = true,
                        IsOwn = j.UserId == currentUser.Id,
                        IsAnonymous = j.IsAnonymous,
                        DUI = j.DUI,
                        ReferencedDUI = j.ReferencedDUI
                    };
                }).ToList(),
                FriendJournals = new List<ExploreViewModel>(),
                SearchQuery = search,
                TagQuery = tag,
                SortBy = sortBy
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> View(int id, bool fromLibrary = false, bool fromBrowse = false, string? topicName = null, bool fromFeed = false)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            var journal = await _context.Journals
                .Include(j => j.User)
                .Include(j => j.Views)
                .Include(j => j.Likes)
                .Include(j => j.Comments)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(j => j.JournalId == id && j.Status == JournalStatus.Published);

            if (journal == null)
                return NotFound();

            // Check access permissions
            if (journal.PrivacyLevel == PrivacyLevel.Private)
                return Forbid();

            if (journal.PrivacyLevel == PrivacyLevel.FriendsOnly)
            {
                var isFriend = await _context.Friends
                    .AnyAsync(f => f.UserId == currentUser.Id && f.FriendUserId == journal.UserId);

                if (!isFriend && journal.UserId != currentUser.Id)
                    return Forbid();
            }

            // Track unique user view
            try
            {
                var existingView = await _context.JournalViews
                    .FirstOrDefaultAsync(v => v.JournalId == id && v.UserId == currentUser.Id);

                if (existingView == null)
                {
                    // New view from this user - create record
                    var journalView = new JournalView
                    {
                        JournalId = id,
                        UserId = currentUser.Id,
                        ViewedAt = DateTime.UtcNow
                    };

                    _context.JournalViews.Add(journalView);
                    await _context.SaveChangesAsync();
                    
                    // ONLY increment if INSERT succeeded
                    journal.ViewCount++;
                    _context.Journals.Update(journal);
                    await _context.SaveChangesAsync();
                }
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("Violation of UNIQUE KEY constraint") ?? false)
            {
                // Handle race condition where another request already inserted the view
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving view: {ex.Message}");
            }

            ViewBag.FromLibrary = fromLibrary;
            ViewBag.FromBrowse = fromBrowse;
            ViewBag.TopicName = topicName;
            ViewBag.FromFeed = fromFeed;

            return View(journal);
        }

        /// <summary>
        /// View a journal by its DUI (Document Unique Identifier)
        /// Looks up the journal by DUI and redirects to the standard View action
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ViewByDUI(string dui)
        {
            if (string.IsNullOrWhiteSpace(dui))
                return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            // Look up journal by DUI
            var journal = await _context.Journals
                .FirstOrDefaultAsync(j => j.DUI == dui && j.Status == JournalStatus.Published);

            if (journal == null)
            {
                TempData["Error"] = $"Journal with DUI '{dui}' not found.";
                return RedirectToAction("Index");
            }

            // Check access permissions before redirecting
            if (journal.PrivacyLevel == PrivacyLevel.Private)
            {
                TempData["Error"] = "This journal is private and cannot be accessed.";
                return RedirectToAction("Index");
            }

            if (journal.PrivacyLevel == PrivacyLevel.FriendsOnly)
            {
                var isFriend = await _context.Friends
                    .AnyAsync(f => f.UserId == currentUser.Id && f.FriendUserId == journal.UserId);

                if (!isFriend && journal.UserId != currentUser.Id)
                {
                    TempData["Error"] = "This journal is only visible to friends.";
                    return RedirectToAction("Index");
                }
            }

            // Redirect to the standard View action with the journal ID
            return RedirectToAction("View", new { id = journal.JournalId });
        }
    }
}
