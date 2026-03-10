using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InkVault.Models;
using InkVault.ViewModels;
using InkVault.Data;

namespace InkVault.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ApplicationDbContext _context;

        public ProfileController(UserManager<ApplicationUser> userManager, IWebHostEnvironment webHostEnvironment, ApplicationDbContext context)
        {
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var model = new ProfileViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Username = user.UserName,
                UserId = user.Id,
                PhoneNumber = user.PhoneNumber,
                Gender = user.Gender,
                DateOfBirth = user.DateOfBirth,
                ProfilePictureUrl = user.ProfilePicturePath,
                Bio = user.Bio,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                ThemePreference = user.ThemePreference
            };

            var pinnedRaw = await _context.Journals
                .Where(j => j.UserId == user.Id && j.IsPinned && j.Status == JournalStatus.Published)
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => new { j.JournalId, j.Title, j.Content, j.Abstract, j.Topic, j.Tags, j.CreatedAt, j.ViewCount, j.PrivacyLevel, j.IsAnonymous, j.DUI, j.ReferencedDUI })
                .ToListAsync();

            model.PinnedJournals = pinnedRaw.Select(j =>
            {
                var rawText = !string.IsNullOrEmpty(j.Content) ? j.Content : j.Abstract ?? string.Empty;
                var stripped = System.Text.RegularExpressions.Regex.Replace(rawText, "<[^>]+>", " ").Trim();
                var preview = stripped.Length > 150 ? stripped.Substring(0, 150) + "..." : stripped;
                List<string>? tags = null;
                if (!string.IsNullOrEmpty(j.Tags)) { try { tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(j.Tags); } catch { } }
                return new UserJournalViewModel
                {
                    JournalId = j.JournalId,
                    Title = j.Title,
                    Content = preview,
                    Topic = j.Topic,
                    Tags = tags ?? new List<string>(),
                    CreatedAt = j.CreatedAt,
                    ViewCount = j.ViewCount,
                    PrivacyLevel = j.PrivacyLevel,
                    IsAnonymous = j.IsAnonymous,
                    DUI = j.DUI,
                    ReferencedDUI = j.ReferencedDUI
                };
            }).ToList();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ProfileViewModel model, IFormFile? profilePicture)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(model?.FirstName) || string.IsNullOrWhiteSpace(model?.LastName))
            {
                ModelState.AddModelError(string.Empty, "First name and last name are required.");
                model = new ProfileViewModel
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Username = user.UserName,
                    UserId = user.Id,
                    PhoneNumber = user.PhoneNumber,
                    Gender = user.Gender,
                    DateOfBirth = user.DateOfBirth,
                    ProfilePictureUrl = user.ProfilePicturePath,
                    Bio = user.Bio,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt,
                    ThemePreference = user.ThemePreference
                };
                return View("Index", model);
            }

            // Validate bio length
            if (!string.IsNullOrEmpty(model.Bio) && model.Bio.Length > 200)
            {
                ModelState.AddModelError(nameof(model.Bio), "Bio must not exceed 200 characters.");
                return View("Index", model);
            }

            try
            {
                user.FirstName = model.FirstName?.Trim() ?? user.FirstName;
                user.LastName = model.LastName?.Trim() ?? user.LastName;
                user.PhoneNumber = model.PhoneNumber?.Trim();
                user.DateOfBirth = model.DateOfBirth;
                user.Bio = model.Bio?.Trim(); // Support emojis
                
                // Handle Gender - convert empty string to "Not Specified"
                if (string.IsNullOrWhiteSpace(model.Gender))
                {
                    user.Gender = "Not Specified";
                }
                else
                {
                    user.Gender = model.Gender.Trim();
                }

                if (profilePicture != null && profilePicture.Length > 0)
                {
                    try
                    {
                        var uploads = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "profiles");
                        Directory.CreateDirectory(uploads);

                        var fileName = $"{user.Id}_{Guid.NewGuid()}{Path.GetExtension(profilePicture.FileName)}";
                        var filePath = Path.Combine(uploads, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await profilePicture.CopyToAsync(stream);
                        }

                        user.ProfilePicturePath = $"/uploads/profiles/{fileName}";
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError(string.Empty, "Error uploading profile picture. Please try again.");
                        return View("Index", model);
                    }
                }

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    TempData["Success"] = "Profile updated successfully!";
                    return RedirectToAction("Index");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View("Index", model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"An error occurred while updating your profile: {ex.Message}");
                return View("Index", model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveProfilePicture()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            if (!string.IsNullOrEmpty(user.ProfilePicturePath))
            {
                try
                {
                    var filePath = Path.Combine(_webHostEnvironment.WebRootPath, user.ProfilePicturePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
                catch
                {
                    // Ignore file deletion errors
                }

                user.ProfilePicturePath = null;
                await _userManager.UpdateAsync(user);
            }

            TempData["Success"] = "Profile picture removed successfully!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeTheme(string theme)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return BadRequest(new { error = "User not found" });

            if (theme != "dark" && theme != "light")
                return BadRequest(new { error = "Invalid theme" });

            user.ThemePreference = theme;
            var result = await _userManager.UpdateAsync(user);
            
            if (result.Succeeded)
            {
                return Ok(new { success = true, theme = theme });
            }

            return BadRequest(new { error = "Failed to save theme" });
        }

        [HttpGet]
        public async Task<IActionResult> GetTheme()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return BadRequest();

            var theme = user.ThemePreference ?? "dark";
            return Json(new { theme = theme });
        }

        /// <summary>
        /// View another user's public profile (Instagram-style)
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ViewPublic(string userId, bool fromFriends = false)
        {
            if (string.IsNullOrEmpty(userId))
                return NotFound();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);

            var model = new ProfileViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Username = user.UserName,
                UserId = user.Id,
                ProfilePictureUrl = user.ProfilePicturePath,
                Bio = user.Bio,
                DateOfBirth = user.DateOfBirth,
                CreatedAt = user.CreatedAt
            };

            // Check if they're friends (simplified check)
            if (currentUser != null && currentUser.Id != userId)
            {
                // Check block status
                bool isBlockedByMe = await _context.BlockedUsers
                    .AnyAsync(b => b.BlockerId == currentUser.Id && b.BlockedId == userId);
                bool hasBlockedMe = await _context.BlockedUsers
                    .AnyAsync(b => b.BlockerId == userId && b.BlockedId == currentUser.Id);

                if (hasBlockedMe)
                    return NotFound();

                model.IsBlockedByMe = isBlockedByMe;

                var areFriends = await _context.Friends
                    .Where(f => (f.UserId == currentUser.Id && f.FriendUserId == userId) ||
                                (f.UserId == userId && f.FriendUserId == currentUser.Id))
                    .AnyAsync();

                model.AreFriends = areFriends;

                // Get accessible journal count (public + friends-only if friends)
                var accessibleJournalCount = await _context.Journals
                    .Where(j => j.UserId == userId && j.Status == Models.JournalStatus.Published &&
                               (j.PrivacyLevel == Models.PrivacyLevel.Public ||
                                (j.PrivacyLevel == Models.PrivacyLevel.FriendsOnly && areFriends)))
                    .CountAsync();

                model.PublicJournalCount = accessibleJournalCount;
            }
            else if (currentUser != null && currentUser.Id == userId)
            {
                // Own profile - show all published journals
                var ownJournalCount = await _context.Journals
                    .Where(j => j.UserId == userId && j.Status == Models.JournalStatus.Published)
                    .CountAsync();

                model.PublicJournalCount = ownJournalCount;
            }
            else
            {
                // Get public journal count only for non-friends/anonymous users
                var publicJournalCount = await _context.Journals
                    .Where(j => j.UserId == userId && j.Status == Models.JournalStatus.Published && j.PrivacyLevel == Models.PrivacyLevel.Public)
                    .CountAsync();

                model.PublicJournalCount = publicJournalCount;
            }

            // Load pinned journals with privacy filtering
            bool viewerIsOwner = currentUser?.Id == userId;
            var pinnedQuery = _context.Journals
                .Where(j => j.UserId == userId && j.IsPinned && j.Status == JournalStatus.Published);

            if (!viewerIsOwner)
            {
                if (model.AreFriends)
                    pinnedQuery = pinnedQuery.Where(j => j.PrivacyLevel == Models.PrivacyLevel.Public || j.PrivacyLevel == Models.PrivacyLevel.FriendsOnly);
                else
                    pinnedQuery = pinnedQuery.Where(j => j.PrivacyLevel == Models.PrivacyLevel.Public);
            }

            var pinnedPubRaw = await pinnedQuery
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => new { j.JournalId, j.Title, j.Content, j.Abstract, j.Topic, j.Tags, j.CreatedAt, j.ViewCount, j.PrivacyLevel, j.IsAnonymous, j.DUI, j.ReferencedDUI })
                .ToListAsync();

            model.PinnedJournals = pinnedPubRaw.Select(j =>
            {
                var rawText = !string.IsNullOrEmpty(j.Content) ? j.Content : j.Abstract ?? string.Empty;
                var stripped = System.Text.RegularExpressions.Regex.Replace(rawText, "<[^>]+>", " ").Trim();
                var preview = stripped.Length > 150 ? stripped.Substring(0, 150) + "..." : stripped;
                List<string>? tags = null;
                if (!string.IsNullOrEmpty(j.Tags)) { try { tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(j.Tags); } catch { } }
                return new UserJournalViewModel
                {
                    JournalId = j.JournalId,
                    Title = j.Title,
                    Content = preview,
                    Topic = j.Topic,
                    Tags = tags ?? new List<string>(),
                    CreatedAt = j.CreatedAt,
                    ViewCount = j.ViewCount,
                    PrivacyLevel = j.PrivacyLevel,
                    IsAnonymous = j.IsAnonymous,
                    DUI = j.DUI,
                    ReferencedDUI = j.ReferencedDUI
                };
            }).ToList();

            // Pass navigation context
            ViewBag.FromFriends = fromFriends;

            return View(model);
        }

        /// <summary>
        /// View a specific user's public journals
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ViewUserJournals(string userId, bool fromFriends = false)
        {
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "Invalid user ID provided.";
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Index", "Home");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserId = currentUser?.Id;

            // Block check: if the journal owner has blocked us, deny access
            if (currentUser != null && currentUser.Id != userId)
            {
                bool hasBlockedMe = await _context.BlockedUsers
                    .AnyAsync(b => b.BlockerId == userId && b.BlockedId == currentUser.Id);
                if (hasBlockedMe)
                    return NotFound();
            }

            // Check if they're friends (needed for privacy filtering)
            bool areFriends = false;
            if (currentUser != null && currentUser.Id != userId)
            {
                areFriends = await _context.Friends
                    .Where(f => (f.UserId == currentUserId && f.FriendUserId == userId) ||
                                (f.UserId == userId && f.FriendUserId == currentUserId))
                    .AnyAsync();
            }

            // Build query for journals based on privacy rules
            var journalsQuery = _context.Journals
                .Where(j => j.UserId == userId && j.Status == JournalStatus.Published)
                .Include(j => j.User)
                .AsQueryable();

            // Apply privacy filtering
            if (currentUserId == userId)
            {
                // Own journals - show all published journals
                journalsQuery = journalsQuery.Where(j => j.Status == JournalStatus.Published);
            }
            else
            {
                // Other user's journals - apply privacy rules
                journalsQuery = journalsQuery.Where(j => 
                    j.PrivacyLevel == PrivacyLevel.Public || 
                    (j.PrivacyLevel == PrivacyLevel.FriendsOnly && areFriends));
            }

            // Helper method to strip HTML tags
            string StripHtmlTags(string input)
            {
                if (string.IsNullOrEmpty(input)) return input;
                var regexRemoveHtmlTags = new System.Text.RegularExpressions.Regex("<[^>]*>");
                return regexRemoveHtmlTags.Replace(input, string.Empty).Trim();
            }

            var journalsData = await journalsQuery
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => new {
                    j.JournalId,
                    j.Title,
                    j.Content,
                    j.Abstract,
                    j.Topic,
                    j.Tags,
                    j.CreatedAt,
                    j.UpdatedAt,
                    j.ViewCount,
                    j.PrivacyLevel,
                    j.IsAnonymous,
                    j.DUI,
                    j.ReferencedDUI
                })
                .ToListAsync();

            var journals = journalsData.Select(j => new UserJournalViewModel
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
                Topic = j.Topic,
                Tags = string.IsNullOrEmpty(j.Tags) ? new List<string>() 
                    : System.Text.Json.JsonSerializer.Deserialize<List<string>>(j.Tags) ?? new List<string>(),
                CreatedAt = j.CreatedAt,
                UpdatedAt = j.UpdatedAt,
                ViewCount = j.ViewCount,
                PrivacyLevel = j.PrivacyLevel,
                IsAnonymous = j.IsAnonymous,
                DUI = j.DUI,
                ReferencedDUI = j.ReferencedDUI
            }).ToList();

            var viewModel = new UserJournalsViewModel
            {
                UserId = userId,
                UserFirstName = user.FirstName,
                UserLastName = user.LastName,
                UserProfilePicture = user.ProfilePicturePath,
                IsOwner = currentUserId == userId,
                AreFriends = areFriends,
                Journals = journals,
                JournalCount = journals.Count
            };

            // Pass navigation context
            ViewBag.FromFriends = fromFriends;

            return View(viewModel);
        }

        /// <summary>
        /// Block a user: removes friendship, pending requests, and creates block record.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BlockUser(string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "Invalid user.";
                return RedirectToAction("Index", "Home");
            }

            if (currentUser.Id == userId)
            {
                TempData["Error"] = "You cannot block yourself.";
                return RedirectToAction("ViewPublic", new { userId });
            }

            try
            {
                var existingBlock = await _context.BlockedUsers
                    .AnyAsync(b => b.BlockerId == currentUser.Id && b.BlockedId == userId);

                if (!existingBlock)
                {
                    // Remove friendship in both directions if it exists
                    var friends = await _context.Friends
                        .Where(f => (f.UserId == currentUser.Id && f.FriendUserId == userId) ||
                                    (f.UserId == userId && f.FriendUserId == currentUser.Id))
                        .ToListAsync();
                    _context.Friends.RemoveRange(friends);

                    // Remove any friend requests (pending or accepted) between the two users
                    var requests = await _context.FriendRequests
                        .Where(fr => (fr.SenderId == currentUser.Id && fr.ReceiverId == userId) ||
                                     (fr.SenderId == userId && fr.ReceiverId == currentUser.Id))
                        .ToListAsync();
                    _context.FriendRequests.RemoveRange(requests);

                    _context.BlockedUsers.Add(new BlockedUser
                    {
                        BlockerId = currentUser.Id,
                        BlockedId = userId,
                        CreatedAt = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "User has been blocked.";
            }
            catch (Exception)
            {
                TempData["Error"] = "An error occurred while blocking the user. Please try again.";
            }

            return RedirectToAction("ViewPublic", new { userId });
        }

        /// <summary>
        /// Unblock a previously blocked user.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnblockUser(string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "Invalid user.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                var block = await _context.BlockedUsers
                    .FirstOrDefaultAsync(b => b.BlockerId == currentUser.Id && b.BlockedId == userId);

                if (block != null)
                {
                    _context.BlockedUsers.Remove(block);
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "User has been unblocked.";
            }
            catch (Exception)
            {
                TempData["Error"] = "An error occurred while unblocking the user. Please try again.";
            }

            return RedirectToAction("ViewPublic", new { userId });
        }

        /// <summary>
        /// View and manage the current user's blocked users list.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> BlockedUsers()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            var blockedUsers = await _context.BlockedUsers
                .Where(b => b.BlockerId == currentUser.Id)
                .Include(b => b.Blocked)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new BlockedUserViewModel
                {
                    BlockedUserId = b.BlockedId,
                    FirstName = b.Blocked!.FirstName,
                    LastName = b.Blocked.LastName,
                    ProfilePicturePath = b.Blocked.ProfilePicturePath,
                    BlockedAt = b.CreatedAt
                })
                .ToListAsync();

            return View(blockedUsers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return RedirectToAction("Index");

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                TempData["Success"] = "Password changed successfully!";
                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Home");
            }

            TempData["Error"] = "Failed to delete account";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePin(int journalId)
        {
            var userId = _userManager.GetUserId(User);
            var journal = await _context.Journals
                .FirstOrDefaultAsync(j => j.JournalId == journalId && j.UserId == userId);

            if (journal == null)
                return NotFound(new { message = "Journal not found" });

            if (!journal.IsPinned)
            {
                var pinnedCount = await _context.Journals
                    .CountAsync(j => j.UserId == userId && j.IsPinned);
                if (pinnedCount >= 3)
                    return BadRequest(new { message = "You can pin at most 3 journals to your profile." });
            }

            journal.IsPinned = !journal.IsPinned;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                isPinned = journal.IsPinned,
                message = journal.IsPinned ? "Journal pinned to your profile!" : "Journal unpinned from your profile."
            });
        }
    }
}

