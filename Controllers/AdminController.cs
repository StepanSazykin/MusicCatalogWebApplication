using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MusicCatalogWebApplication.Context;
using MusicCatalogWebApplication.Models;
using MusicCatalogWebApplication.Services;

namespace MusicCatalogWebApplication.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : BaseController
    {
        private readonly AuthService _authService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, AuthService authService, ILogger<AdminController> logger)
            : base(context)
        {
            _authService = authService;
            _logger = logger;
        }

        // GET: Admin
        public async Task<IActionResult> Index(string searchString, bool? isAdmin)
        {
            var users = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                users = users.Where(u => u.Login.Contains(searchString) || (u.Email != null && u.Email.Contains(searchString)));
            }

            if (isAdmin.HasValue)
            {
                users = users.Where(u => u.IsAdmin == isAdmin.Value);
            }

            var model = await users
                .Select(u => new UserViewModel
                {
                    ID = u.ID,
                    Login = u.Login,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    RegistrationDate = u.RegistrationDate,
                    LastLoginDate = u.LastLoginDate,
                    IsActive = u.IsActive
                })
                .ToListAsync();

            ViewData["SearchString"] = searchString;
            ViewData["IsAdminFilter"] = isAdmin;
            return View(model);
        }

        // POST: Admin/ToggleAdminStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAdminStatus(int id)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                TempData["UserErrorMessage"] = "Пользователь не найден";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _authService.ToggleAdminStatusAsync(id, currentUserId);

                // Обновляем данные пользователя
                await _context.Entry(user).ReloadAsync();

                _logger.LogInformation("Администратор {AdminId} изменил статус пользователя {UserId} на IsAdmin={IsAdmin}",
                    currentUserId, id, user.IsAdmin);

                // Обновление Claims для пользователя, если он вошел в систему
                var targetUserPrincipal = HttpContext.User;
                if (targetUserPrincipal.Identity.IsAuthenticated && int.Parse(targetUserPrincipal.FindFirst(ClaimTypes.NameIdentifier).Value) == id)
                {
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.Name, user.Login),
                        new Claim(ClaimTypes.NameIdentifier, user.ID.ToString()),
                        new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
                    };
                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                }

                TempData["UserSuccessMessage"] = user.IsAdmin
                    ? $"Пользователь {user.Login} успешно назначен администратором."
                    : $"Пользователь {user.Login} успешно лишен статуса администратора.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["UserErrorMessage"] = ex.Message;
            }
            catch (ArgumentException ex)
            {
                TempData["UserErrorMessage"] = ex.Message;
            }
            catch (DbUpdateException)
            {
                TempData["UserErrorMessage"] = "Произошла ошибка при обновлении статуса пользователя.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/ToggleActiveStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActiveStatus(int id)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var user = await _context.Users.FindAsync(id);
            try
            {
                await _authService.ToggleActiveStatusAsync(id, currentUserId);
                _logger.LogInformation("Администратор {AdminId} изменил статус активности пользователя {UserId} на IsActive={IsActive}", currentUserId, id, user.IsActive);

                // Если пользователь заблокирован, завершаем его сессию
                if (!user.IsActive && HttpContext.User.Identity.IsAuthenticated && int.Parse(HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value) == id)
                {
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }

                TempData["UserSuccessMessage"] = user.IsActive
                    ? $"Пользователь {user.Login} успешно разблокирован."
                    : $"Пользователь {user.Login} успешно заблокирован.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["UserErrorMessage"] = user.IsAdmin
                    ? ex.Message
                    : "Не удалось назначить администратора. " + ex.Message;
            }
            catch (ArgumentException ex)
            {
                TempData["UserErrorMessage"] = ex.Message;
            }
            catch (DbUpdateException)
            {
                TempData["UserErrorMessage"] = "Произошла ошибка при обновлении статуса активности пользователя.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Proposals
        public async Task<IActionResult> Proposals(string searchString, string status)
        {
            var proposals = _context.EditProposals
                .Include(p => p.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                proposals = proposals.Where(p => p.ProposedChange.Contains(searchString) || p.TableName.Contains(searchString) || p.User.Login.Contains(searchString));
            }

            if (!string.IsNullOrEmpty(status))
            {
                proposals = proposals.Where(p => p.Status == status);
            }

            var model = await proposals
                .Select(p => new EditProposalViewModel
                {
                    ID = p.ID,
                    UserLogin = p.User.Login,
                    TableName = p.TableName,
                    Record_ID = p.Record_ID,
                    ProposedChange = p.ProposedChange,
                    Status = p.Status,
                    CreatedDate = p.CreatedDate
                })
                .ToListAsync();

            ViewData["SearchString"] = searchString;
            ViewData["StatusFilter"] = status;
            ViewData["StatusOptions"] = new SelectList(new[]
            {
                new { Value = "", Text = "Все статусы" },
                new { Value = "pending", Text = "Ожидает" },
                new { Value = "approved", Text = "Одобрено" },
                new { Value = "rejected", Text = "Отклонено" }
            }, "Value", "Text", status);

            return View(model);
        }

        // POST: Admin/ApproveProposal/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveProposal(int id)
        {
            var proposal = await _context.EditProposals.FindAsync(id);
            if (proposal == null)
            {
                TempData["ProposalErrorMessage"] = "Предложение не найдено.";
                return RedirectToAction(nameof(Proposals));
            }

            try
            {
                proposal.Status = "approved"; // Используем строчное значение
                await _context.SaveChangesAsync();
                _logger.LogInformation("Администратор {AdminId} одобрил предложение {ProposalId}", User.FindFirst(ClaimTypes.NameIdentifier).Value, id);
                TempData["ProposalSuccessMessage"] = "Предложение успешно одобрено.";
            }
            catch (DbUpdateException)
            {
                TempData["ProposalErrorMessage"] = "Произошла ошибка при одобрении предложения.";
            }

            return RedirectToAction(nameof(Proposals));
        }

        // POST: Admin/RejectProposal/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectProposal(int id)
        {
            var proposal = await _context.EditProposals.FindAsync(id);
            if (proposal == null)
            {
                TempData["ProposalErrorMessage"] = "Предложение не найдено.";
                return RedirectToAction(nameof(Proposals));
            }

            try
            {
                proposal.Status = "rejected"; // Используем строчное значение
                await _context.SaveChangesAsync();
                _logger.LogInformation("Администратор {AdminId} отклонил предложение {ProposalId}", User.FindFirst(ClaimTypes.NameIdentifier).Value, id);
                TempData["ProposalSuccessMessage"] = "Предложение успешно отклонено.";
            }
            catch (DbUpdateException)
            {
                TempData["ProposalErrorMessage"] = "Произошла ошибка при отклонении предложения.";
            }

            return RedirectToAction(nameof(Proposals));
        }

        // GET: Admin/AddToCatalog
        public async Task<IActionResult> AddToCatalog()
        {
            var model = new AddToCatalogViewModel
            {
                Artists = new SelectList(await _context.Artists.Select(a => new { a.ID, a.Name }).ToListAsync(), "ID", "Name"),
                Genres = new SelectList(await _context.Genres.Select(g => new { g.ID, g.Name }).ToListAsync(), "ID", "Name"),
                Albums = new SelectList(await _context.Albums.Select(a => new { a.ID, a.Title }).ToListAsync(), "ID", "Title")
            };
            return View(model);
        }

        // POST: Admin/AddToCatalog
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCatalog(AddToCatalogViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Artists = new SelectList(await _context.Artists.Select(a => new { a.ID, a.Name }).ToListAsync(), "ID", "Name");
                model.Genres = new SelectList(await _context.Genres.Select(g => new { g.ID, g.Name }).ToListAsync(), "ID", "Name");
                model.Albums = new SelectList(await _context.Albums.Select(a => new { a.ID, a.Title }).ToListAsync(), "ID", "Title");
                return View(model);
            }

            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            try
            {
                switch (model.EntityType)
                {
                    case "Artist":
                        if (string.IsNullOrEmpty(model.ArtistName))
                        {
                            ModelState.AddModelError("ArtistName", "Имя артиста обязательно.");
                            model.Artists = new SelectList(await _context.Artists.Select(a => new { a.ID, a.Name }).ToListAsync(), "ID", "Name");
                            model.Genres = new SelectList(await _context.Genres.Select(g => new { g.ID, g.Name }).ToListAsync(), "ID", "Name");
                            model.Albums = new SelectList(await _context.Albums.Select(a => new { a.ID, a.Title }).ToListAsync(), "ID", "Title");
                            return View(model);
                        }
                        var artist = new Artist
                        {
                            Name = model.ArtistName,
                            Owner_ID = adminId,
                            IsPublic = true
                        };
                        _context.Artists.Add(artist);
                        break;

                    case "Album":
                        if (string.IsNullOrEmpty(model.AlbumTitle) || !model.ArtistId.HasValue || !model.GenreId.HasValue)
                        {
                            ModelState.AddModelError("", "Все поля для альбома обязательны.");
                            model.Artists = new SelectList(await _context.Artists.Select(a => new { a.ID, a.Name }).ToListAsync(), "ID", "Name");
                            model.Genres = new SelectList(await _context.Genres.Select(g => new { g.ID, g.Name }).ToListAsync(), "ID", "Name");
                            model.Albums = new SelectList(await _context.Albums.Select(a => new { a.ID, a.Title }).ToListAsync(), "ID", "Title");
                            return View(model);
                        }
                        var album = new Album
                        {
                            Title = model.AlbumTitle,
                            Artist_ID = model.ArtistId.Value,
                            Genre_ID = model.GenreId.Value,
                            ReleaseDate = model.ReleaseDate,
                            Owner_ID = adminId,
                            IsPublic = true
                        };
                        _context.Albums.Add(album);
                        break;

                    case "Track":
                        if (string.IsNullOrEmpty(model.TrackTitle) || !model.AlbumId.HasValue)
                        {
                            ModelState.AddModelError("", "Все поля для трека обязательны.");
                            model.Artists = new SelectList(await _context.Artists.Select(a => new { a.ID, a.Name }).ToListAsync(), "ID", "Name");
                            model.Genres = new SelectList(await _context.Genres.Select(g => new { g.ID, g.Name }).ToListAsync(), "ID", "Name");
                            model.Albums = new SelectList(await _context.Albums.Select(a => new { a.ID, a.Title }).ToListAsync(), "ID", "Title");
                            return View(model);
                        }
                        var track = new Track
                        {
                            Title = model.TrackTitle,
                            Album_ID = model.AlbumId.Value,
                            Duration = (short)model.Duration,
                            IsPublic = model.IsPublic,
                            Owner_ID = adminId
                        };
                        _context.Tracks.Add(track);
                        break;

                    default:
                        ModelState.AddModelError("EntityType", "Неверный тип сущности.");
                        model.Artists = new SelectList(await _context.Artists.Select(a => new { a.ID, a.Name }).ToListAsync(), "ID", "Name");
                        model.Genres = new SelectList(await _context.Genres.Select(g => new { g.ID, g.Name }).ToListAsync(), "ID", "Name");
                        model.Albums = new SelectList(await _context.Albums.Select(a => new { a.ID, a.Title }).ToListAsync(), "ID", "Title");
                        return View(model);
                }

                await _context.SaveChangesAsync();
                TempData["CatalogSuccessMessage"] = $"Сущность '{model.EntityType}' успешно добавлена в каталог.";
                return RedirectToAction(nameof(AddToCatalog));
            }
            catch (DbUpdateException)
            {
                TempData["CatalogErrorMessage"] = "Произошла ошибка при добавлении в каталог.";
                model.Artists = new SelectList(await _context.Artists.Select(a => new { a.ID, a.Name }).ToListAsync(), "ID", "Name");
                model.Genres = new SelectList(await _context.Genres.Select(g => new { g.ID, g.Name }).ToListAsync(), "ID", "Name");
                model.Albums = new SelectList(await _context.Albums.Select(a => new { a.ID, a.Title }).ToListAsync(), "ID", "Title");
                return View(model);
            }
        }
    }
}