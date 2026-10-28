using MyEcomWeb.Data;
using MyEcomWeb.Models.Entities;
using MyEcomWeb.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MyEcomWeb.Controllers
{
    public class AcccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        public AcccountController(ApplicationDbContext context)
        {
             _context = context;
        }

        public IActionResult Login() => View();
        [HttpPost]

        public async Task<IActionResult> Login(string email, string password, bool rememberMe = false)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Invalid email or password");
                return View();
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsynce();

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("UserRole", user.Role.ToString());

            TempData["Success"] = $"Welcome Back, {user.Username}!";
            return RedirectToAction("Index", "Home");
        }

        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(string username, string email, string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            {
                ModelState.AddModelError("username", "Username must be at least 3 characters");
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match");
                return View();
            }

            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                ModelState.AddModelError("email", "Email already registered");
                return View();
            }

            if (await _context.Users.AnyAsync(u => u.Username == username))
            {
                ModelState.AddModelError("username", "Username already taken");
                return View();
            }

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRole.Customer,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _context.ShoppingCarts.Add(new ShoppingCart{UserId = user.Id});
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("Username", user.Username);

            TempData["Success"] = $"Welcome to shopnow, {username}";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if(userId == null)
            {
                return RedirectToAction("Login");

                var user = await _context.Users
                    .Include(u => u.Orders)
                        .ThenInclude(o => o.OderItems)
                    .Include(u => u.Reviews)
                    .FirstOrDefultAsync(u => u.Id == userId);
                
                return View(user);
            }
        }
    }
}