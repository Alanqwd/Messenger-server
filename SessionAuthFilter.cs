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
            // ✅ Пропускаем OPTIONS запросы (preflight CORS)
            if (context.HttpContext.Request.Method == "OPTIONS")
            {
                await next();
                return;
            }

            // Пропускаем авторизацию для логина и регистрации
            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();

            if (controller == "Auth" && (action == "Login" || action == "Register"))
            {
                await next();
                return;
            }

            // Получаем токен из заголовка
            var token = context.HttpContext.Request.Headers["Authorization"]
                .FirstOrDefault()?.Split(" ").Last();

            if (string.IsNullOrEmpty(token))
            {
                context.Result = new UnauthorizedObjectResult("No session token");
                return;
            }

            // Получаем DbContext
            var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

            // Проверяем, есть ли пользователь с таким токеном
            var userExists = await dbContext.Users.AnyAsync(u => u.SessionToken == token);

            if (!userExists)
            {
                context.Result = new UnauthorizedObjectResult("Session expired or logged in elsewhere");
                return;
            }

            await next();
        }
    }
}