using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicCatalogWebApplication.Context;
using MusicCatalogWebApplication.Models;
using MusicCatalogWebApplication.Services;
using System.Security.Claims;

namespace MusicCatalogWebApplication.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuthService _authService;

        public ProfileController(ApplicationDbContext context, AuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        // GET: Profile
        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Guest"))
            {
                TempData["ErrorMessage"] = "Гостям запрещен доступ к профилю.";
                return Forbid();
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.ID == userId);
            if (user == null)
            {
                return NotFound();
            }

            var model = new ProfileViewModel
            {
                ID = user.ID,
                Login = user.Login,
                Email = user.Email
            };

            return View(model);
        }

        // POST: Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ProfileViewModel model)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["ErrorMessage"] = "Гостям запрещен доступ к профилю.";
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.ID == userId);
            if (user == null)
            {
                return NotFound();
            }

            // Проверяем текущий пароль
            if (!PasswordHelper.VerifyPassword(model.CurrentPassword, user.Password))
            {
                ModelState.AddModelError("CurrentPassword", "Неверный текущий пароль.");
                return View(model);
            }

            try
            {
                // Проверяем уникальность логина
                if (model.Login != user.Login && await _context.Users.AnyAsync(u => u.Login == model.Login))
                {
                    ModelState.AddModelError("Login", "Этот логин уже занят.");
                    return View(model);
                }

                // Проверяем уникальность email
                if (model.Email != user.Email && !string.IsNullOrEmpty(model.Email) && await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Этот email уже занят.");
                    return View(model);
                }

                // Обновляем данные
                user.Login = model.Login;
                user.Email = model.Email;

                // Обновляем пароль, если указан новый
                if (!string.IsNullOrEmpty(model.NewPassword))
                {
                    user.Password = PasswordHelper.HashPassword(model.NewPassword);
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Профиль успешно обновлен.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Произошла ошибка при сохранении изменений.");
                return View(model);
            }
        }
    }
}
