using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using InkVault.Data;
using InkVault.Models;
using InkVault.Services;
using InkVault.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace InkVault.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotificationService _notificationService;

        public NotificationController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var preferences = await _notificationService.GetOrCreateNotificationPreferencesAsync(user.Id);

            System.Diagnostics.Debug.WriteLine($"=== LOADING PREFERENCES (GET) ===");
            System.Diagnostics.Debug.WriteLine($"User: {user.Email}");
            System.Diagnostics.Debug.WriteLine($"UserId: {user.Id}");
            System.Diagnostics.Debug.WriteLine($"Loaded preferences ID: {preferences.Id}");
            System.Diagnostics.Debug.WriteLine($"EmailOnFriendRequestReceived: {preferences.EmailOnFriendRequestReceived}");
            System.Diagnostics.Debug.WriteLine($"EmailOnFriendRequestAccepted: {preferences.EmailOnFriendRequestAccepted}");
            System.Diagnostics.Debug.WriteLine($"EmailOnFriendRequestDenied: {preferences.EmailOnFriendRequestDenied}");
            System.Diagnostics.Debug.WriteLine($"EmailOnFriendJournalPost: {preferences.EmailOnFriendJournalPost}");
            System.Diagnostics.Debug.WriteLine($"RequireOTPOnEveryLogin: {preferences.RequireOTPOnEveryLogin}");
            System.Diagnostics.Debug.WriteLine($"EmailOnSuccessfulLogin: {preferences.EmailOnSuccessfulLogin}");

            var viewModel = new NotificationSettingsViewModel
            {
                Id = preferences.Id,
                EmailOnFriendRequestReceived = preferences.EmailOnFriendRequestReceived,
                EmailOnFriendRequestAccepted = preferences.EmailOnFriendRequestAccepted,
                EmailOnFriendRequestDenied = preferences.EmailOnFriendRequestDenied,
                EmailOnFriendJournalPost = preferences.EmailOnFriendJournalPost,
                EmailOnJournalLiked = preferences.EmailOnJournalLiked,
                EmailOnJournalCommented = preferences.EmailOnJournalCommented,
                RequireOTPOnEveryLogin = preferences.RequireOTPOnEveryLogin,
                EmailOnSuccessfulLogin = preferences.EmailOnSuccessfulLogin
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSettings(NotificationSettingsViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            try
            {
                System.Diagnostics.Debug.WriteLine($"\n=== NOTIFICATION SETTINGS UPDATE ===");
                System.Diagnostics.Debug.WriteLine($"User: {user.Email}");
                System.Diagnostics.Debug.WriteLine($"Received values:");
                System.Diagnostics.Debug.WriteLine($"  EmailOnFriendRequestReceived: {model.EmailOnFriendRequestReceived}");
                System.Diagnostics.Debug.WriteLine($"  EmailOnFriendRequestAccepted: {model.EmailOnFriendRequestAccepted}");
                System.Diagnostics.Debug.WriteLine($"  EmailOnFriendRequestDenied: {model.EmailOnFriendRequestDenied}");
                System.Diagnostics.Debug.WriteLine($"  EmailOnFriendJournalPost: {model.EmailOnFriendJournalPost}");
                System.Diagnostics.Debug.WriteLine($"  RequireOTPOnEveryLogin: {model.RequireOTPOnEveryLogin}");
                System.Diagnostics.Debug.WriteLine($"  EmailOnSuccessfulLogin: {model.EmailOnSuccessfulLogin}");

                // Get the existing preferences WITHOUT AsNoTracking (important for updates!)
                var preferences = await _context.NotificationPreferences
                    .FirstOrDefaultAsync(np => np.UserId == user.Id);

                if (preferences == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Creating NEW preferences for user");
                    preferences = new NotificationPreference
                    {
                        UserId = user.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.NotificationPreferences.Add(preferences);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Updating EXISTING preferences (ID: {preferences.Id})");
                }

                // Update all properties
                preferences.EmailOnFriendRequestReceived = model.EmailOnFriendRequestReceived;
                preferences.EmailOnFriendRequestAccepted = model.EmailOnFriendRequestAccepted;
                preferences.EmailOnFriendRequestDenied = model.EmailOnFriendRequestDenied;
                preferences.EmailOnFriendJournalPost = model.EmailOnFriendJournalPost;
                preferences.EmailOnJournalLiked = model.EmailOnJournalLiked;
                preferences.EmailOnJournalCommented = model.EmailOnJournalCommented;
                preferences.RequireOTPOnEveryLogin = model.RequireOTPOnEveryLogin;
                preferences.EmailOnSuccessfulLogin = model.EmailOnSuccessfulLogin;
                preferences.UpdatedAt = DateTime.UtcNow;

                System.Diagnostics.Debug.WriteLine($"Saving to database...");
                int rowsAffected = await _context.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine($"SaveChangesAsync completed. Rows affected: {rowsAffected}");

                // Verify the save by reloading from database
                var verifyPrefs = await _context.NotificationPreferences
                    .FirstOrDefaultAsync(np => np.UserId == user.Id);

                if (verifyPrefs != null)
                {
                    System.Diagnostics.Debug.WriteLine($"? VERIFICATION SUCCESSFUL");
                    System.Diagnostics.Debug.WriteLine($"  Saved values:");
                    System.Diagnostics.Debug.WriteLine($"  EmailOnFriendRequestReceived: {verifyPrefs.EmailOnFriendRequestReceived}");
                    System.Diagnostics.Debug.WriteLine($"  EmailOnFriendRequestAccepted: {verifyPrefs.EmailOnFriendRequestAccepted}");
                    System.Diagnostics.Debug.WriteLine($"  EmailOnFriendRequestDenied: {verifyPrefs.EmailOnFriendRequestDenied}");
                    System.Diagnostics.Debug.WriteLine($"  EmailOnFriendJournalPost: {verifyPrefs.EmailOnFriendJournalPost}");
                    System.Diagnostics.Debug.WriteLine($"  RequireOTPOnEveryLogin: {verifyPrefs.RequireOTPOnEveryLogin}");
                    System.Diagnostics.Debug.WriteLine($"  EmailOnSuccessfulLogin: {verifyPrefs.EmailOnSuccessfulLogin}");
                }

                TempData["Success"] = "Notification preferences updated successfully!";
                return RedirectToAction("Settings");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? EXCEPTION: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");

                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Settings");
            }
        }

        // ── In-app notification inbox ─────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Inbox()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var notifications = await _context.AppNotifications
                .Where(n => n.RecipientId == user.Id)
                .Include(n => n.Actor)
                .OrderByDescending(n => n.CreatedAt)
                .Take(100)
                .Select(n => new NotificationItemViewModel
                {
                    Id                  = n.Id,
                    Type                = n.Type,
                    Message             = n.Message,
                    ResourceUrl         = n.ResourceUrl,
                    ActorId             = n.ActorId,
                    ActorName           = n.Actor != null ? $"{n.Actor.FirstName} {n.Actor.LastName}" : null,
                    ActorProfilePicture = n.Actor != null ? n.Actor.ProfilePicturePath : null,
                    JournalId           = n.JournalId,
                    JournalTitle        = n.JournalTitle,
                    IsRead              = n.IsRead,
                    CreatedAt           = n.CreatedAt
                })
                .ToListAsync();

            var viewModel = new NotificationInboxViewModel
            {
                AllNotifications = notifications,
                UnreadCount      = notifications.Count(n => !n.IsRead)
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var notification = await _context.AppNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.RecipientId == user.Id);

            if (notification == null) return NotFound();

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            await _context.AppNotifications
                .Where(n => n.RecipientId == user.Id && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true));

            TempData["Success"] = "All notifications marked as read.";
            return RedirectToAction("Inbox");
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { count = 0 });

            var count = await _context.AppNotifications
                .CountAsync(n => n.RecipientId == user.Id && !n.IsRead);

            return Json(new { count });
        }
    }
}