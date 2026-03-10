using InkVault.Data;
using InkVault.Models;
using Microsoft.EntityFrameworkCore;

namespace InkVault.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ApplicationDbContext context, IEmailService emailService, ILogger<NotificationService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        /// <summary>
        /// Get or create notification preferences for a user
        /// </summary>
        public async Task<NotificationPreference> GetOrCreateNotificationPreferencesAsync(string userId)
        {
            var preferences = await _context.NotificationPreferences
                .FirstOrDefaultAsync(np => np.UserId == userId);

            if (preferences == null)
            {
                preferences = new NotificationPreference
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.NotificationPreferences.Add(preferences);
                await _context.SaveChangesAsync();
            }

            return preferences;
        }

        /// <summary>
        /// Send registration success email
        /// </summary>
        public async Task SendRegistrationSuccessEmailAsync(ApplicationUser user)
        {
            try
            {
                var subject = "Welcome to InkVault - Account Created Successfully";
                var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #333; background: #f5f5f5; }}
        .wrapper {{ background: #f5f5f5; padding: 40px 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; border-radius: 12px; box-shadow: 0 10px 40px rgba(102, 126, 234, 0.2); overflow: hidden; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 50px 30px; text-align: center; color: white; }}
        .header h1 {{ font-size: 32px; font-weight: 700; margin-bottom: 10px; }}
        .header p {{ font-size: 16px; opacity: 0.95; }}
        .content {{ padding: 40px 30px; }}
        .greeting {{ font-size: 18px; font-weight: 600; color: #333; margin-bottom: 20px; }}
        .text {{ color: #555; margin-bottom: 20px; line-height: 1.8; }}
        .info-box {{ background: #f8f9ff; border-left: 4px solid #667eea; padding: 20px; border-radius: 6px; margin: 25px 0; }}
        .info-box strong {{ color: #667eea; }}
        .feature-list {{ margin: 25px 0; }}
        .feature-item {{ display: flex; align-items: center; margin: 12px 0; color: #555; }}
        .feature-icon {{ display: inline-block; width: 20px; height: 20px; background: #667eea; color: white; border-radius: 50%; text-align: center; line-height: 20px; margin-right: 12px; font-size: 12px; font-weight: bold; flex-shrink: 0; }}
        .divider {{ height: 1px; background: #eee; margin: 30px 0; }}
        .footer {{ background: #f8f9fa; padding: 20px; text-align: center; font-size: 12px; color: #999; }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='container'>
            <div class='header'>
                <h1>Welcome to InkVault</h1>
                <p>Your account is ready to go</p>
            </div>
            <div class='content'>
                <p class='greeting'>Hi {user.FirstName},</p>
                <p class='text'>Welcome to InkVault! We're thrilled to have you join our creative community of writers and journal enthusiasts. Your account has been successfully created.</p>
                <div class='info-box'>
                    <p><strong>Your Account Details</strong></p>
                    <p>Email: {user.Email}</p>
                    <p>Username: {user.UserName}</p>
                </div>
                <p class='text'><strong>What you can do now:</strong></p>
                <div class='feature-list'>
                    <div class='feature-item'>
                        <span class='feature-icon'>?</span>
                        <span>Create and publish your own journals</span>
                    </div>
                    <div class='feature-item'>
                        <span class='feature-icon'>?</span>
                        <span>Explore journals from other writers</span>
                    </div>
                    <div class='feature-item'>
                        <span class='feature-icon'>?</span>
                        <span>Connect with friends and build your network</span>
                    </div>
                    <div class='feature-item'>
                        <span class='feature-icon'>?</span>
                        <span>Save your favorite journals to your library</span>
                    </div>
                    <div class='feature-item'>
                        <span class='feature-icon'>?</span>
                        <span>Like and comment on journals</span>
                    </div>
                </div>
                <div class='divider'></div>
                <p class='text'>Get started by logging into your account and exploring the platform. You can customize your notification preferences in Settings anytime.</p>
                <p class='text' style='margin-top: 30px; color: #888; font-size: 14px;'>Have questions? Our support team is here to help.</p>
            </div>
            <div class='footer'>
                <p>© 2025 InkVault. All rights reserved.</p>
                <p style='margin-top: 10px;'>This is an automated message. Please do not reply to this email address.</p>
            </div>
        </div>
    </div>
</body>
</html>";

                await _emailService.SendEmailAsync(user.Email, subject, body);
                _logger.LogInformation($"Registration success email sent to {user.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending registration success email to {user.Email}: {ex.Message}");
            }
        }

        public async Task SendFriendRequestReceivedEmailAsync(FriendRequest request)
        {
            try
            {
                var preferences = await GetOrCreateNotificationPreferencesAsync(request.ReceiverId);
                
                if (!preferences.EmailOnFriendRequestReceived)
                    return;

                var sender = await _context.Users.FindAsync(request.SenderId);
                var receiver = await _context.Users.FindAsync(request.ReceiverId);

                if (sender == null || receiver == null)
                    return;

                var subject = $"New Friend Request from {sender.FirstName} {sender.LastName}";
                var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #333; background: #f5f5f5; }}
        .wrapper {{ background: #f5f5f5; padding: 40px 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; border-radius: 12px; box-shadow: 0 10px 40px rgba(102, 126, 234, 0.2); overflow: hidden; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 50px 30px; text-align: center; color: white; }}
        .header h1 {{ font-size: 28px; font-weight: 700; margin-bottom: 10px; }}
        .content {{ padding: 40px 30px; }}
        .user-card {{ background: linear-gradient(135deg, rgba(102, 126, 234, 0.1) 0%, rgba(118, 75, 162, 0.1) 100%); border-radius: 8px; padding: 20px; margin: 25px 0; border: 2px solid rgba(102, 126, 234, 0.2); }}
        .user-name {{ font-size: 18px; font-weight: 700; color: #333; }}
        .user-handle {{ color: #999; font-size: 14px; }}
        .message {{ color: #555; margin: 15px 0; line-height: 1.8; }}
        .divider {{ height: 1px; background: #eee; margin: 30px 0; }}
        .footer {{ background: #f8f9fa; padding: 20px; text-align: center; font-size: 12px; color: #999; }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='container'>
            <div class='header'>
                <h1>New Friend Request</h1>
                <p>Someone wants to connect with you</p>
            </div>
            <div class='content'>
                <p>Hi {receiver.FirstName},</p>
                <div class='user-card'>
                    <div class='user-name'>{sender.FirstName} {sender.LastName}</div>
                    <div class='user-handle'>@{sender.UserName}</div>
                </div>
                <p class='message'>This person would like to connect with you! Once you're friends, you can see their journals, like their posts, and stay updated with their latest writing.</p>
                <div class='divider'></div>
                <p class='message'>Log in to InkVault to accept or decline this friend request.</p>
                <p style='color: #999; font-size: 14px; margin-top: 30px;'>Managing your privacy matters to us. You can adjust your notification preferences in Settings anytime.</p>
            </div>
            <div class='footer'>
                <p>© 2025 InkVault. All rights reserved.</p>
                <p style='margin-top: 10px;'>This is an automated message. Please do not reply to this email address.</p>
            </div>
        </div>
    </div>
</body>
</html>";

                await _emailService.SendEmailAsync(receiver.Email, subject, body);
                _logger.LogInformation($"Friend request email sent to {receiver.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending friend request email: {ex.Message}");
            }
        }

        /// <summary>
        /// Send friend request accepted email
        /// </summary>
        public async Task SendFriendRequestAcceptedEmailAsync(FriendRequest request)
        {
            try
            {
                var preferences = await GetOrCreateNotificationPreferencesAsync(request.SenderId);
                
                if (!preferences.EmailOnFriendRequestAccepted)
                    return;

                var sender = await _context.Users.FindAsync(request.SenderId);
                var receiver = await _context.Users.FindAsync(request.ReceiverId);

                if (sender == null || receiver == null)
                    return;

                var subject = $"{receiver.FirstName} {receiver.LastName} accepted your friend request!";
                var body = $@"
                    <html>
                    <head>
                        <style>
                            body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background: linear-gradient(135deg, #51cf66 0%, #37b24d 100%); color: white; padding: 30px; text-align: center; border-radius: 8px 8px 0 0; }}
                            .content {{ background: #f5f5f5; padding: 30px; border-radius: 0 0 8px 8px; }}
                            .message {{ background: white; padding: 20px; border-radius: 5px; margin: 15px 0; border-left: 4px solid #51cf66; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>Friend Request Accepted!</h1>
                            </div>
                            <div class='content'>
                                <p>Hi {sender.FirstName},</p>
                                
                                <div class='message'>
                                    <p><strong>{receiver.FirstName} {receiver.LastName}</strong> (@{receiver.UserName}) has accepted your friend request!</p>
                                    <p>You can now see their journals and stay updated with their latest posts.</p>
                                </div>
                                
                                <p>Log in to InkVault to view their profile.</p>
                                
                                <p>Best regards,<br/>
                                <strong>The InkVault Team</strong></p>
                            </div>
                        </div>
                    </body>
                    </html>
                ";

                await _emailService.SendEmailAsync(sender.Email, subject, body);
                _logger.LogInformation($"Friend request accepted email sent to {sender.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending friend request accepted email: {ex.Message}");
            }
        }

        /// <summary>
        /// Send friend request denied email
        /// </summary>
        public async Task SendFriendRequestDeniedEmailAsync(FriendRequest request)
        {
            try
            {
                var preferences = await GetOrCreateNotificationPreferencesAsync(request.SenderId);
                
                if (!preferences.EmailOnFriendRequestDenied)
                    return;

                var sender = await _context.Users.FindAsync(request.SenderId);
                var receiver = await _context.Users.FindAsync(request.ReceiverId);

                if (sender == null || receiver == null)
                    return;

                var subject = $"{receiver.FirstName} {receiver.LastName} declined your friend request";
                var body = $@"
                    <html>
                    <head>
                        <style>
                            body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background: linear-gradient(135deg, #ffa94d 0%, #fd7e14 100%); color: white; padding: 30px; text-align: center; border-radius: 8px 8px 0 0; }}
                            .content {{ background: #f5f5f5; padding: 30px; border-radius: 0 0 8px 8px; }}
                            .message {{ background: white; padding: 20px; border-radius: 5px; margin: 15px 0; border-left: 4px solid #ffa94d; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>Friend Request Update</h1>
                            </div>
                            <div class='content'>
                                <p>Hi {sender.FirstName},</p>
                                
                                <div class='message'>
                                    <p><strong>{receiver.FirstName} {receiver.LastName}</strong> has declined your friend request.</p>
                                    <p>You can try again later or explore other writers and journals on InkVault.</p>
                                </div>
                                
                                <p>Keep exploring and connecting with the community!</p>
                                
                                <p>Best regards,<br/>
                                <strong>The InkVault Team</strong></p>
                            </div>
                        </div>
                    </body>
                    </html>
                ";

                await _emailService.SendEmailAsync(sender.Email, subject, body);
                _logger.LogInformation($"Friend request denied email sent to {sender.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending friend request denied email: {ex.Message}");
            }
        }

        /// <summary>
        /// Send login notification email with browser info
        /// </summary>
        private static DateTime ToIST(DateTime utcTime)
        {
            try
            {
                var ist = TimeZoneInfo.FindSystemTimeZoneById(
                    OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
                return TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(utcTime, DateTimeKind.Utc), ist);
            }
            catch
            {
                return utcTime.AddHours(5).AddMinutes(30);
            }
        }

        public async Task SendLoginNotificationEmailAsync(ApplicationUser user, string browserName)
        {
            try
            {
                var preferences = await GetOrCreateNotificationPreferencesAsync(user.Id);

                if (!preferences.EmailOnSuccessfulLogin)
                    return;

                var subject = "Login Activity - InkVault Account";
                var loginTime = ToIST(DateTime.UtcNow).ToString("MMMM dd, yyyy 'at' hh:mm tt 'IST'");
                
                var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #333; background: #f5f5f5; }}
        .wrapper {{ background: #f5f5f5; padding: 40px 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; border-radius: 12px; box-shadow: 0 10px 40px rgba(59, 130, 246, 0.2); overflow: hidden; }}
        .header {{ background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%); padding: 50px 30px; text-align: center; color: white; }}
        .header h1 {{ font-size: 28px; font-weight: 700; margin-bottom: 10px; }}
        .content {{ padding: 40px 30px; }}
        .activity-card {{ background: linear-gradient(135deg, rgba(59, 130, 246, 0.1) 0%, rgba(37, 99, 235, 0.1) 100%); border-radius: 8px; padding: 25px; margin: 25px 0; border: 2px solid rgba(59, 130, 246, 0.2); }}
        .activity-item {{ display: flex; align-items: center; padding: 10px 0; border-bottom: 1px solid rgba(59, 130, 246, 0.1); }}
        .activity-item:last-child {{ border-bottom: none; }}
        .activity-label {{ font-weight: 600; color: #555; min-width: 80px; }}
        .activity-value {{ color: #333; flex: 1; }}
        .activity-icon {{ font-size: 20px; margin-right: 10px; }}
        .warning-box {{ background: linear-gradient(135deg, rgba(239, 68, 68, 0.1) 0%, rgba(220, 38, 38, 0.1) 100%); border-left: 4px solid #ef4444; padding: 15px; border-radius: 6px; margin: 20px 0; }}
        .warning-box strong {{ color: #dc2626; }}
        .divider {{ height: 1px; background: #eee; margin: 30px 0; }}
        .footer {{ background: #f8f9fa; padding: 20px; text-align: center; font-size: 12px; color: #999; }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='container'>
            <div class='header'>
                <h1>Login Activity</h1>
                <p>Successful sign-in detected</p>
            </div>
            <div class='content'>
                <p>Hi {user.FirstName},</p>
                <p>A successful login was detected on your InkVault account.</p>
                <div class='activity-card'>
                    <div class='activity-item'>
                        <div style='font-weight: 600; color: #555; min-width: 80px;'>Email:</div>
                        <div style='color: #333; flex: 1;'>{user.Email}</div>
                    </div>
                    <div class='activity-item'>
                        <div style='font-weight: 600; color: #555; min-width: 80px;'>Browser:</div>
                        <div style='color: #333; flex: 1;'>{browserName}</div>
                    </div>
                    <div class='activity-item'>
                        <div style='font-weight: 600; color: #555; min-width: 80px;'>Time:</div>
                        <div style='color: #333; flex: 1;'>{loginTime}</div>
                    </div>
                </div>
                <div class='divider'></div>
                <div class='warning-box'>
                    <strong>Security Alert:</strong>
                    <p>If this wasn't you, please change your password immediately to keep your account secure.</p>
                </div>
                <p style='color: #999; font-size: 14px; margin-top: 30px;'>This is an automated security notification. We're here to help you stay safe.</p>
            </div>
            <div class='footer'>
                <p>© 2025 InkVault. All rights reserved.</p>
                <p style='margin-top: 10px;'>This is an automated message. Please do not reply to this email address.</p>
            </div>
        </div>
    </div>
</body>
</html>";

                await _emailService.SendEmailAsync(user.Email, subject, body);
                _logger.LogInformation($"Login notification email sent to {user.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending login notification email: {ex.Message}");
            }
        }

        /// <summary>
        /// Send friend journal post notification email
        /// </summary>
        public async Task SendFriendJournalPostEmailAsync(Journal journal, IEnumerable<string> friendIds)
        {
            try
            {
                var author = await _context.Users.FindAsync(journal.UserId);
                if (author == null)
                    return;

                foreach (var friendId in friendIds)
                {
                    var preferences = await GetOrCreateNotificationPreferencesAsync(friendId);
                    
                    if (!preferences.EmailOnFriendJournalPost)
                        continue;

                    var friend = await _context.Users.FindAsync(friendId);
                    if (friend == null)
                        continue;

                    var subject = $"{author.FirstName} posted a new journal: {journal.Title}";
                    var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #333; background: #f5f5f5; }}
        .wrapper {{ background: #f5f5f5; padding: 40px 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; border-radius: 12px; box-shadow: 0 10px 40px rgba(167, 139, 250, 0.2); overflow: hidden; }}
        .header {{ background: linear-gradient(135deg, #a78bfa 0%, #8b5cf6 100%); padding: 50px 30px; text-align: center; color: white; }}
        .header h1 {{ font-size: 28px; font-weight: 700; margin-bottom: 10px; }}
        .content {{ padding: 40px 30px; }}
        .author-info {{ color: #666; margin-bottom: 20px; font-weight: 500; }}
        .journal-card {{ background: linear-gradient(135deg, rgba(167, 139, 250, 0.1) 0%, rgba(139, 92, 246, 0.1) 100%); border-radius: 8px; padding: 25px; margin: 25px 0; border: 2px solid rgba(167, 139, 250, 0.2); }}
        .journal-title {{ font-size: 22px; font-weight: 700; color: #333; margin-bottom: 10px; }}
        .journal-meta {{ display: flex; gap: 20px; margin: 15px 0; color: #666; font-size: 14px; }}
        .journal-topic {{ background: rgba(167, 139, 250, 0.15); color: #333; padding: 8px 12px; border-radius: 4px; display: inline-block; margin-top: 10px; }}
        .cta-section {{ margin-top: 30px; padding-top: 30px; border-top: 1px solid #eee; }}
        .footer {{ background: #f8f9fa; padding: 20px; text-align: center; font-size: 12px; color: #999; }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='container'>
            <div class='header'>
                <h1>New Journal Post</h1>
                <p>From someone you're following</p>
            </div>
            <div class='content'>
                <p>Hi {friend.FirstName},</p>
                <p class='author-info'>{author.FirstName} {author.LastName} just published a new journal!</p>
                <div class='journal-card'>
                    <div class='journal-title'>{journal.Title}</div>
                    <div class='journal-meta'>
                        <span>{journal.CreatedAt:MMMM dd, yyyy}</span>
                        <span>{(string.IsNullOrEmpty(journal.Topic) ? "General" : journal.Topic)}</span>
                    </div>
                    <div class='journal-topic'>{(string.IsNullOrEmpty(journal.Topic) ? "Check it out" : journal.Topic)}</div>
                </div>
                <p>This is a great opportunity to stay connected with {author.FirstName}'s writing journey and discover new perspectives!</p>
                <div class='cta-section'>
                    <p>Log in to InkVault to read the full journal, leave your thoughts, and engage with the community.</p>
                </div>
                <p style='color: #999; font-size: 14px; margin-top: 30px;'>Happy reading!</p>
            </div>
            <div class='footer'>
                <p>© 2025 InkVault. All rights reserved.</p>
                <p style='margin-top: 10px;'>This is an automated message. Please do not reply to this email address.</p>
            </div>
        </div>
    </div>
</body>
</html>";

                    await _emailService.SendEmailAsync(friend.Email, subject, body);
                    _logger.LogInformation($"Friend journal post email sent to {friend.Email}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending friend journal post email: {ex.Message}");
            }
        }

        /// <summary>
        /// Send full text request notification email to journal owner
        /// </summary>
        public async Task SendFullTextRequestEmailAsync(Journal journal, ApplicationUser requester)
        {
            try
            {
                var owner = await _context.Users.FindAsync(journal.UserId);
                if (owner == null)
                    return;

                // Check if owner wants to receive these notifications
                var preferences = await GetOrCreateNotificationPreferencesAsync(journal.UserId);
                // Note: You may want to add a new preference setting for this later
                // For now, we'll always send it

                var subject = $"Full Text Request for '{journal.Title}'";
                var requestTime = ToIST(DateTime.UtcNow).ToString("MMMM dd, yyyy 'at' hh:mm tt 'IST'");

                var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        * {{{{ margin: 0; padding: 0; box-sizing: border-box; }}}}
        body {{{{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #333; background: #f5f5f5; }}}}
        .wrapper {{{{ background: #f5f5f5; padding: 40px 20px; }}}}
        .container {{{{ max-width: 600px; margin: 0 auto; background: white; border-radius: 12px; box-shadow: 0 10px 40px rgba(102, 126, 234, 0.2); overflow: hidden; }}}}
        .header {{{{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 50px 30px; text-align: center; color: white; }}}}
        .header h1 {{{{ font-size: 28px; font-weight: 700; margin-bottom: 10px; }}}}
        .header p {{{{ font-size: 16px; opacity: 0.9; }}}}
        .content {{{{ padding: 40px 30px; }}}}
        .request-card {{{{ background: linear-gradient(135deg, rgba(102, 126, 234, 0.1) 0%, rgba(118, 75, 162, 0.1) 100%); border-radius: 8px; padding: 25px; margin: 25px 0; border: 2px solid rgba(102, 126, 234, 0.2); }}}}
        .journal-title {{{{ font-size: 18px; font-weight: 700; color: #667eea; margin: 15px 0; }}}}
        .info-box {{{{ background: rgba(102, 126, 234, 0.1); padding: 15px; border-radius: 6px; margin: 20px 0; border-left: 4px solid #667eea; }}}}
        .footer {{{{ background: #f8f9fa; padding: 20px; text-align: center; font-size: 12px; color: #999; }}}}
        .icon {{{{ display: inline-block; width: 24px; height: 24px; margin-right: 8px; vertical-align: middle; }}}}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='container'>
            <div class='header'>
                <h1>&#128231; Full Text Request</h1>
                <p>Someone wants to read your full journal</p>
            </div>
            <div class='content'>
                <p>Hi {owner.FirstName},</p>
                <p style='margin-top: 15px;'>A user has requested access to view the full text of your journal that currently only displays an abstract.</p>

                <div class='journal-title'>&quot;{journal.Title}&quot;</div>

                <div class='request-card'>
                    <p style='margin: 8px 0; color: #333; line-height: 1.8;'><strong>Requester:</strong> {requester.FirstName} {requester.LastName}</p>
                    <p style='margin: 8px 0; color: #333; line-height: 1.8;'><strong>Username:</strong> @{requester.UserName}</p>
                    <p style='margin: 8px 0; color: #333; line-height: 1.8;'><strong>Email:</strong> {requester.Email}</p>
                    <p style='margin: 8px 0; color: #333; line-height: 1.8;'><strong>Request Time:</strong> {requestTime}</p>
                    <p style='margin: 8px 0; color: #333; line-height: 1.8;'><strong>Journal DUI:</strong> {journal.DUI ?? "N/A"}</p>
                </div>

                <div class='info-box'>
                    <strong>&#128221; What this means:</strong>
                    <p style='margin-top: 10px;'>The user is interested in reading the complete content of your journal beyond the abstract. This request is for your information and you may choose how to respond.</p>
                </div>

                <p style='margin-top: 25px; color: #666; font-size: 14px;'>
                    You can manage your journals and privacy settings by logging into your InkVault account.
                </p>
            </div>
            <div class='footer'>
                <p>&copy; 2025 InkVault. All rights reserved.</p>
                <p style='margin-top: 10px;'>This is an automated message. Please do not reply to this email address.</p>
            </div>
        </div>
    </div>
</body>
</html>";

                await _emailService.SendEmailAsync(owner.Email, subject, body);
                _logger.LogInformation($"Full text request email sent to {owner.Email} for journal {journal.JournalId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending full text request email: {ex.Message}");
            }
        }

        // ===================================================
        // EMAIL NOTIFICATIONS — Likes & Comments
        // ===================================================

        public async Task SendJournalLikedEmailAsync(string actorId, Journal journal)
        {
            try
            {
                if (journal.UserId == actorId) return; // don't notify on self-like

                var preferences = await GetOrCreateNotificationPreferencesAsync(journal.UserId);
                if (!preferences.EmailOnJournalLiked) return;

                var actor  = await _context.Users.FindAsync(actorId);
                var owner  = await _context.Users.FindAsync(journal.UserId);
                if (actor == null || owner == null) return;

                var likedAt = ToIST(DateTime.UtcNow).ToString("MMMM dd, yyyy 'at' hh:mm tt 'IST'");
                var subject = $"{actor.FirstName} {actor.LastName} liked your journal \"{journal.Title}\"";

                var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f5f5f5; color: #333; }}
        .wrapper {{ background: #f5f5f5; padding: 40px 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; border-radius: 12px; box-shadow: 0 10px 40px rgba(233,30,99,.15); overflow: hidden; }}
        .header {{ background: linear-gradient(135deg, #e91e63 0%, #ad1457 100%); padding: 40px 30px; text-align: center; color: white; }}
        .header h1 {{ font-size: 26px; font-weight: 700; margin-bottom: 8px; }}
        .content {{ padding: 35px 30px; }}
        .card {{ background: #fce4ec; border-left: 4px solid #e91e63; padding: 20px; border-radius: 6px; margin: 20px 0; }}
        .card .name {{ font-size: 18px; font-weight: 700; color: #333; }}
        .card .handle {{ font-size: 14px; color: #999; margin-top: 4px; }}
        .info-row {{ display: flex; gap: 12px; margin: 8px 0; font-size: 14px; color: #555; }}
        .footer {{ background: #f8f9fa; padding: 18px; text-align: center; font-size: 12px; color: #999; }}
    </style>
</head>
<body>
<div class='wrapper'><div class='container'>
    <div class='header'>
        <h1>&#10084; Journal Liked</h1>
        <p>Someone loved your writing</p>
    </div>
    <div class='content'>
        <p>Hi {owner.FirstName},</p>
        <div class='card'>
            <div class='name'>{actor.FirstName} {actor.LastName}</div>
            <div class='handle'>@{actor.UserName}</div>
        </div>
        <div class='info-row'><strong>Journal:</strong>&nbsp;&quot;{journal.Title}&quot;</div>
        <div class='info-row'><strong>Date:</strong>&nbsp;{likedAt}</div>
        <p style='margin-top:20px;color:#555;'>Log in to InkVault to see all your likes.</p>
    </div>
    <div class='footer'>&copy; 2025 InkVault. All rights reserved.</div>
</div></div>
</body></html>";

                await _emailService.SendEmailAsync(owner.Email, subject, body);
                _logger.LogInformation($"Journal liked email sent to {owner.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending journal liked email: {ex.Message}");
            }
        }

        public async Task SendJournalCommentedEmailAsync(string actorId, Journal journal, string commentContent)
        {
            try
            {
                if (journal.UserId == actorId) return; // don't notify on self-comment

                var preferences = await GetOrCreateNotificationPreferencesAsync(journal.UserId);
                if (!preferences.EmailOnJournalCommented) return;

                var actor = await _context.Users.FindAsync(actorId);
                var owner = await _context.Users.FindAsync(journal.UserId);
                if (actor == null || owner == null) return;

                var commentedAt = ToIST(DateTime.UtcNow).ToString("MMMM dd, yyyy 'at' hh:mm tt 'IST'");
                var preview     = commentContent.Length > 200 ? commentContent[..200] + "…" : commentContent;
                var subject     = $"{actor.FirstName} {actor.LastName} commented on \"{journal.Title}\"";

                var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f5f5f5; color: #333; }}
        .wrapper {{ background: #f5f5f5; padding: 40px 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; border-radius: 12px; box-shadow: 0 10px 40px rgba(102,126,234,.15); overflow: hidden; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 40px 30px; text-align: center; color: white; }}
        .header h1 {{ font-size: 26px; font-weight: 700; margin-bottom: 8px; }}
        .content {{ padding: 35px 30px; }}
        .user-card {{ background: #f3f0ff; border-left: 4px solid #667eea; padding: 18px; border-radius: 6px; margin: 20px 0; }}
        .user-card .name {{ font-size: 18px; font-weight: 700; color: #333; }}
        .user-card .handle {{ font-size: 14px; color: #999; margin-top: 4px; }}
        .comment-box {{ background: #f8f9ff; border: 1px solid rgba(102,126,234,.2); border-radius: 8px; padding: 18px; margin: 20px 0; font-style: italic; color: #444; line-height: 1.7; }}
        .info-row {{ display: flex; gap: 12px; margin: 8px 0; font-size: 14px; color: #555; }}
        .footer {{ background: #f8f9fa; padding: 18px; text-align: center; font-size: 12px; color: #999; }}
    </style>
</head>
<body>
<div class='wrapper'><div class='container'>
    <div class='header'>
        <h1>&#128172; New Comment</h1>
        <p>Someone replied to your journal</p>
    </div>
    <div class='content'>
        <p>Hi {owner.FirstName},</p>
        <div class='user-card'>
            <div class='name'>{actor.FirstName} {actor.LastName}</div>
            <div class='handle'>@{actor.UserName}</div>
        </div>
        <div class='info-row'><strong>Journal:</strong>&nbsp;&quot;{journal.Title}&quot;</div>
        <div class='info-row'><strong>Date:</strong>&nbsp;{commentedAt}</div>
        <div class='comment-box'>&ldquo;{preview}&rdquo;</div>
        <p style='color:#555;'>Log in to InkVault to read the full comment and reply.</p>
    </div>
    <div class='footer'>&copy; 2025 InkVault. All rights reserved.</div>
</div></div>
</body></html>";

                await _emailService.SendEmailAsync(owner.Email, subject, body);
                _logger.LogInformation($"Journal commented email sent to {owner.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending journal commented email: {ex.Message}");
            }
        }

        // ===================================================
        // IN-APP NOTIFICATIONS  (no email sent)
        // ===================================================

        public async Task CreateInAppNotificationAsync(
            string recipientId,
            string? actorId,
            NotificationType type,
            string message,
            string? resourceUrl = null,
            int? journalId = null,
            string? journalTitle = null)
        {
            if (recipientId == actorId) return; // never notify yourself

            _context.AppNotifications.Add(new AppNotification
            {
                RecipientId  = recipientId,
                ActorId      = actorId,
                Type         = type,
                Message      = message,
                ResourceUrl  = resourceUrl,
                JournalId    = journalId,
                JournalTitle = journalTitle,
                IsRead       = false,
                CreatedAt    = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        public async Task NotifyLikeAsync(string actorId, Journal journal)
        {
            if (journal.UserId == actorId) return;
            var actor = await _context.Users.FindAsync(actorId);
            if (actor == null) return;
            await CreateInAppNotificationAsync(
                journal.UserId, actorId, NotificationType.LikeReceived,
                $"{actor.FirstName} {actor.LastName} liked your journal \"{journal.Title}\"",
                $"/Journal/View/{journal.JournalId}", journal.JournalId, journal.Title);
        }

        public async Task NotifyUnlikeAsync(string actorId, Journal journal)
        {
            if (journal.UserId == actorId) return;
            var actor = await _context.Users.FindAsync(actorId);
            if (actor == null) return;
            await CreateInAppNotificationAsync(
                journal.UserId, actorId, NotificationType.LikeRemoved,
                $"{actor.FirstName} {actor.LastName} removed their like from \"{journal.Title}\"",
                $"/Journal/View/{journal.JournalId}", journal.JournalId, journal.Title);
        }

        public async Task NotifyCommentAsync(string actorId, Journal journal, string commentContent)
        {
            if (journal.UserId == actorId) return;
            var actor = await _context.Users.FindAsync(actorId);
            if (actor == null) return;
            var preview = commentContent.Length > 80 ? commentContent[..80] + "\u2026" : commentContent;
            await CreateInAppNotificationAsync(
                journal.UserId, actorId, NotificationType.CommentReceived,
                $"{actor.FirstName} {actor.LastName} commented on \"{journal.Title}\": \"{preview}\"",
                $"/Journal/View/{journal.JournalId}", journal.JournalId, journal.Title);
        }

        public async Task NotifyCommentDeletedAsync(string actorId, Journal journal)
        {
            if (journal.UserId == actorId) return;
            var actor = await _context.Users.FindAsync(actorId);
            if (actor == null) return;
            await CreateInAppNotificationAsync(
                journal.UserId, actorId, NotificationType.CommentDeleted,
                $"{actor.FirstName} {actor.LastName} deleted their comment on \"{journal.Title}\"",
                $"/Journal/View/{journal.JournalId}", journal.JournalId, journal.Title);
        }

        public async Task NotifyFriendRequestReceivedInAppAsync(FriendRequest request)
        {
            var sender = await _context.Users.FindAsync(request.SenderId);
            if (sender == null) return;
            await CreateInAppNotificationAsync(
                request.ReceiverId, request.SenderId, NotificationType.FriendRequestReceived,
                $"{sender.FirstName} {sender.LastName} sent you a friend request",
                $"/Profile/ViewPublic/{request.SenderId}");
        }

        public async Task NotifyFriendRequestAcceptedInAppAsync(FriendRequest request)
        {
            var receiver = await _context.Users.FindAsync(request.ReceiverId);
            if (receiver == null) return;
            await CreateInAppNotificationAsync(
                request.SenderId, request.ReceiverId, NotificationType.FriendRequestAccepted,
                $"{receiver.FirstName} {receiver.LastName} accepted your friend request",
                $"/Profile/ViewPublic/{request.ReceiverId}");
        }

        public async Task NotifyFriendRequestDeclinedInAppAsync(FriendRequest request)
        {
            var receiver = await _context.Users.FindAsync(request.ReceiverId);
            if (receiver == null) return;
            await CreateInAppNotificationAsync(
                request.SenderId, request.ReceiverId, NotificationType.FriendRequestDeclined,
                $"{receiver.FirstName} {receiver.LastName} declined your friend request",
                $"/Profile/ViewPublic/{request.ReceiverId}");
        }

        public async Task NotifyFriendRequestWithdrawnInAppAsync(string senderId, string receiverId)
        {
            var sender = await _context.Users.FindAsync(senderId);
            if (sender == null) return;
            await CreateInAppNotificationAsync(
                receiverId, senderId, NotificationType.FriendRequestWithdrawn,
                $"{sender.FirstName} {sender.LastName} withdrew their friend request",
                $"/Profile/ViewPublic/{senderId}");
        }

        public async Task NotifyFriendRemovedAsync(string actorId, string recipientId)
        {
            var actor = await _context.Users.FindAsync(actorId);
            if (actor == null) return;
            await CreateInAppNotificationAsync(
                recipientId, actorId, NotificationType.FriendRemoved,
                $"{actor.FirstName} {actor.LastName} removed you from their friends",
                $"/Profile/ViewPublic/{actorId}");
        }

        public async Task NotifyJournalReferencedAsync(string actorId, Journal referencedJournal, Journal newJournal)
        {
            if (referencedJournal.UserId == actorId) return;
            var actor = await _context.Users.FindAsync(actorId);
            if (actor == null) return;
            var actorName = newJournal.IsAnonymous ? "Someone" : $"{actor.FirstName} {actor.LastName}";
            await CreateInAppNotificationAsync(
                referencedJournal.UserId,
                newJournal.IsAnonymous ? null : actorId,
                NotificationType.JournalReferenced,
                $"{actorName} referenced your journal \"{referencedJournal.Title}\" in a new journal \"{newJournal.Title}\"",
                $"/Journal/View/{newJournal.JournalId}",
                referencedJournal.JournalId, referencedJournal.Title);
        }

        public async Task NotifyJournalPublishedInAppAsync(Journal journal, IEnumerable<string> friendIds)
        {
            if (journal.IsAnonymous) return;
            var author = await _context.Users.FindAsync(journal.UserId);
            if (author == null) return;
            foreach (var friendId in friendIds)
            {
                await CreateInAppNotificationAsync(
                    friendId, journal.UserId, NotificationType.JournalPublished,
                    $"{author.FirstName} {author.LastName} published a new journal: \"{journal.Title}\"",
                    $"/Journal/View/{journal.JournalId}", journal.JournalId, journal.Title);
            }
        }

        public async Task NotifyFullTextRequestedInAppAsync(Journal journal, ApplicationUser requester)
        {
            await CreateInAppNotificationAsync(
                journal.UserId, requester.Id, NotificationType.FullTextRequested,
                $"{requester.FirstName} {requester.LastName} requested the full text of your journal \"{journal.Title}\"",
                $"/Journal/View/{journal.JournalId}", journal.JournalId, journal.Title);
        }
    }
}
