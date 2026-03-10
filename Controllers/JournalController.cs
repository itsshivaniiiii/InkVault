using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InkVault.Data;
using InkVault.Models;
using InkVault.ViewModels;
using InkVault.Services;

namespace InkVault.Controllers
{
    [Authorize]
    public class JournalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotificationService _notificationService;
        private readonly IAIEnhancementService _aiEnhancementService;

        public JournalController(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager,
            NotificationService notificationService,
            IAIEnhancementService aiEnhancementService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _aiEnhancementService = aiEnhancementService;
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateJournalViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Validate ReferencedDUI if provided
            if (!string.IsNullOrWhiteSpace(model.ReferencedDUI))
            {
                var referencedJournal = await _context.Journals
                    .FirstOrDefaultAsync(j => j.DUI == model.ReferencedDUI.Trim());

                if (referencedJournal == null)
                {
                    ModelState.AddModelError("ReferencedDUI", "The referenced DUI does not exist.");
                    return View(model);
                }

                // Check if the referenced journal is accessible (must be public or friends-only if they are friends)
                if (referencedJournal.PrivacyLevel == PrivacyLevel.Private)
                {
                    ModelState.AddModelError("ReferencedDUI", "The referenced journal is private and cannot be referenced.");
                    return View(model);
                }

                // If friends-only, check if they are friends
                if (referencedJournal.PrivacyLevel == PrivacyLevel.FriendsOnly)
                {
                    var areFriends = await _context.Friends
                        .AnyAsync(f => 
                            (f.UserId == userId && f.FriendUserId == referencedJournal.UserId) ||
                            (f.UserId == referencedJournal.UserId && f.FriendUserId == userId));

                    if (!areFriends && referencedJournal.UserId != userId)
                    {
                        ModelState.AddModelError("ReferencedDUI", "The referenced journal is friends-only and you don't have access.");
                        return View(model);
                    }
                }
            }

            // If posting anonymously, force privacy to Public
            if (model.IsAnonymous)
            {
                model.PrivacyLevel = PrivacyLevel.Public;
            }

            // Process tags - convert comma-separated string to JSON array
            string? tagsJson = null;
            if (!string.IsNullOrWhiteSpace(model.Tags))
            {
                var tags = model.Tags.Split(',')
                    .Select(t => t.Trim().ToLower())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Distinct()
                    .ToList();

                if (tags.Any())
                {
                    tagsJson = System.Text.Json.JsonSerializer.Serialize(tags);
                }
            }

            var journal = new Journal
            {
                Title = model.Title,
                Abstract = string.IsNullOrWhiteSpace(model.Abstract) ? null : model.Abstract.Trim(),
                Content = string.IsNullOrWhiteSpace(model.Content) ? null : model.Content.Trim(),
                Topic = model.Topic,
                Tags = tagsJson,
                UserId = userId,
                Status = model.Status,
                PrivacyLevel = model.PrivacyLevel,
                IsAnonymous = model.IsAnonymous,
                ReferencedDUI = string.IsNullOrWhiteSpace(model.ReferencedDUI) ? null : model.ReferencedDUI.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Journals.Add(journal);
            await _context.SaveChangesAsync();

            // Generate DUI after successful save (only for published + public journals)
            if (model.Status == JournalStatus.Published && model.PrivacyLevel == PrivacyLevel.Public)
            {
                journal.DUI = $"JRN-{journal.JournalId:D6}";
                _context.Journals.Update(journal);
                await _context.SaveChangesAsync();
            }

            // If journal is published and public, send notifications to friends
            // But skip if anonymous to not reveal the anonymous post
            if (model.Status == JournalStatus.Published && model.PrivacyLevel == PrivacyLevel.Public && !model.IsAnonymous)
            {
                var friendIds = await _context.Friends
                    .Where(f => f.UserId == userId)
                    .Select(f => f.FriendUserId)
                    .ToListAsync();

                if (friendIds.Any())
                {
                    await _notificationService.SendFriendJournalPostEmailAsync(journal, friendIds);
                    await _notificationService.NotifyJournalPublishedInAppAsync(journal, friendIds);
                }
            }

            // If journal references another, notify that journal's owner
            if (model.Status == JournalStatus.Published && !string.IsNullOrWhiteSpace(journal.ReferencedDUI))
            {
                var referencedJournal = await _context.Journals
                    .FirstOrDefaultAsync(j => j.DUI == journal.ReferencedDUI);

                if (referencedJournal != null)
                {
                    await _notificationService.NotifyJournalReferencedAsync(userId!, referencedJournal, journal);
                }
            }

            await UpdateWriteStreakAsync(userId!);

            TempData["Success"] = model.Status == JournalStatus.Draft 
                ? "Journal saved as draft!" 
                : "Journal published successfully!";

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User);
            var journal = await _context.Journals
                .FirstOrDefaultAsync(j => j.JournalId == id && j.UserId == userId);

            if (journal == null)
                return NotFound();

            // Parse tags from JSON back to comma-separated string for the form
            string? tagsString = null;
            if (!string.IsNullOrEmpty(journal.Tags))
            {
                try
                {
                    var tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(journal.Tags);
                    tagsString = string.Join(",", tags ?? new List<string>());
                }
                catch
                {
                    // If JSON parsing fails, treat as empty
                    tagsString = null;
                }
            }

            var model = new EditJournalViewModel
            {
                JournalId = journal.JournalId,
                Title = journal.Title,
                Abstract = journal.Abstract,
                Content = journal.Content,
                Topic = journal.Topic,
                Tags = tagsString,
                Status = journal.Status,
                PrivacyLevel = journal.PrivacyLevel
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditJournalViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userId = _userManager.GetUserId(User);
            var journal = await _context.Journals
                .FirstOrDefaultAsync(j => j.JournalId == model.JournalId && j.UserId == userId);

            if (journal == null)
                return NotFound();

            // Check if journal is being published for the first time
            var isFirstTimePublishing = journal.Status != JournalStatus.Published && model.Status == JournalStatus.Published;

            // Process tags - convert comma-separated string to JSON array
            string? tagsJson = null;
            if (!string.IsNullOrWhiteSpace(model.Tags))
            {
                var tags = model.Tags.Split(',')
                    .Select(t => t.Trim().ToLower())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Distinct()
                    .ToList();

                if (tags.Any())
                {
                    tagsJson = System.Text.Json.JsonSerializer.Serialize(tags);
                }
            }

            journal.Title = model.Title;
            journal.Abstract = string.IsNullOrWhiteSpace(model.Abstract) ? null : model.Abstract.Trim();
            journal.Content = string.IsNullOrWhiteSpace(model.Content) ? null : model.Content.Trim();
            journal.Topic = model.Topic;
            journal.Tags = tagsJson;
            journal.Status = model.Status;
            journal.PrivacyLevel = model.PrivacyLevel;
            journal.UpdatedAt = DateTime.UtcNow;

            // JRN/DUI Lifecycle Rules:
            // 1. Generate DUI if journal is Published + Public and doesn't have one
            // 2. Keep existing DUI if it exists (even when changing privacy)
            // 3. Never delete or regenerate existing DUI
            if (model.Status == JournalStatus.Published && 
                model.PrivacyLevel == PrivacyLevel.Public && 
                string.IsNullOrEmpty(journal.DUI))
            {
                journal.DUI = $"JRN-{journal.JournalId:D6}";
            }
            // Note: DUI is preserved in database even when journal becomes Private
            // Visibility is controlled in views by checking PrivacyLevel

            _context.Journals.Update(journal);
            await _context.SaveChangesAsync();

            // If journal is being published for the first time and is public, send notifications
            if (isFirstTimePublishing && model.PrivacyLevel == PrivacyLevel.Public)
            {
                var friendIds = await _context.Friends
                    .Where(f => f.UserId == userId)
                    .Select(f => f.FriendUserId)
                    .ToListAsync();

                if (friendIds.Any())
                {
                    await _notificationService.SendFriendJournalPostEmailAsync(journal, friendIds);
                }
            }

            await UpdateWriteStreakAsync(userId!);

            TempData["Success"] = model.Status == JournalStatus.Draft 
                ? "Journal saved as draft!" 
                : "Journal updated successfully!";

            return RedirectToAction("MyJournals");
        }

        [HttpGet]
        public async Task<IActionResult> MyJournals()
        {
            var userId = _userManager.GetUserId(User);
            var journals = await _context.Journals
                .Where(j => j.UserId == userId)
                .Include(j => j.Likes)
                .Include(j => j.Comments)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            var viewModels = journals.Select(j => {
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

                return new JournalListViewModel
                {
                    JournalId = j.JournalId,
                    Title = j.Title,
                    Abstract = j.Abstract,
                    Content = !string.IsNullOrEmpty(j.Content) 
                        ? (j.Content.Length > 100 ? j.Content.Substring(0, 100) + "..." : j.Content)
                        : j.Abstract != null 
                            ? (j.Abstract.Length > 100 ? j.Abstract.Substring(0, 100) + "..." : j.Abstract)
                            : string.Empty,
                    ReadingTimeMinutes = Math.Max(1, (int)Math.Ceiling(
                        System.Text.RegularExpressions.Regex.Replace(
                            !string.IsNullOrEmpty(j.Content) ? j.Content : j.Abstract ?? string.Empty,
                            "<[^>]+>", " ")
                        .Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                        .Length / 200.0)),
                    CreatedAt = j.CreatedAt,
                    UpdatedAt = j.UpdatedAt,
                    Status = j.Status,
                    PrivacyLevel = j.PrivacyLevel,
                    Topic = j.Topic,
                    Tags = tags,
                    ViewCount = j.ViewCount,
                    DUI = j.DUI,
                    ReferencedDUI = j.ReferencedDUI,
                    LikeCount = j.Likes.Count,
                    CommentCount = j.Comments.Count,
                    IsPinned = j.IsPinned
                };
            }).ToList();

            return View(viewModels);
        }

        [HttpGet]
        public async Task<IActionResult> Library(string? sort = "recently_added", string? topic = null)
        {
            var userId = _userManager.GetUserId(User);
            
            // Debug: Check if user has any saved journals
            var totalSavedJournals = await _context.SavedJournals
                .Where(s => s.UserId == userId)
                .CountAsync();
            
            Console.WriteLine($"User {userId} has {totalSavedJournals} total saved journals");
            
            // Get saved journals from other users
            var savedJournalsQuery = _context.SavedJournals
                .Include(s => s.Journal)
                .ThenInclude(j => j.User)
                .Where(s => s.UserId == userId && s.Journal.UserId != userId)
                .AsQueryable();

            // Apply topic filter
            if (!string.IsNullOrEmpty(topic) && topic != "all")
            {
                savedJournalsQuery = savedJournalsQuery.Where(s => s.Journal.Topic.ToLower() == topic.ToLower());
            }

            // Apply sorting
            savedJournalsQuery = sort switch
            {
                "newest_written" => savedJournalsQuery.OrderByDescending(s => s.Journal.CreatedAt),
                "oldest_written" => savedJournalsQuery.OrderBy(s => s.Journal.CreatedAt),
                "recently_added" => savedJournalsQuery.OrderByDescending(s => s.SavedAt),
                _ => savedJournalsQuery.OrderByDescending(s => s.SavedAt)
            };

            var savedJournals = await savedJournalsQuery
                .Select(s => new SavedJournalListViewModel
                {
                    SavedJournalId = s.SavedJournalId,
                    JournalId = s.Journal.JournalId,
                    Title = s.Journal.Title,
                    Content = !string.IsNullOrEmpty(s.Journal.Content)
                        ? (s.Journal.Content.Length > 150 ? s.Journal.Content.Substring(0, 150) + "..." : s.Journal.Content)
                        : s.Journal.Abstract != null
                            ? (s.Journal.Abstract.Length > 150 ? s.Journal.Abstract.Substring(0, 150) + "..." : s.Journal.Abstract)
                            : string.Empty,
                    AuthorName = $"{s.Journal.User.FirstName} {s.Journal.User.LastName}",
                    AuthorUserName = s.Journal.User.UserName,
                    CreatedAt = s.Journal.CreatedAt,
                    SavedAt = s.SavedAt,
                    Topic = s.Journal.Topic,
                    ViewCount = s.Journal.ViewCount,
                    DUI = s.Journal.DUI,
                    ReferencedDUI = s.Journal.ReferencedDUI
                })
                .ToListAsync();

            Console.WriteLine($"Filtered saved journals count: {savedJournals.Count}");

            // Get ALL available topics from ALL published journals for filter dropdown
            var availableTopics = await _context.Journals
                .Where(j => j.Status == JournalStatus.Published && !string.IsNullOrEmpty(j.Topic))
                .Select(j => j.Topic)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            ViewData["AvailableTopics"] = availableTopics;
            ViewData["CurrentSort"] = sort;
            ViewData["CurrentTopic"] = topic;

            // Reading history for the Library section (most recent 12)
            var historyRaw = await _context.JournalViews
                .Where(v => v.UserId == userId && v.Journal.Status == JournalStatus.Published)
                .Include(v => v.Journal)
                    .ThenInclude(j => j.User)
                .OrderByDescending(v => v.ViewedAt)
                .Take(12)
                .Select(v => new
                {
                    v.JournalId,
                    v.Journal.Title,
                    v.Journal.Topic,
                    v.ViewedAt,
                    v.Journal.IsAnonymous,
                    v.Journal.PrivacyLevel,
                    AuthorFirstName = v.Journal.IsAnonymous ? null : v.Journal.User!.FirstName,
                    AuthorLastName = v.Journal.IsAnonymous ? null : v.Journal.User!.LastName,
                    IsOwn = v.Journal.UserId == userId,
                    Content = v.Journal.Content ?? v.Journal.Abstract ?? string.Empty
                })
                .ToListAsync();

            var readingHistory = historyRaw.Select(r => new ReadingHistoryItemViewModel
            {
                JournalId = r.JournalId,
                Title = r.Title,
                Topic = r.Topic,
                ViewedAt = r.ViewedAt,
                IsAnonymous = r.IsAnonymous,
                PrivacyLevel = r.PrivacyLevel,
                AuthorName = r.IsOwn
                    ? "Your Journal"
                    : r.IsAnonymous
                        ? null
                        : $"{r.AuthorFirstName} {r.AuthorLastName}",
                IsOwn = r.IsOwn,
                Content = r.Content
            }).ToList();

            var historyTotal = await _context.JournalViews
                .CountAsync(v => v.UserId == userId && v.Journal.Status == JournalStatus.Published);

            ViewBag.ReadingHistory = readingHistory;
            ViewBag.TotalHistoryCount = historyTotal;

            return View(savedJournals);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            var journal = await _context.Journals
                .FirstOrDefaultAsync(j => j.JournalId == id && j.UserId == userId);

            if (journal == null)
                return NotFound();

            _context.Journals.Remove(journal);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Journal deleted successfully!";
            return RedirectToAction("MyJournals");
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> View(int id, bool fromBrowse = false, string? topicName = null, bool fromFeed = false, bool fromUserProfile = false, string? userProfileId = null, bool backToFriends = false)
        {
            var journal = await _context.Journals
                .Include(j => j.User)
                .Include(j => j.Views)
                .Include(j => j.Likes)
                .Include(j => j.Comments)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(j => j.JournalId == id);

            if (journal == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            var userName = User?.Identity?.Name;
            
            System.Diagnostics.Debug.WriteLine($"\n=== JOURNAL VIEW DEBUG ===");
            System.Diagnostics.Debug.WriteLine($"Journal ID: {id}");
            System.Diagnostics.Debug.WriteLine($"User Name: {userName ?? "ANONYMOUS"}");
            System.Diagnostics.Debug.WriteLine($"User ID: {userId ?? "NULL"}");
            System.Diagnostics.Debug.WriteLine($"Is Authenticated: {User?.Identity?.IsAuthenticated}");
            System.Diagnostics.Debug.WriteLine($"========================\n");

            var isOwner = journal.UserId == userId;

            if (journal.Status != JournalStatus.Published && !isOwner)
                return Forbid();

            if (journal.PrivacyLevel == PrivacyLevel.Private && !isOwner)
                return Forbid();

            // Check if user is still friends with the author for friends-only journals
            if (journal.PrivacyLevel == PrivacyLevel.FriendsOnly && !isOwner && !string.IsNullOrEmpty(userId))
            {
                var isFriend = await _context.Friends
                    .AnyAsync(f => f.UserId == userId && f.FriendUserId == journal.UserId);

                if (!isFriend)
                    return Forbid();
            }

            // Track unique user view ONLY if user is logged in
            if (!string.IsNullOrEmpty(userId))
            {
                try
                {
                    var existingView = await _context.JournalViews
                        .FirstOrDefaultAsync(v => v.JournalId == id && v.UserId == userId);

                    if (existingView == null)
                    {
                        // New view from this user - create record
                        var journalView = new JournalView
                        {
                            JournalId = id,
                            UserId = userId,
                            ViewedAt = DateTime.UtcNow
                        };

                        _context.JournalViews.Add(journalView);
                        await _context.SaveChangesAsync();
                        
                        // ONLY increment if INSERT succeeded
                        journal.ViewCount++;
                        _context.Journals.Update(journal);
                        await _context.SaveChangesAsync();
                        
                        System.Diagnostics.Debug.WriteLine($"? NEW VIEW RECORDED: User={userId}, Journal={id}, NewCount={journal.ViewCount}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"??  DUPLICATE VIEW PREVENTED: User={userId}, Journal={id}, FirstViewedAt={existingView.ViewedAt}");
                    }
                }
                catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("Violation of UNIQUE KEY constraint") ?? false)
                {
                    // Handle race condition where another request already inserted the view
                    System.Diagnostics.Debug.WriteLine($"??  CONCURRENT VIEW PREVENTED (Unique Constraint): User={userId}, Journal={id}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"? ERROR SAVING VIEW: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"??  ANONYMOUS VIEW: No view tracking (userId is null)");
            }

            // Pass browse/feed/user profile parameters to view
            ViewBag.FromBrowse = fromBrowse;
            ViewBag.TopicName = topicName;
            ViewBag.FromFeed = fromFeed;
            ViewBag.FromUserProfile = fromUserProfile;
            ViewBag.UserProfileId = userProfileId;
            ViewBag.BackToFriends = backToFriends;

            return View(journal);
        }

        /// <summary>
        /// API endpoint to enhance journal content with AI
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnhanceContent([FromBody] EnhanceContentRequest request)
        {
            System.Diagnostics.Debug.WriteLine("=== ENHANCE CONTENT REQUEST ===");
            System.Diagnostics.Debug.WriteLine($"Request received: {request}");
            System.Diagnostics.Debug.WriteLine($"Content: {request?.Content}");
            System.Diagnostics.Debug.WriteLine($"Content length: {request?.Content?.Length ?? 0}");
            
            try
            {
                if (string.IsNullOrEmpty(request?.Content))
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Content is empty");
                    return BadRequest(new { error = "Content is required" });
                }

                if (request.Content.Length > 10000)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: Content too long ({request.Content.Length})");
                    return BadRequest(new { error = "Content exceeds maximum length (10000 characters)" });
                }

                if (request.Content.Length < 10)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: Content too short ({request.Content.Length})");
                    return BadRequest(new { error = "Content must be at least 10 characters long" });
                }

                System.Diagnostics.Debug.WriteLine("Calling AI enhancement service...");
                var enhancedContent = await _aiEnhancementService.EnhanceContentAsync(request.Content);

                System.Diagnostics.Debug.WriteLine($"Enhancement result received: {enhancedContent}");

                if (string.IsNullOrEmpty(enhancedContent))
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Enhanced content is empty");
                    return BadRequest(new { error = "Failed to enhance content. No result returned from AI service." });
                }

                System.Diagnostics.Debug.WriteLine("Returning success response");
                return Ok(new { enhanced = enhancedContent });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EXCEPTION: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"STACK: {ex.StackTrace}");
                
                // Return proper HTTP status codes for different errors
                if (ex.Message.Contains("Rate limit exceeded"))
                {
                    return StatusCode(429, new { error = ex.Message });
                }
                
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Test endpoint to verify Gemini API is working
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> TestEnhance(string text = "I really enjoys programming")
        {
            System.Diagnostics.Debug.WriteLine("=== TEST ENHANCE ENDPOINT ===");
            System.Diagnostics.Debug.WriteLine($"Testing with: {text}");
            
            try
            {
                System.Diagnostics.Debug.WriteLine("Calling AI service...");
                var result = await _aiEnhancementService.EnhanceContentAsync(text);
                System.Diagnostics.Debug.WriteLine($"Result: {result}");
                
                return Ok(new 
                { 
                    original = text,
                    enhanced = result,
                    success = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"STACK: {ex.StackTrace}");
                return BadRequest(new 
                { 
                    error = ex.Message,
                    success = false
                });
            }
        }

        /// <summary>
        /// API endpoint to get existing tags for auto-complete functionality
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTags(string? query = null)
        {
            try
            {
                var userId = _userManager.GetUserId(User);

                // Get all tags from published journals
                var journals = await _context.Journals
                    .Where(j => j.Status == JournalStatus.Published && !string.IsNullOrEmpty(j.Tags))
                    .Select(j => new { j.Tags, j.UserId })
                    .ToListAsync();

                var allTags = new List<string>();
                var tagCounts = new Dictionary<string, int>();

                foreach (var journal in journals)
                {
                    try
                    {
                        var tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(journal.Tags);
                        if (tags != null)
                        {
                            foreach (var tag in tags)
                            {
                                var lowerTag = tag.ToLower().Trim();
                                if (!string.IsNullOrEmpty(lowerTag))
                                {
                                    allTags.Add(lowerTag);
                                    tagCounts[lowerTag] = tagCounts.ContainsKey(lowerTag) ? tagCounts[lowerTag] + 1 : 1;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip invalid JSON
                    }
                }

                // Get unique tags and sort by popularity (frequency)
                var uniqueTags = tagCounts
                    .OrderByDescending(tc => tc.Value)
                    .ThenBy(tc => tc.Key)
                    .Select(tc => tc.Key)
                    .ToList();

                // Filter by query if provided
                if (!string.IsNullOrWhiteSpace(query))
                {
                    var queryLower = query.ToLower();
                    uniqueTags = uniqueTags
                        .Where(tag => tag.Contains(queryLower))
                        .Take(10) // Limit to top 10 suggestions
                        .ToList();
                }
                else
                {
                    // Return top 20 most popular tags if no query
                    uniqueTags = uniqueTags.Take(20).ToList();
                }

                return Ok(new { tags = uniqueTags });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting tags: {ex.Message}");
                return Ok(new { tags = new List<string>() });
            }
        }

        /// <summary>
        /// Navigate to a journal by its DUI
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ViewByDUI(string dui)
        {
            if (string.IsNullOrWhiteSpace(dui))
                return NotFound();

            var journal = await _context.Journals
                .FirstOrDefaultAsync(j => j.DUI == dui.Trim());

            if (journal == null)
            {
                TempData["Error"] = $"Journal with DUI '{dui}' not found.";
                return RedirectToAction("Index", "Explore");
            }

            // Redirect to the regular View action
            return RedirectToAction("View", new { id = journal.JournalId });
        }

        /// <summary>
        /// Validate if a DUI exists and is accessible (AJAX endpoint)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ValidateDUI([FromBody] ValidateDUIRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.DUI))
            {
                return Json(new { valid = true, message = "DUI is optional" });
            }

            var dui = request.DUI.Trim();

            // Check format
            if (!System.Text.RegularExpressions.Regex.IsMatch(dui, @"^JRN-\d{6}$"))
            {
                return Json(new { valid = false, message = "Invalid DUI format. Expected format: JRN-000123" });
            }

            var journal = await _context.Journals
                .FirstOrDefaultAsync(j => j.DUI == dui);

            if (journal == null)
            {
                return Json(new { valid = false, message = "No journal found with this DUI" });
            }

            var userId = _userManager.GetUserId(User);

            // Check accessibility
            if (journal.PrivacyLevel == PrivacyLevel.Private && journal.UserId != userId)
            {
                return Json(new { valid = false, message = "The referenced journal is private and cannot be referenced" });
            }

            if (journal.PrivacyLevel == PrivacyLevel.FriendsOnly && journal.UserId != userId)
            {
                var areFriends = await _context.Friends
                    .AnyAsync(f => 
                        (f.UserId == userId && f.FriendUserId == journal.UserId) ||
                        (f.UserId == journal.UserId && f.FriendUserId == userId));

                if (!areFriends)
                {
                    return Json(new { valid = false, message = "The referenced journal is friends-only and you don't have access" });
                }
            }

            return Json(new { valid = true, message = "Valid DUI", title = journal.Title });
        }

        /// <summary>
        /// Request access to view full text of a journal that only has an abstract
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestFullText(int journalId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var journal = await _context.Journals
                .Include(j => j.User)
                .FirstOrDefaultAsync(j => j.JournalId == journalId);

            if (journal == null)
                return NotFound();

            // Validate: Journal must have abstract but no content
            if (string.IsNullOrWhiteSpace(journal.Abstract))
            {
                return BadRequest("This journal does not have an abstract-only format.");
            }

            if (!string.IsNullOrWhiteSpace(journal.Content))
            {
                return BadRequest("Full content is already visible.");
            }

            // Check if user already requested
            var existingRequest = await _context.FullTextRequests
                .FirstOrDefaultAsync(r => r.JournalId == journalId && r.RequesterId == userId);

            if (existingRequest != null)
            {
                TempData["Info"] = "You have already requested access to this journal.";
                return RedirectToAction("View", new { id = journalId });
            }

            // Create the request record
            var request = new FullTextRequest
            {
                JournalId = journalId,
                RequesterId = userId,
                RequestedAt = DateTime.UtcNow
            };

            _context.FullTextRequests.Add(request);
            await _context.SaveChangesAsync();

            // Send email to journal owner
            var requester = await _userManager.GetUserAsync(User);
            if (requester != null && journal.User != null)
            {
                await _notificationService.SendFullTextRequestEmailAsync(journal, requester);
                await _notificationService.NotifyFullTextRequestedInAppAsync(journal, requester);
            }

            TempData["Success"] = "Your request has been sent to the journal author.";
            return RedirectToAction("View", new { id = journalId });
        }

        /// <summary>
        /// Check if current user has requested full text for a journal (AJAX endpoint)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> HasRequestedFullText(int journalId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Json(new { hasRequested = false });

            var hasRequested = await _context.FullTextRequests
                .AnyAsync(r => r.JournalId == journalId && r.RequesterId == userId);

            return Json(new { hasRequested });
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var historyRaw = await _context.JournalViews
                .Where(v => v.UserId == userId && v.Journal.Status == JournalStatus.Published)
                .Include(v => v.Journal)
                    .ThenInclude(j => j.User)
                .OrderByDescending(v => v.ViewedAt)
                .Select(v => new
                {
                    v.JournalId,
                    v.Journal.Title,
                    v.Journal.Topic,
                    v.ViewedAt,
                    v.Journal.IsAnonymous,
                    v.Journal.PrivacyLevel,
                    AuthorFirstName = v.Journal.IsAnonymous ? null : v.Journal.User!.FirstName,
                    AuthorLastName = v.Journal.IsAnonymous ? null : v.Journal.User!.LastName,
                    AuthorUserId = v.Journal.IsAnonymous ? null : v.Journal.UserId,
                    IsOwn = v.Journal.UserId == userId,
                    Content = v.Journal.Content ?? v.Journal.Abstract ?? string.Empty
                })
                .ToListAsync();

            var history = historyRaw.Select(r => new ReadingHistoryItemViewModel
            {
                JournalId = r.JournalId,
                Title = r.Title,
                Topic = r.Topic,
                ViewedAt = r.ViewedAt,
                IsAnonymous = r.IsAnonymous,
                PrivacyLevel = r.PrivacyLevel,
                AuthorName = r.IsOwn
                    ? "Your Journal"
                    : r.IsAnonymous
                        ? null
                        : $"{r.AuthorFirstName} {r.AuthorLastName}",
                AuthorUserId = r.IsOwn || r.IsAnonymous ? null : r.AuthorUserId,
                IsOwn = r.IsOwn,
                Content = r.Content
            }).ToList();

            return View(history);
        }

        [HttpGet]
        public async Task<IActionResult> Stats(string sortBy = "views")
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var statsRaw = await _context.Journals
                .Where(j => j.UserId == userId)
                .Select(j => new
                {
                    j.JournalId,
                    j.Title,
                    j.Topic,
                    j.CreatedAt,
                    j.PrivacyLevel,
                    j.Status,
                    j.ViewCount,
                    j.DUI,
                    LikeCount = j.Likes.Count,
                    CommentCount = j.Comments.Count,
                    Content = j.Content ?? j.Abstract ?? string.Empty
                })
                .ToListAsync();

            var statViewModels = statsRaw.Select(s => new JournalStatItemViewModel
            {
                JournalId = s.JournalId,
                Title = s.Title,
                Topic = s.Topic,
                CreatedAt = s.CreatedAt,
                PrivacyLevel = s.PrivacyLevel,
                Status = s.Status,
                ViewCount = s.ViewCount,
                DUI = s.DUI,
                LikeCount = s.LikeCount,
                CommentCount = s.CommentCount,
                Content = s.Content
            }).ToList();

            statViewModels = sortBy switch
            {
                "likes" => statViewModels.OrderByDescending(s => s.LikeCount).ToList(),
                "comments" => statViewModels.OrderByDescending(s => s.CommentCount).ToList(),
                "date" => statViewModels.OrderByDescending(s => s.CreatedAt).ToList(),
                _ => statViewModels.OrderByDescending(s => s.ViewCount).ToList()
            };

            ViewBag.TotalViews = statViewModels.Sum(s => s.ViewCount);
            ViewBag.TotalLikes = statViewModels.Sum(s => s.LikeCount);
            ViewBag.TotalComments = statViewModels.Sum(s => s.CommentCount);
            ViewBag.TotalJournals = statViewModels.Count;
            ViewBag.PublishedCount = statViewModels.Count(s => s.Status == JournalStatus.Published);
            ViewBag.DraftCount = statViewModels.Count(s => s.Status == JournalStatus.Draft);
            ViewBag.SortBy = sortBy;

            return View(statViewModels);
        }

        private async Task UpdateWriteStreakAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return;

            var todayUtc = DateTime.UtcNow.Date;

            if (user.LastWriteDate.HasValue)
            {
                var lastDate = user.LastWriteDate.Value.Date;

                if (lastDate == todayUtc)
                    return; // Already wrote today

                user.CurrentStreak = lastDate == todayUtc.AddDays(-1)
                    ? user.CurrentStreak + 1  // Consecutive day
                    : 1;                       // Missed day(s) — reset
            }
            else
            {
                user.CurrentStreak = 1;
            }

            user.LastWriteDate = todayUtc;

            if (user.CurrentStreak > user.LongestStreak)
                user.LongestStreak = user.CurrentStreak;

            await _userManager.UpdateAsync(user);
        }
    }

    /// <summary>
    /// Request model for DUI validation
    /// </summary>
    public class ValidateDUIRequest
    {
        public string? DUI { get; set; }
    }

    /// <summary>
    /// Request model for AI enhancement
    /// </summary>
    public class EnhanceContentRequest
    {
        public string? Content { get; set; }
    }
}


