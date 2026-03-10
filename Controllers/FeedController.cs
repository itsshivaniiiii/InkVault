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
    public class FeedController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FeedController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            // Get all friends of current user
            var friendIds = await _context.Friends
                .Where(f => f.UserId == currentUser.Id)
                .Select(f => f.FriendUserId)
                .ToListAsync();

            // Exclude blocked users from the feed
            var blockedUserIds = await _context.BlockedUsers
                .Where(b => b.BlockerId == currentUser.Id)
                .Select(b => b.BlockedId)
                .ToListAsync();

            var visibleFriendIds = blockedUserIds.Count > 0
                ? friendIds.Where(id => !blockedUserIds.Contains(id)).ToList()
                : friendIds;

            if (!friendIds.Any())
            {
                // No friends yet
                var emptyViewModel = new FriendsFeedViewModel
                {
                    PublicJournals = new List<FriendJournalViewModel>(),
                    FriendsOnlyJournals = new List<FriendJournalViewModel>(),
                    HasFriends = false
                };
                return View(emptyViewModel);
            }

            // Get public journals from friends (excluding blocked users)
            var publicJournals = await _context.Journals
                .Where(j => j.Status == JournalStatus.Published &&
                           j.PrivacyLevel == PrivacyLevel.Public &&
                           visibleFriendIds.Contains(j.UserId))
                .Include(j => j.User)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            // Get friends-only journals from friends (excluding blocked users)
            var friendsOnlyJournals = await _context.Journals
                .Where(j => j.Status == JournalStatus.Published &&
                           j.PrivacyLevel == PrivacyLevel.FriendsOnly &&
                           visibleFriendIds.Contains(j.UserId))
                .Include(j => j.User)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            // Helper method to strip HTML tags
            string StripHtmlTags(string input)
            {
                if (string.IsNullOrEmpty(input)) return input;
                var regexRemoveHtmlTags = new System.Text.RegularExpressions.Regex("<[^>]*>");
                return regexRemoveHtmlTags.Replace(input, string.Empty).Trim();
            }

            var viewModel = new FriendsFeedViewModel
            {
                PublicJournals = publicJournals.Select(j => {
                    // Parse tags from JSON
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

                    return new FriendJournalViewModel
                    {
                        JournalId = j.JournalId,
                        Title = j.Title,
                        Content = !string.IsNullOrEmpty(j.Content)
                            ? (StripHtmlTags(j.Content).Length > 150
                                ? StripHtmlTags(j.Content).Substring(0, 150) + "..."
                                : StripHtmlTags(j.Content))
                            : !string.IsNullOrEmpty(j.Abstract)
                                ? (j.Abstract.Length > 150
                                    ? j.Abstract.Substring(0, 150) + "..."
                                    : j.Abstract)
                                : string.Empty,
                        AuthorName = $"{j.User?.FirstName} {j.User?.LastName}",
                        AuthorProfilePicture = j.User?.ProfilePicturePath,
                        CreatedAt = j.CreatedAt,
                        ViewCount = j.ViewCount,
                        Topic = j.Topic,
                        Tags = tags,
                        PrivacyLevel = j.PrivacyLevel,
                        DUI = j.DUI,
                        ReferencedDUI = j.ReferencedDUI
                    };
                }).ToList(),

                FriendsOnlyJournals = friendsOnlyJournals.Select(j => {
                    // Parse tags from JSON
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

                    return new FriendJournalViewModel
                    {
                        JournalId = j.JournalId,
                        Title = j.Title,
                        Content = !string.IsNullOrEmpty(j.Content)
                            ? (StripHtmlTags(j.Content).Length > 150
                                ? StripHtmlTags(j.Content).Substring(0, 150) + "..."
                                : StripHtmlTags(j.Content))
                            : !string.IsNullOrEmpty(j.Abstract)
                                ? (j.Abstract.Length > 150
                                    ? j.Abstract.Substring(0, 150) + "..."
                                    : j.Abstract)
                                : string.Empty,
                        AuthorName = $"{j.User?.FirstName} {j.User?.LastName}",
                        AuthorProfilePicture = j.User?.ProfilePicturePath,
                        CreatedAt = j.CreatedAt,
                        ViewCount = j.ViewCount,
                        Topic = j.Topic,
                        Tags = tags,
                        PrivacyLevel = j.PrivacyLevel,
                        DUI = j.DUI,
                        ReferencedDUI = j.ReferencedDUI
                    };
                }).ToList(),

                HasFriends = true
            };

            return View(viewModel);
        }
    }
}
