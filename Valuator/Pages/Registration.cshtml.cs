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
            if (!IsInputValid(login, password))
            {
                return Page();
            }

            if (await UserExists(login))
            {
                ModelState.AddModelError("", "Этот логин уже занят.");
                return Page();
            }

            await CreateUser(login, password);

            return RedirectToPage("/Login");
        }

        private bool IsInputValid(string login, string password)
        {
            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Логин и пароль обязательны");
                return false;
            }
            return true;
        }

        private async Task<bool> UserExists(string login)
        {
            return await _db.KeyExistsAsync(GetUserKey(login));
        }

        private async Task CreateUser(string login, string password)
        {
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var newUser = new User
            {
                Login = login,
                PasswordHash = passwordHash
            };

            string json = JsonSerializer.Serialize(newUser);
            await _db.StringSetAsync(GetUserKey(login), json);
        }

        private string GetUserKey(string login) => $"USER:{login}";
    }
}
