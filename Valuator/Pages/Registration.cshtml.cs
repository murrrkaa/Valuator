using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using System.Text.Json;

namespace Valuator.Pages
{
    public class Registration : PageModel
    {
        private readonly IDatabase _db;

        public Registration(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync(string login, string password)
        {
            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Логин и пароль обязательны");
                return Page();
            }

            string userKey = $"USER:{login}";
            if (await _db.KeyExistsAsync(userKey))
            {
                ModelState.AddModelError("", "Этот логин уже занят.");
                return Page();
            }
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            var newUser = new User
            {
                Login = login,
                PasswordHash = passwordHash
            };

            string json = JsonSerializer.Serialize(newUser);
            await _db.StringSetAsync(userKey, json);

            return RedirectToPage("/Login");
        }
    }
}
