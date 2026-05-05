using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;

namespace Valuator.Pages
{
    public class Login : PageModel
    {
        private readonly IDatabase _db;

        public Login(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        public async Task<IActionResult> OnPostAsync(string login, string password)
        {
            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Введите логин и пароль");
                return Page();
            }

            var userJson = await _db.StringGetAsync($"USER:{login}");
            if (userJson.IsNull)
            {
                ModelState.AddModelError("", "Пользователь не найден");
                return Page();
            }

            var user = JsonSerializer.Deserialize<User>(userJson);

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Неверный пароль");
                return Page();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Login)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity)
            );

            return RedirectToPage("/Index");
        }
    }
}