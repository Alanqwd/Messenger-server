using Messenger_server.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Messenger_server.Filters
{
    public class SessionAuthFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {

            if (context.HttpContext.Request.Method == "OPTIONS")
            {
                await next();
                return;
            }

            
            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();

            if (controller == "Auth" && (action == "Login" || action == "Register"))
            {
                await next();
                return;
            }

        
            var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            var sessionHeader = context.HttpContext.Request.Headers["X-Session-Token"].FirstOrDefault();

            string? token = null;

            if (!string.IsNullOrEmpty(authHeader))
            {
                token = authHeader.StartsWith("Bearer ") ? authHeader.Substring(7) : authHeader;
            }

            if (string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(sessionHeader))
            {
                token = sessionHeader;
            }

            if (string.IsNullOrEmpty(token))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "No session token provided" });
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.SessionToken == token);

            if (user == null)
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Invalid or expired session. Please log in again." });
                return;
            }

            context.HttpContext.Items["UserId"] = user.Id;

            await next();
        }
    }
}