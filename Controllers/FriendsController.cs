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
    public class FriendsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotificationService _notificationService;

        public FriendsController(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager,
            NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            // Get friends
            var friends = await _context.Friends
                .Where(f => f.UserId == currentUser.Id)
                .Include(f => f.FriendUser)
                .Select(f => new FriendsViewModel
                {
                    FriendId = f.FriendId,
                    UserId = f.FriendUser!.Id,
                    FirstName = f.FriendUser.FirstName,
                    LastName = f.FriendUser.LastName,
                    ProfilePicturePath = f.FriendUser.ProfilePicturePath,
                    CreatedAt = f.CreatedAt
                })
                .ToListAsync();

            // Get pending friend requests (incoming) - ONLY show pending requests
            var pendingRequests = await _context.FriendRequests
                .Where(fr => fr.ReceiverId == currentUser.Id && fr.Status == FriendRequestStatus.Pending)
                .Include(fr => fr.Sender)
                .Select(fr => new FriendRequestViewModel
                {
                    FriendRequestId = fr.FriendRequestId,
                    SenderId = fr.Sender!.Id,
                    SenderFirstName = fr.Sender.FirstName,
                    SenderLastName = fr.Sender.LastName,
                    SenderProfilePicture = fr.Sender.ProfilePicturePath,
                    Status = (int)fr.Status,
                    CreatedAt = fr.CreatedAt
                })
                .ToListAsync();



            // Search for users
            List<UserSearchResultViewModel> searchResults = new();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();

                // Exclude users who have blocked me or whom I have blocked
                var blockedIds = await _context.BlockedUsers
                    .Where(b => b.BlockerId == currentUser.Id || b.BlockedId == currentUser.Id)
                    .Select(b => b.BlockerId == currentUser.Id ? b.BlockedId : b.BlockerId)
                    .ToListAsync();

                var users = await _userManager.Users
                    .Where(u => u.Id != currentUser.Id &&
                               !blockedIds.Contains(u.Id) &&
                               (u.FirstName.ToLower().Contains(searchLower) ||
                                u.LastName.ToLower().Contains(searchLower) ||
                                u.UserName!.ToLower().Contains(searchLower) ||
                                u.Email!.ToLower().Contains(searchLower)))
                    .ToListAsync();

                var friendsData = await _context.Friends
                    .Where(f => f.UserId == currentUser.Id)
                    .ToListAsync();

                var sentRequests = await _context.FriendRequests
                    .Where(fr => fr.SenderId == currentUser.Id && fr.Status == FriendRequestStatus.Pending)
                    .ToListAsync();

                // Get incoming requests so we can disable "Add Friend" if user has sent us a request
                var incomingRequests = await _context.FriendRequests
                    .Where(fr => fr.ReceiverId == currentUser.Id && fr.Status == FriendRequestStatus.Pending)
                    .Select(fr => fr.SenderId)
                    .ToListAsync();

                foreach (var user in users)
                {
                    string? status = null;
                    int? friendId = null;
                    int? requestId = null;

                    var friend = friendsData.FirstOrDefault(f => f.FriendUserId == user.Id);
                    if (friend != null)
                    {
                        status = "friends";
                        friendId = friend.FriendId;
                    }
                    else
                    {
                        // Check if they sent me a request (I need to accept/decline)
                        if (incomingRequests.Contains(user.Id))
                        {
                            status = "pending_received";
                            requestId = null; // Not needed for display
                        }
                        else
                        {
                            // Check if I sent them a request
                            var request = sentRequests.FirstOrDefault(r => r.ReceiverId == user.Id);
                            if (request != null)
                            {
                                status = "pending_sent";
                                requestId = request.FriendRequestId;
                            }
                            else
                            {
                                status = "not-friends";
                            }
                        }
                    }

                    searchResults.Add(new UserSearchResultViewModel
                    {
                        UserId = user.Id,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        ProfilePicturePath = user.ProfilePicturePath,
                        FriendStatus = status,
                        FriendId = friendId,
                        FriendRequestId = requestId
                    });
                }
            }

            // Get sent pending friend requests (requests we sent) - ONLY show pending requests
            var sentPendingRequests = await _context.FriendRequests
                .Where(fr => fr.SenderId == currentUser.Id && fr.Status == FriendRequestStatus.Pending)
                .Include(fr => fr.Receiver)
                .Select(fr => new FriendRequestViewModel
                {
                    FriendRequestId = fr.FriendRequestId,
                    SenderId = fr.Sender!.Id,
                    SenderFirstName = fr.Sender.FirstName,
                    SenderLastName = fr.Sender.LastName,
                    SenderProfilePicture = fr.Sender.ProfilePicturePath,
                    ReceiverId = fr.Receiver!.Id,
                    ReceiverFirstName = fr.Receiver.FirstName,
                    ReceiverLastName = fr.Receiver.LastName,
                    ReceiverProfilePicture = fr.Receiver.ProfilePicturePath,
                    Status = (int)fr.Status,
                    CreatedAt = fr.CreatedAt
                })
                .ToListAsync();

            var viewModel = new FriendsManagementViewModel
            {
                Friends = friends,
                PendingRequests = pendingRequests,
                SentPendingRequests = sentPendingRequests,
                SearchResults = searchResults,
                SearchQuery = search,
                FriendCount = friends.Count
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendRequest(string receiverId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            if (currentUser.Id == receiverId)
                return BadRequest(new { message = "Cannot send friend request to yourself" });

            // Check if already friends
            var existingFriend = await _context.Friends
                .AnyAsync(f => (f.UserId == currentUser.Id && f.FriendUserId == receiverId) ||
                              (f.UserId == receiverId && f.FriendUserId == currentUser.Id));

            if (existingFriend)
                return BadRequest(new { message = "Already friends" });

            // Check if request already exists FROM ME TO THEM
            var existingRequest = await _context.FriendRequests
                .AnyAsync(fr => fr.SenderId == currentUser.Id && 
                               fr.ReceiverId == receiverId && 
                               fr.Status == FriendRequestStatus.Pending);

            if (existingRequest)
                return BadRequest(new { message = "Friend request already sent" });

            // IMPORTANT: Check if THEY have already sent ME a pending request
            var incomingRequest = await _context.FriendRequests
                .AnyAsync(fr => fr.SenderId == receiverId && 
                               fr.ReceiverId == currentUser.Id && 
                               fr.Status == FriendRequestStatus.Pending);

            if (incomingRequest)
                return BadRequest(new { message = "You have a pending friend request from this user. Please accept or decline it first." });

            var friendRequest = new FriendRequest
            {
                SenderId = currentUser.Id,
                ReceiverId = receiverId,
                Status = FriendRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.FriendRequests.Add(friendRequest);
            await _context.SaveChangesAsync();

            // Send email notification to receiver
            await _notificationService.SendFriendRequestReceivedEmailAsync(friendRequest);
            await _notificationService.NotifyFriendRequestReceivedInAppAsync(friendRequest);

            return Ok(new { success = true, message = "Friend request sent" });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptRequest(int friendRequestId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                    return Unauthorized();

                var friendRequest = await _context.FriendRequests
                    .FirstOrDefaultAsync(fr => fr.FriendRequestId == friendRequestId && fr.ReceiverId == currentUser.Id);

                if (friendRequest == null)
                    return NotFound("Request not found");

                if (friendRequest.Status != FriendRequestStatus.Pending)
                    return BadRequest("Request is no longer pending");

                friendRequest.Status = FriendRequestStatus.Accepted;
                friendRequest.RespondedAt = DateTime.UtcNow;

                // Create bidirectional friend relationship
                var friend1 = new Friend
                {
                    UserId = currentUser.Id,
                    FriendUserId = friendRequest.SenderId,
                    CreatedAt = DateTime.UtcNow
                };

                var friend2 = new Friend
                {
                    UserId = friendRequest.SenderId,
                    FriendUserId = currentUser.Id,
                    CreatedAt = DateTime.UtcNow
                };

                _context.FriendRequests.Update(friendRequest);
                _context.Friends.Add(friend1);
                _context.Friends.Add(friend2);
                await _context.SaveChangesAsync();

                // Send email notification to the sender about acceptance
                await _notificationService.SendFriendRequestAcceptedEmailAsync(friendRequest);
                await _notificationService.NotifyFriendRequestAcceptedInAppAsync(friendRequest);

                return Ok(new { success = true, message = "Friend request accepted" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error accepting friend request: {ex.Message}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineRequest(int friendRequestId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                    return Unauthorized();

                var friendRequest = await _context.FriendRequests
                    .FirstOrDefaultAsync(fr => fr.FriendRequestId == friendRequestId && fr.ReceiverId == currentUser.Id);

                if (friendRequest == null)
                    return NotFound("Request not found");

                if (friendRequest.Status != FriendRequestStatus.Pending)
                    return BadRequest("Request is no longer pending");

                friendRequest.Status = FriendRequestStatus.Declined;
                friendRequest.RespondedAt = DateTime.UtcNow;

                _context.FriendRequests.Update(friendRequest);
                await _context.SaveChangesAsync();

                // Send email notification to the sender about decline
                await _notificationService.SendFriendRequestDeniedEmailAsync(friendRequest);
                await _notificationService.NotifyFriendRequestDeclinedInAppAsync(friendRequest);

                return Ok(new { success = true, message = "Friend request declined" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error declining friend request: {ex.Message}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFriend(int friendId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            var friend = await _context.Friends
                .FirstOrDefaultAsync(f => f.FriendId == friendId && f.UserId == currentUser.Id);

            if (friend == null)
                return NotFound(new { success = false, message = "Friend not found" });

            var removedUserId = friend.FriendUserId; // capture before removal

            // Remove bidirectional friendship
            var reverseFriend = await _context.Friends
                .FirstOrDefaultAsync(f => f.UserId == friend.FriendUserId && f.FriendUserId == currentUser.Id);

            _context.Friends.Remove(friend);
            if (reverseFriend != null)
                _context.Friends.Remove(reverseFriend);

            await _context.SaveChangesAsync();

            await _notificationService.NotifyFriendRemovedAsync(currentUser.Id, removedUserId);

            return Ok(new { success = true, message = "Friend removed" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WithdrawRequest(int friendRequestId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                    return Unauthorized();

                var friendRequest = await _context.FriendRequests
                    .FirstOrDefaultAsync(fr => fr.FriendRequestId == friendRequestId && fr.SenderId == currentUser.Id);

                if (friendRequest == null)
                    return NotFound(new { success = false, message = "Request not found" });

                if (friendRequest.Status != FriendRequestStatus.Pending)
                    return BadRequest(new { success = false, message = "Can only withdraw pending requests" });

                var receiverId = friendRequest.ReceiverId; // capture before removal

                _context.FriendRequests.Remove(friendRequest);
                await _context.SaveChangesAsync();

                await _notificationService.NotifyFriendRequestWithdrawnInAppAsync(currentUser.Id, receiverId);

                return Ok(new { success = true, message = "Friend request withdrawn" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error withdrawing friend request: {ex.Message}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string query)
        {

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(query))
                return Json(new List<object>());

            var searchLower = query.ToLower();

            // Exclude users who have blocked me or whom I have blocked
            var blockedIds = await _context.BlockedUsers
                .Where(b => b.BlockerId == currentUser.Id || b.BlockedId == currentUser.Id)
                .Select(b => b.BlockerId == currentUser.Id ? b.BlockedId : b.BlockerId)
                .ToListAsync();

            // Search by name or username
            var users = await _userManager.Users
                .Where(u => u.Id != currentUser.Id &&
                           !blockedIds.Contains(u.Id) &&
                           (u.FirstName.ToLower().Contains(searchLower) ||
                            u.LastName.ToLower().Contains(searchLower) ||
                            u.UserName!.ToLower().Contains(searchLower)))
                .Take(10)
                .ToListAsync();

            // Get friend and request status
            var friendIds = await _context.Friends
                .Where(f => f.UserId == currentUser.Id)
                .Select(f => f.FriendUserId)
                .ToListAsync();

            var sentRequestIds = await _context.FriendRequests
                .Where(fr => fr.SenderId == currentUser.Id && fr.Status == FriendRequestStatus.Pending)
                .Select(fr => fr.ReceiverId)
                .ToListAsync();

            var result = users.Select(u => new
            {
                userId = u.Id,
                firstName = u.FirstName,
                lastName = u.LastName,
                userName = u.UserName,
                profilePicturePath = u.ProfilePicturePath,
                friendStatus = friendIds.Contains(u.Id) ? "friends" : 
                              sentRequestIds.Contains(u.Id) ? "pending" : "not-friends"
            }).ToList();

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetCounts()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            // Get count of received pending requests
            var receivedCount = await _context.FriendRequests
                .CountAsync(fr => fr.ReceiverId == currentUser.Id && fr.Status == FriendRequestStatus.Pending);

            // Get count of sent pending requests
            var sentCount = await _context.FriendRequests
                .CountAsync(fr => fr.SenderId == currentUser.Id && fr.Status == FriendRequestStatus.Pending);

            return Json(new { receivedCount, sentCount });
        }
    }
}


