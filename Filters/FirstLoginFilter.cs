using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using InkVault.Models;

namespace InkVault.Filters
{
    public class FirstLoginFilter : IAsyncActionFilter
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public FirstLoginFilter(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Check if user is authenticated
            if (context.HttpContext.User?.Identity?.IsAuthenticated ?? false)
            {
                try
                {
                    var userId = _userManager.GetUserId(context.HttpContext.User);
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var user = await _userManager.FindByIdAsync(userId);
                        if (user != null && context.Controller is Controller controller)
                        {
                            controller.ViewData["IsFirstLogin"] = !user.HasCompletedFirstLogin;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Gracefully continue if DB schema is temporarily out of sync (e.g. pending migration)
                    Console.WriteLine($"[FirstLoginFilter] Non-fatal error: {ex.Message}");
                }
            }

            await next();
        }
    }
}
