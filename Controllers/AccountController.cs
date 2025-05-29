using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicCatalogWebApplication.Models;
using MusicCatalogWebApplication.Services;

namespace MusicCatalogWebApplication.Controllers
{
    public class AccountController : Controller
    {
        private readonly AuthService _authService;

        public AccountController(AuthService authService)
        {
            _authService = authService;
        }

        // GET: Account/Login
        public IActionResult Login(string returnUrl = "/")
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _authService.AuthenticateAsync(model.Login, model.Password);
            if (user == null)
            {
                ModelState.AddModelError("", "Неверный логин или пароль.");
                return View(model);
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Login),
                new Claim(ClaimTypes.NameIdentifier, user.ID.ToString()),
                new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // Проверка, новый ли пользователь
            bool isNewUser = user.LastLoginDate == null ||
                             (user.RegistrationDate > DateTime.Now.AddHours(-24));
            if (isNewUser)
            {
                TempData["WelcomeMessage"] = $"Добро пожаловать, {user.Login}! Это приложение для управления музыкальным каталогом. Если хотите лучше узнать, вот ссылка на <a href='/tutorial'>туториал</a>.";
            }
            else
            {
                TempData["WelcomeMessage"] = $"Добро пожаловать, {user.Login}!";
            }

            return LocalRedirect(model.ReturnUrl ?? "/");
        }

        // GET: Account/Register
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        // POST: Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Пароли не совпадают.");
                return View(model);
            }

            try
            {
                var user = await _authService.RegisterAsync(model.Login, model.Email, model.Password);
                TempData["SuccessMessage"] = $"Поздравляю, {model.Login}! Вы успешно зарегистрировались";
                return RedirectToAction("Login");
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Логин или email уже заняты.");
                return View(model);
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        // GET: Account/GuestLogin
        [HttpGet]
        public async Task<IActionResult> GuestLogin()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "Guest"),
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Guest")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToAction("Index", "Tracks");
        }

        // POST: Account/CheckPasswordStrength
        [HttpPost]
        public IActionResult CheckPasswordStrength([FromBody] PasswordInput input)
        {
            if (string.IsNullOrEmpty(input.Password))
            {
                return Json(new { strength = "VeryWeak" });
            }

            var strength = PasswordHelper.CheckPasswordStrength(input.Password);
            return Json(new { strength = strength.ToString() });
        }

        // POST: Account/GenerateRandomPassword
        [HttpPost]
        public IActionResult GenerateRandomPassword()
        {
            var password = PasswordHelper.GenerateRandomPassword();
            return Json(new { password });
        }

        // POST: Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // GET: Account/AccessDenied
        public IActionResult AccessDenied()
        {
            return View();
        }
    }

    public class PasswordInput
    {
        public string Password { get; set; }
    }
}