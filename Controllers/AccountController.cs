using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using InkVault.Models;
using InkVault.ViewModels;
using InkVault.Services;
using InkVault.Data;
using Microsoft.EntityFrameworkCore;

namespace InkVault.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailService _emailService;
        private readonly IOTPService _otpService;
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailService emailService,
            IOTPService otpService,
            ApplicationDbContext context,
            NotificationService notificationService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _otpService = otpService;
            _context = context;
            _notificationService = notificationService;
        }

        [HttpGet]
        public IActionResult Welcome()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Welcome(WelcomeViewModel model)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] ============================================");
                System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] DATABASE CONNECTION TEST");
                System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Connection string contains: inkvault_dev");
                System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] ============================================");
                System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Attempting login for username: {model.Username}");

                if (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
                {
                    System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Username or password empty");
                    ModelState.AddModelError(string.Empty, "Username and password are required.");
                    return View(model);
                }

                System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Calling FindByNameAsync for: {model.Username}");
                var user = await _userManager.FindByNameAsync(model.Username);
                
                if (user == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] ? User NOT found in database: {model.Username}");
                    System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] This means either:");
                    System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] 1. User doesn't exist in database");
                    System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] 2. App is connecting to WRONG database");
                    System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] 3. Connection string is incorrect");
                    ModelState.AddModelError(string.Empty, "Invalid username or password.");
                    return View(model);
                }

                System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] ? User found: {user.UserName}, EmailVerified: {user.EmailVerified}");

                // Check if account is currently locked out
                if (await _userManager.IsLockedOutAsync(user))
                {
                    System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Account is locked out for user: {user.UserName}");
                    var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                    if (lockoutEnd.HasValue)
                    {
                        var timeRemaining = lockoutEnd.Value.Subtract(DateTimeOffset.UtcNow);
                        if (timeRemaining > TimeSpan.Zero)
                        {
                            System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Lockout still active, time remaining: {timeRemaining}");
                            ModelState.AddModelError(string.Empty, "Your account is temporarily locked due to multiple failed login attempts. Please try again after 1 hour.");
                            return View(model);
                        }
                    }
                }

                // Sign in with lockout enabled
                System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Attempting password sign in for: {user.UserName}");
                var result = await _signInManager.PasswordSignInAsync(
                    user,
                    model.Password,
                    isPersistent: true,
                    lockoutOnFailure: true); // Enable lockout on failure

                System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Sign in result: Succeeded={result.Succeeded}, IsLockedOut={result.IsLockedOut}, RequiresTwoFactor={result.RequiresTwoFactor}");

                if (result.Succeeded)
                {
                    System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] ? Password correct for: {user.UserName}");
                    
                    // Reset the failed access count on successful login
                    await _userManager.ResetAccessFailedCountAsync(user);
                    System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Failed access count reset for: {user.UserName}");
                    
                    // If email not verified, redirect to OTP verification
                    if (!user.EmailVerified)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Email not verified, redirecting to OTP");
                        await _signInManager.SignOutAsync();
                        TempData["Warning"] = "Please verify your email first.";
                        return RedirectToAction("VerifyOTP", new { email = user.Email });
                    }

                    // Update last login time
                    user.LastLoginAt = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);

                    // Check if user requires OTP on login
                    try
                    {
                        var notificationPref = await _context.NotificationPreferences
                            .FirstOrDefaultAsync(np => np.UserId == user.Id);
                        
                        // If notification preference doesn't exist, create default one
                        if (notificationPref == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] NotificationPreference not found for user {user.Id}, creating default");
                            notificationPref = new NotificationPreference
                            {
                                UserId = user.Id,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            _context.NotificationPreferences.Add(notificationPref);
                            await _context.SaveChangesAsync();
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Notification pref found - RequireOTPOnEveryLogin: {notificationPref.RequireOTPOnEveryLogin}");
                        
                        if (notificationPref.RequireOTPOnEveryLogin)
                        {
                            System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] RequireOTPOnEveryLogin enabled for: {user.UserName}");
                            
                            // Generate OTP
                            var otp = _otpService.GenerateOTP();
                            user.OTP = otp;
                            user.OTPExpiration = DateTime.UtcNow.AddMinutes(10);
                            user.OTPSentAt = DateTime.UtcNow;
                            user.OTPResendCount = 0;
                            user.OTPResendLockedUntil = null;
                            await _userManager.UpdateAsync(user);

                            System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] OTP generated and saved for: {user.UserName}");
                            
                            // Send OTP email
                            try
                            {
                                _ = _emailService.SendOTPAsync(user.Email, otp);
                                System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] OTP email sent to: {user.Email}");
                            }
                            catch (Exception emailEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Error sending OTP email: {emailEx.Message}");
                                // Continue - OTP can be sent later
                            }
                            
                            // Sign out and redirect to OTP verification
                            await _signInManager.SignOutAsync();
                            System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] User signed out - redirecting to OTP verification");
                            return RedirectToAction("VerifyOTP", new { email = user.Email, purpose = "Login" });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Error checking OTP requirement: {ex.Message}");
                        // Continue with login - OTP check failed
                    }

                    // Send login notification email if enabled (only if OTP not required)
                    try
                    {
                        var userAgent = Request.Headers["User-Agent"].ToString();
                        var browserName = BrowserDetectionService.GetBrowserName(userAgent);
                        await _notificationService.SendLoginNotificationEmailAsync(user, browserName);
                    }
                    catch (Exception emailEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Error sending login notification email: {emailEx.Message}");
                        // Non-blocking - don't interrupt login flow
                    }

                    System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] ? LOGIN SUCCESSFUL for: {user.UserName}");
                    return RedirectToAction("Index", "Home");
                }

                if (result.IsLockedOut)
                {
                    System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Account locked out for: {user.UserName}");
                    
                    // Send security alert email for lockout
                    try
                    {
                        var userAgent = Request.Headers["User-Agent"].ToString();
                        var browserName = BrowserDetectionService.GetBrowserName(userAgent);
                        await SendLockoutSecurityAlertEmail(user, browserName);
                        System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Lockout security alert email sent to: {user.Email}");
                    }
                    catch (Exception emailEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Error sending lockout security alert email: {emailEx.Message}");
                        // Non-blocking - don't interrupt login flow
                    }
                    
                    ModelState.AddModelError(string.Empty, "Your account is temporarily locked due to multiple failed login attempts. Please try again after 1 hour.");
                    return View(model);
                }

                if (result.RequiresTwoFactor)
                {
                    System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Two-factor required for: {user.UserName}");
                    return RedirectToAction("LoginWith2fa", new { rememberMe = true });
                }

                System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] ? Password incorrect for: {user.UserName}");

                // Check if user is now locked out after this failed attempt
                if (await _userManager.IsLockedOutAsync(user))
                {
                    System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Account became locked out after failed attempt for: {user.UserName}");
                    
                    // Send security alert email for lockout
                    try
                    {
                        var userAgent = Request.Headers["User-Agent"].ToString();
                        var browserName = BrowserDetectionService.GetBrowserName(userAgent);
                        await SendLockoutSecurityAlertEmail(user, browserName);
                        System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Lockout security alert email sent to: {user.Email}");
                    }
                    catch (Exception emailEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Error sending lockout security alert email: {emailEx.Message}");
                        // Non-blocking
                    }
                    
                    ModelState.AddModelError(string.Empty, "Your account is temporarily locked due to multiple failed login attempts. Please try again after 1 hour.");
                }
                else
                {
                    var failedCount = await _userManager.GetAccessFailedCountAsync(user);
                    System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Failed access count for {user.UserName}: {failedCount}");
                    ModelState.AddModelError(string.Empty, "Invalid username or password.");
                }
                
                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] ? EXCEPTION: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[WELCOME_LOGIN] Stack Trace: {ex.StackTrace}");
                ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[REGISTER] Starting registration for username: {model.Username}");

                if (!ModelState.IsValid)
                {
                    System.Diagnostics.Debug.WriteLine($"[REGISTER] ModelState invalid");
                    return View(model);
                }

                System.Diagnostics.Debug.WriteLine($"[REGISTER] Checking if username exists: {model.Username}");
                // Check if username exists and is verified
                var existingUserByUsername = await _userManager.FindByNameAsync(model.Username);
                if (existingUserByUsername != null && existingUserByUsername.EmailVerified)
                {
                    System.Diagnostics.Debug.WriteLine($"[REGISTER] Username already in use (verified): {model.Username}");
                    ModelState.AddModelError(nameof(model.Username), "Username is already in use.");
                    return View(model);
                }

                System.Diagnostics.Debug.WriteLine($"[REGISTER] Checking if email exists: {model.Email}");
                // Check if there's an unverified account with the same email for the same username
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                
                ApplicationUser user;

                // If unverified user exists with same email and username, delete and recreate
                if (existingUser != null && !existingUser.EmailVerified && existingUser.UserName == model.Username)
                {
                    System.Diagnostics.Debug.WriteLine($"[REGISTER] Deleting previous unverified account");
                    // Delete the previous unverified registration
                    await _userManager.DeleteAsync(existingUser);
                    user = new ApplicationUser
                    {
                        UserName = model.Username,
                        Email = model.Email,
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        PhoneNumber = model.PhoneNumber,
                        Gender = model.Gender,
                        DateOfBirth = model.DateOfBirth,
                        EmailVerified = false,
                        CreatedAt = DateTime.UtcNow
                    };
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[REGISTER] Creating new user");
                    // Create new user (allow same email with different username)
                    user = new ApplicationUser
                    {
                        UserName = model.Username,
                        Email = model.Email,
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        PhoneNumber = model.PhoneNumber,
                        Gender = model.Gender,
                        DateOfBirth = model.DateOfBirth,
                        EmailVerified = false,
                        CreatedAt = DateTime.UtcNow
                    };
                }

                System.Diagnostics.Debug.WriteLine($"[REGISTER] Calling CreateAsync for user: {user.UserName}");
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    System.Diagnostics.Debug.WriteLine($"[REGISTER] User created successfully: {user.UserName}");
                    var otp = _otpService.GenerateOTP();
                    user.OTP = otp;
                    user.OTPExpiration = DateTime.UtcNow.AddMinutes(10);
                    user.OTPSentAt = DateTime.UtcNow;
                    user.OTPResendCount = 0;
                    user.OTPResendLockedUntil = null;
                    await _userManager.UpdateAsync(user);
                    // Send OTP email asynchronously (non-blocking)
                    _ = _emailService.SendOTPAsync(model.Email, otp);

                    TempData["Email"] = model.Email;
                    System.Diagnostics.Debug.WriteLine($"[REGISTER] Redirecting to VerifyOTP");
                    return RedirectToAction("VerifyOTP", new { email = model.Email });
                }

                System.Diagnostics.Debug.WriteLine($"[REGISTER] CreateAsync failed. Errors count: {result.Errors.Count()}");
                foreach (var error in result.Errors)
                {
                    System.Diagnostics.Debug.WriteLine($"[REGISTER] Error: {error.Code} - {error.Description}");
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[REGISTER] EXCEPTION: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[REGISTER] Stack Trace: {ex.StackTrace}");
                ModelState.AddModelError(string.Empty, $"An error occurred during registration: {ex.Message}");
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> VerifyOTP(string email, string purpose = "Registration")
        {
            var model = new VerifyOTPViewModel
            {
                Email = email,
                Purpose = purpose
            };

            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                ViewBag.OTPSentAt       = user.OTPSentAt;
                ViewBag.ResendCount     = user.OTPResendCount;
                ViewBag.IsLocked        = user.OTPResendLockedUntil.HasValue && user.OTPResendLockedUntil > DateTime.UtcNow;
                ViewBag.LockedUntil     = user.OTPResendLockedUntil;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOTP(VerifyOTPViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(model);

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "User not found.");
                    return View(model);
                }

                if (string.IsNullOrEmpty(user.OTP) || user.OTP != model.OTP)
                {
                    ModelState.AddModelError(nameof(model.OTP), "Invalid OTP.");
                    return View(model);
                }

                if (user.OTPExpiration < DateTime.UtcNow)
                {
                    ModelState.AddModelError(nameof(model.OTP), "OTP has expired. Please request a new one.");
                    return View(model);
                }

                if (model.Purpose == "Registration")
                {
                    user.EmailVerified = true;
                    user.OTP = null;
                    user.OTPExpiration = null;
                    user.OTPSentAt = null;
                    user.OTPResendCount = 0;
                    user.OTPResendLockedUntil = null;
                    await _userManager.UpdateAsync(user);
                    TempData["Success"] = "Email verified successfully. You can now login.";
                    return RedirectToAction("Welcome");
                }
                else if (model.Purpose == "PasswordReset")
                {
                    HttpContext.Session.SetString("ResetEmail", model.Email);
                    return RedirectToAction("ResetPassword");
                }
                else if (model.Purpose == "Login")
                {
                    // Clear OTP and reset failed access count
                    user.OTP = null;
                    user.OTPExpiration = null;
                    user.OTPSentAt = null;
                    user.OTPResendCount = 0;
                    user.OTPResendLockedUntil = null;
                    await _userManager.UpdateAsync(user);
                    
                    // Reset failed access count on successful OTP verification
                    await _userManager.ResetAccessFailedCountAsync(user);
                    
                    // Sign in the user
                    await _signInManager.SignInAsync(user, isPersistent: true);
                    
                    System.Diagnostics.Debug.WriteLine($"[LOGIN_OTP] OTP verified successfully for: {user.UserName}");
                    
                    // Send login successful email after OTP verification
                    try
                    {
                        var userAgent = Request.Headers["User-Agent"].ToString();
                        var browserName = BrowserDetectionService.GetBrowserName(userAgent);
                        await _notificationService.SendLoginNotificationEmailAsync(user, browserName);
                        System.Diagnostics.Debug.WriteLine($"[LOGIN_OTP] Login notification email sent to: {user.Email}");
                    }
                    catch (Exception emailEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LOGIN_OTP] Error sending login notification email: {emailEx.Message}");
                        // Non-blocking - don't interrupt login flow
                    }
                    
                    TempData["Success"] = "Login successful!";
                    return RedirectToAction("Index", "Home");
                }

                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VerifyOTP Error: {ex.Message}");
                ModelState.AddModelError(string.Empty, "An error occurred. Please try again.");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOTP(string email, string purpose)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return RedirectToAction("VerifyOTP", new { email, purpose });

            // Check if account is locked out from too many resend attempts
            if (user.OTPResendLockedUntil.HasValue && user.OTPResendLockedUntil > DateTime.UtcNow)
            {
                TempData["Error"] = $"Account locked. Try again after {user.OTPResendLockedUntil.Value:HH:mm} UTC.";
                return RedirectToAction("VerifyOTP", new { email, purpose });
            }

            // Enforce 60-second cooldown
            if (user.OTPSentAt.HasValue && (DateTime.UtcNow - user.OTPSentAt.Value).TotalSeconds < 60)
            {
                var wait = 60 - (int)(DateTime.UtcNow - user.OTPSentAt.Value).TotalSeconds;
                TempData["Error"] = $"Please wait {wait} more second(s) before requesting a new OTP.";
                return RedirectToAction("VerifyOTP", new { email, purpose });
            }

            // Check max resend attempts (3 allowed)
            if (user.OTPResendCount >= 3)
            {
                user.OTPResendLockedUntil = DateTime.UtcNow.AddHours(1);
                user.OTP = null;
                user.OTPExpiration = null;
                await _userManager.UpdateAsync(user);
                TempData["Error"] = "Too many resend attempts. Account locked for 1 hour.";
                return RedirectToAction("VerifyOTP", new { email, purpose });
            }

            // Generate new OTP — old one is automatically invalidated
            var otp = _otpService.GenerateOTP();
            user.OTP = otp;
            user.OTPExpiration = DateTime.UtcNow.AddMinutes(10);
            user.OTPSentAt = DateTime.UtcNow;
            user.OTPResendCount++;
            await _userManager.UpdateAsync(user);

            _ = _emailService.SendOTPAsync(email, otp, purpose);

            var remaining = 3 - user.OTPResendCount;
            TempData["Success"] = remaining > 0
                ? $"New OTP sent. {remaining} resend attempt(s) remaining."
                : "New OTP sent. This was your last resend attempt.";

            return RedirectToAction("VerifyOTP", new { email, purpose });
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(model);

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    ModelState.AddModelError(nameof(model.Email), "Email not found.");
                    return View(model);
                }

                var otp = _otpService.GenerateOTP();
                user.OTP = otp;
                user.OTPExpiration = DateTime.UtcNow.AddMinutes(10);
                user.OTPSentAt = DateTime.UtcNow;
                user.OTPResendCount = 0;
                user.OTPResendLockedUntil = null;
                await _userManager.UpdateAsync(user);

                try
                {
                    await _emailService.SendOTPAsync(model.Email, otp);
                }
                catch
                {
                    ModelState.AddModelError(string.Empty, "Error sending OTP. Please try again.");
                    return View(model);
                }

                return RedirectToAction("VerifyOTP", new { email = model.Email, purpose = "PasswordReset" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ForgotPassword Error: {ex.Message}");
                ModelState.AddModelError(string.Empty, "An error occurred. Please try again.");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            var email = HttpContext.Session.GetString("ResetEmail");
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("ForgotPassword");

            var model = new ResetPasswordViewModel { Email = email };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(model);

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "User not found.");
                    return View(model);
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

                if (result.Succeeded)
                {
                    user.OTP = null;
                    user.OTPExpiration = null;
                    await _userManager.UpdateAsync(user);

                    HttpContext.Session.Remove("ResetEmail");
                    TempData["Success"] = "Password reset successfully. You can now login.";
                    return RedirectToAction("Welcome");
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResetPassword Error: {ex.Message}");
                ModelState.AddModelError(string.Empty, "An error occurred. Please try again.");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(model);

                System.Diagnostics.Debug.WriteLine($"[LOGIN] Attempting login for username: {model.Username}");

                var user = await _userManager.FindByNameAsync(model.Username);
                if (user == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[LOGIN] User not found: {model.Username}");
                    ModelState.AddModelError(string.Empty, "Invalid username or password.");
                    return View(model);
                }

                System.Diagnostics.Debug.WriteLine($"[LOGIN] User found: {user.UserName}, EmailVerified: {user.EmailVerified}");

                // Check if email is verified
                if (!user.EmailVerified)
                {
                    System.Diagnostics.Debug.WriteLine($"[LOGIN] Email not verified for user: {user.UserName}");
                    ModelState.AddModelError(string.Empty, "Please verify your email first.");
                    return View(model);
                }

                // Check if account is currently locked out
                if (await _userManager.IsLockedOutAsync(user))
                {
                    System.Diagnostics.Debug.WriteLine($"[LOGIN] Account is locked out for user: {user.UserName}");
                    var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                    if (lockoutEnd.HasValue)
                    {
                        var timeRemaining = lockoutEnd.Value.Subtract(DateTimeOffset.UtcNow);
                        if (timeRemaining > TimeSpan.Zero)
                        {
                            ModelState.AddModelError(string.Empty, "Your account is temporarily locked due to multiple failed login attempts. Please try again after 1 hour.");
                            return View(model);
                        }
                    }
                }

                // Sign in with lockout enabled
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Attempting password sign in for: {user.UserName}");
                var result = await _signInManager.PasswordSignInAsync(
                    user,
                    model.Password,
                    isPersistent: true,
                    lockoutOnFailure: true); // Enable lockout on failure

                System.Diagnostics.Debug.WriteLine($"[LOGIN] Sign in result: Succeeded={result.Succeeded}, IsLockedOut={result.IsLockedOut}, RequiresTwoFactor={result.RequiresTwoFactor}");

                if (result.Succeeded)
                {
                    System.Diagnostics.Debug.WriteLine($"[LOGIN] Login successful for: {user.UserName}");
                    
                    // Reset the failed access count on successful login
                    await _userManager.ResetAccessFailedCountAsync(user);
                    
                    // Update last login time
                    user.LastLoginAt = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                    
                    // Check if user requires OTP on login
                    try
                    {
                        var notificationPref = await _context.NotificationPreferences
                            .FirstOrDefaultAsync(np => np.UserId == user.Id);
                        
                        // If notification preference doesn't exist, create default one
                        if (notificationPref == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[LOGIN] NotificationPreference not found for user {user.Id}, creating default");
                            notificationPref = new NotificationPreference
                            {
                                UserId = user.Id,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            _context.NotificationPreferences.Add(notificationPref);
                            await _context.SaveChangesAsync();
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[LOGIN] Notification pref found - RequireOTPOnEveryLogin: {notificationPref.RequireOTPOnEveryLogin}");
                        
                        if (notificationPref.RequireOTPOnEveryLogin)
                        {
                            System.Diagnostics.Debug.WriteLine($"[LOGIN] RequireOTPOnEveryLogin enabled for: {user.UserName}");
                            
                            // Generate OTP
                            var otp = _otpService.GenerateOTP();
                            user.OTP = otp;
                            user.OTPExpiration = DateTime.UtcNow.AddMinutes(10);
                            user.OTPSentAt = DateTime.UtcNow;
                            user.OTPResendCount = 0;
                            user.OTPResendLockedUntil = null;
                            await _userManager.UpdateAsync(user);

                            System.Diagnostics.Debug.WriteLine($"[LOGIN] OTP generated and saved for: {user.UserName}");
                            
                            // Send OTP email
                            try
                            {
                                _ = _emailService.SendOTPAsync(user.Email, otp);
                                System.Diagnostics.Debug.WriteLine($"[LOGIN] OTP email sent to: {user.Email}");
                            }
                            catch (Exception emailEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[LOGIN] Error sending OTP email: {emailEx.Message}");
                                // Continue - OTP can be sent later
                            }
                            
                            // Sign out and redirect to OTP verification
                            await _signInManager.SignOutAsync();
                            System.Diagnostics.Debug.WriteLine($"[LOGIN] User signed out - redirecting to OTP verification");
                            return RedirectToAction("VerifyOTP", new { email = user.Email, purpose = "Login" });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LOGIN] Error checking OTP requirement: {ex.Message}");
                        // Continue with login - OTP check failed
                    }
                    
                    // Send login notification email if user has enabled it (only if OTP not required)
                    try
                    {
                        var notificationPref = await _context.NotificationPreferences
                            .FirstOrDefaultAsync(np => np.UserId == user.Id);
                        
                        if (notificationPref != null && notificationPref.EmailOnSuccessfulLogin)
                        {
                            var istZone = TimeZoneInfo.FindSystemTimeZoneById(
                                OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
                            var loginTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone)
                                .ToString("MMMM dd, yyyy 'at' hh:mm tt 'IST'");
                            var subject = "Login Alert - InkVault";
                            var htmlBody = $@"
                                <html>
                                <body style='font-family: Arial, sans-serif;'>
                                    <h2 style='color: #667eea;'>Login Alert</h2>
                                    <p>Hello <strong>{user.FirstName}</strong>,</p>
                                    <p>Your InkVault account was successfully logged in.</p>
                                    <div style='background: #f5f5f5; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                                        <p><strong>Login Details:</strong></p>
                                        <p>Username: <strong>{user.UserName}</strong></p>
                                        <p>Email: <strong>{user.Email}</strong></p>
                                        <p>Time: <strong>{loginTime}</strong></p>
                                    </div>
                                    <p>If this wasn't you, please secure your account immediately.</p>
                                    <p>Best regards,<br/>InkVault Team</p>
                                </body>
                                </html>";
                            
                            // Send email asynchronously (non-blocking)
                            _ = _emailService.SendEmailAsync(user.Email, subject, htmlBody);
                            System.Diagnostics.Debug.WriteLine($"[LOGIN] Login email queued for: {user.Email}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LOGIN] Error sending login email: {ex.Message}");
                        // Non-blocking - don't fail login if email fails
                    }
                    
                    return RedirectToAction("Index", "Home");
                }

                if (result.IsLockedOut)
                {
                    System.Diagnostics.Debug.WriteLine($"[LOGIN] Account locked out for: {user.UserName}");
                    
                    // Send security alert email for lockout
                    try
                    {
                        var userAgent = Request.Headers["User-Agent"].ToString();
                        var browserName = BrowserDetectionService.GetBrowserName(userAgent);
                        await SendLockoutSecurityAlertEmail(user, browserName);
                        System.Diagnostics.Debug.WriteLine($"[LOGIN] Lockout security alert email sent to: {user.Email}");
                    }
                    catch (Exception emailEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LOGIN] Error sending lockout security alert email: {emailEx.Message}");
                        // Non-blocking - don't interrupt login flow
                    }
                    
                    ModelState.AddModelError(string.Empty, "Your account is temporarily locked due to multiple failed login attempts. Please try again after 1 hour.");
                    return View(model);
                }

                if (result.RequiresTwoFactor)
                {
                    System.Diagnostics.Debug.WriteLine($"[LOGIN] Two-factor required for: {user.UserName}");
                    return RedirectToAction("LoginWith2fa", new { rememberMe = true });
                }

                System.Diagnostics.Debug.WriteLine($"[LOGIN] Invalid password for: {user.UserName}");

                // Check if user is now locked out after this failed attempt
                if (await _userManager.IsLockedOutAsync(user))
                {
                    System.Diagnostics.Debug.WriteLine($"[LOGIN] Account became locked out after failed attempt for: {user.UserName}");
                    
                    // Send security alert email for lockout
                    try
                    {
                        var userAgent = Request.Headers["User-Agent"].ToString();
                        var browserName = BrowserDetectionService.GetBrowserName(userAgent);
                        await SendLockoutSecurityAlertEmail(user, browserName);
                        System.Diagnostics.Debug.WriteLine($"[LOGIN] Lockout security alert email sent to: {user.Email}");
                    }
                    catch (Exception emailEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LOGIN] Error sending lockout security alert email: {emailEx.Message}");
                        // Non-blocking
                    }
                    
                    ModelState.AddModelError(string.Empty, "Your account is temporarily locked due to multiple failed login attempts. Please try again after 1 hour.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid username or password.");
                }
                
                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Exception: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Stack Trace: {ex.StackTrace}");
                ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Mark first login as completed when user logs out
            var userId = _userManager.GetUserId(User);
            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null && !user.HasCompletedFirstLogin)
                {
                    user.HasCompletedFirstLogin = true;
                    await _userManager.UpdateAsync(user);
                }
            }

            await _signInManager.SignOutAsync();
            return RedirectToAction("Welcome");
        }

        private async Task SendLockoutSecurityAlertEmail(ApplicationUser user, string browserName)
        {
            var lockoutTime = DateTime.UtcNow.ToString("F");
            var subject = "Security Alert: Account Temporarily Locked - InkVault";
            
            var htmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 8px 8px 0 0; text-align: center;'>
                            <h2 style='margin: 0; font-size: 24px;'>Security Alert</h2>
                            <p style='margin: 5px 0 0 0; opacity: 0.9;'>Your InkVault account has been temporarily locked</p>
                        </div>

                        <div style='background: #ffffff; padding: 30px; border: 1px solid #e0e0e0; border-top: none; border-radius: 0 0 8px 8px;'>
                            <p>Hello <strong>{user.FirstName}</strong>,</p>

                            <p>Your InkVault account has been temporarily locked due to multiple failed login attempts detected from the following source:</p>

                            <div style='background: #f8f9fa; border-left: 4px solid #ff6b6b; padding: 20px; margin: 20px 0; border-radius: 4px;'>
                                <p style='margin: 0; font-weight: bold; color: #dc3545;'><strong>Security Details:</strong></p>
                                <p style='margin: 10px 0 5px 0;'><strong>Email:</strong> {user.Email}</p>
                                <p style='margin: 5px 0;'><strong>Date & Time:</strong> {lockoutTime}</p>
                                <p style='margin: 5px 0;'><strong>Browser Used:</strong> {browserName}</p>
                                <p style='margin: 5px 0 0 0;'><strong>Lockout Duration:</strong> 1 hour</p>
                            </div>

                            <div style='background: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 4px; margin: 20px 0;'>
                                <p style='margin: 0; color: #856404;'><strong>Important:</strong> If this activity was not initiated by you, please change your password immediately after the lockout period expires.</p>
                            </div>

                            <p>To protect your account security:</p>
                            <ul style='color: #555;'>
                                <li>Wait for the 1-hour lockout period to expire before attempting to log in again</li>
                                <li>Ensure you're using the correct username and password</li>
                                <li>Consider changing your password if you suspect unauthorized access</li>
                                <li>Enable two-factor authentication for enhanced security</li>
                            </ul>

                            <div style='background: #e3f2fd; border-left: 4px solid #2196f3; padding: 15px; margin: 20px 0;'>
                                <p style='margin: 0; color: #1565c0;'><strong>Need Help?</strong> If you continue to experience issues, please contact our support team.</p>
                            </div>

                            <p>Best regards,<br/>
                            <strong>InkVault Security Team</strong></p>
                        </div>

                        <div style='text-align: center; padding: 15px; font-size: 12px; color: #666;'>
                            <p style='margin: 0;'>This is an automated security notification from InkVault.</p>
                        </div>
                    </div>
                </body>
                </html>";

            await _emailService.SendEmailAsync(user.Email, subject, htmlBody);
        }
    }
}
