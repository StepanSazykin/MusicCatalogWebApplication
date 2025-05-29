using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MusicCatalogWebApplication.Context;
using MusicCatalogWebApplication.Models;

namespace MusicCatalogWebApplication.Controllers
{
    [Authorize]
    public class ArtistsController : BaseController
    {
        public ArtistsController(ApplicationDbContext context) : base(context) { }

        // GET: Artists
        [AllowAnonymous]
        public async Task<IActionResult> Index(string searchString, string country, int? creationYearFrom, int? creationYearTo, int page = 1)
        {
            int pageSize = 10;
            int? userId = null;
            if (User.Identity.IsAuthenticated && !User.IsInRole("Guest"))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
                if (user != null)
                {
                    userId = user.ID;
                }
            }
            var isAdmin = User.IsInRole("Admin");

            var artists = _context.Artists
                .Include(a => a.Owner)
                .AsQueryable();

            if (!isAdmin)
            {
                artists = userId.HasValue
                    ? artists.Where(a => a.IsPublic || a.Owner_ID == userId)
                    : artists.Where(a => a.IsPublic);
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.ToLower();
                artists = artists.Where(a => a.Name.ToLower().Contains(searchString));
            }

            if (!string.IsNullOrEmpty(country))
            {
                artists = artists.Where(a => a.Country.ToLower() == country.ToLower());
            }

            if (creationYearFrom.HasValue)
            {
                if (creationYearFrom.Value < 1900 || creationYearFrom.Value > 2025)
                {
                    ViewBag.ErrorMessage = "Год создания должен быть в диапазоне от 1900 до 2025.";
                    return View(new List<Artist>());
                }
                artists = artists.Where(a => a.CreationYear >= creationYearFrom.Value);
            }
            if (creationYearTo.HasValue)
            {
                if (creationYearTo.Value < 1900 || creationYearTo.Value > 2025)
                {
                    ViewBag.ErrorMessage = "Год создания должен быть в диапазоне от 1900 до 2025.";
                    return View(new List<Artist>());
                }
                artists = artists.Where(a => a.CreationYear <= creationYearTo.Value);
            }

            var totalItems = await artists.CountAsync();
            var artistList = await artists
                .OrderBy(a => a.Owner_ID != userId)
                .ThenBy(a => a.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (!artistList.Any())
            {
                ViewBag.NoResultsMessage = "К сожалению, по вашему запросу ничего не найдено. Попробуйте изменить параметры поиска.";
            }
            else
            {
                ViewBag.NoResultsMessage = null;
            }

            var countries = await _context.Artists
                .Where(a => a.Country != null)
                .Select(a => a.Country)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewData["Countries"] = new SelectList(countries, country);
            ViewData["SearchString"] = searchString;
            ViewData["Country"] = country;
            ViewData["CreationYearFrom"] = creationYearFrom;
            ViewData["CreationYearTo"] = creationYearTo;
            ViewData["TotalPages"] = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewData["CurrentPage"] = page;
            ViewData["CurrentUserId"] = userId ?? 0;

            return View(artistList);
        }

        // GET: Artists/Albums/5
        [AllowAnonymous]
        public async Task<IActionResult> Albums(int artistId, string returnUrl = null)
        {
            int? userId = null;
            if (User.Identity.IsAuthenticated && !User.IsInRole("Guest"))
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
                userId = user?.ID;
            }
            var isAdmin = User.IsInRole("Admin");

            var albums = isAdmin
                ? _context.Albums
                    .Where(a => a.Artist_ID == artistId)
                    .Include(a => a.Artist)
                    .Include(a => a.Genre)
                    .Include(a => a.Owner)
                : userId.HasValue
                ? _context.Albums
                    .Where(a => a.Artist_ID == artistId && (a.IsPublic || a.Owner_ID == userId))
                    .Include(a => a.Artist)
                    .Include(a => a.Genre)
                    .Include(a => a.Owner)
                : _context.Albums
                    .Where(a => a.Artist_ID == artistId && a.IsPublic)
                    .Include(a => a.Artist)
                    .Include(a => a.Genre)
                    .Include(a => a.Owner);

            var artist = await _context.Artists.FindAsync(artistId);
            if (artist == null)
            {
                return NotFound();
            }

            ViewData["CurrentUserId"] = userId ?? 0;
            ViewData["Artist"] = artist;
            ViewBag.ReturnUrl = returnUrl;
            return View(await albums.ToListAsync());
        }

        // GET: Artists/Create
        public IActionResult Create(string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                return Forbid();
            }
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: Artists/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,Name,IsPublic,Country,CreationYear")] Artist artist, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["ArtistErrorMessage"] = "Гостям запрещено создавать артистов.";
                return Forbid();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null)
            {
                TempData["ArtistErrorMessage"] = "Пользователь не найден.";
                return Forbid();
            }
            artist.Owner_ID = user.ID;

            // Предварительная проверка на уникальность имени артиста в каталоге
            if (await _context.Artists.AnyAsync(a => a.Name == artist.Name))
            {
                ModelState.AddModelError("Name", "Артист с таким названием уже существует в каталоге. Попробуйте найти его через поиск.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(artist);
                    await _context.SaveChangesAsync();

                    if (artist.IsPublic)
                    {
                        artist.IsPublic = false;
                        string changeDescription = $"Сделать артиста '{artist.Name}' публичным";
                        await CreatePublicProposalAsync("Artists", artist.ID, changeDescription, user.ID);
                        _context.Update(artist);
                        await _context.SaveChangesAsync();
                        TempData["ArtistSuccessMessage"] = $"Ваш артист '{artist.Name}' отправлен на модерацию администратором и в скором времени будет рассмотрен.";
                    }
                    else
                    {
                        TempData["ArtistSuccessMessage"] = $"Ваш артист '{artist.Name}' успешно добавлен в ваш личный каталог.";
                    }

                    return Redirect(returnUrl ?? Url.Action("Index"));
                }
                catch (DbUpdateException ex)
                {
                    // Проверяем, связана ли ошибка с уникальным ограничением
                    if (ex.InnerException?.Message.Contains("UQ_Artists_Name") == true)
                    {
                        ModelState.AddModelError("Name", "Артист с таким названием уже существует в каталоге. Попробуйте найти его через поиск.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Произошла ошибка при сохранении артиста: " + ex.InnerException?.Message ?? ex.Message);
                    }
                }
            }
            ViewBag.ReturnUrl = returnUrl;
            return View(artist);
        }

        // GET: Artists/Edit/5
        public async Task<IActionResult> Edit(int? id, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["ArtistErrorMessage"] = "Гостям запрещено редактировать артистов.";
                return Forbid();
            }

            if (id == null)
            {
                TempData["ArtistErrorMessage"] = "Артист не найден.";
                return NotFound();
            }

            var artist = await _context.Artists.FindAsync(id);
            if (artist == null)
            {
                TempData["ArtistErrorMessage"] = "Артист не найден.";
                return NotFound();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null)
            {
                TempData["ArtistErrorMessage"] = "Пользователь не найден.";
                return Forbid();
            }

            if (!User.IsInRole("Admin") && !(await CanEdit(artist.Owner_ID)))
            {
                TempData["ArtistErrorMessage"] = "Вы можете редактировать только своих артистов.";
                return Forbid();
            }

            ViewData["IsOwnerOrAdmin"] = true; // владелец или админ
            ViewBag.ReturnUrl = returnUrl;
            return View(artist);
        }

        // POST: Artists/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,Name,IsPublic,Country,CreationYear")] Artist artist, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["ArtistErrorMessage"] = "Гостям запрещено редактировать артистов.";
                return Forbid();
            }

            if (id != artist.ID)
            {
                TempData["ArtistErrorMessage"] = "Артист не найден.";
                return NotFound();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null)
            {
                TempData["ArtistErrorMessage"] = "Пользователь не найден.";
                return Forbid();
            }

            var existingArtist = await _context.Artists.FindAsync(id);
            if (existingArtist == null)
            {
                TempData["ArtistErrorMessage"] = "Артист не найден.";
                return NotFound();
            }

            if (!User.IsInRole("Admin") && existingArtist.Owner_ID != user.ID)
            {
                TempData["ArtistErrorMessage"] = "Вы можете редактировать только своих артистов.";
                return Forbid();
            }

            ViewData["IsOwnerOrAdmin"] = true;

            // Предварительная проверка на уникальность имени артиста, исключая текущий артист
            if (artist.Name != existingArtist.Name && await _context.Artists.AnyAsync(a => a.Name == artist.Name && a.ID != artist.ID))
            {
                ModelState.AddModelError("Name", "Артист с таким названием уже существует в каталоге. Попробуйте найти его через поиск.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var originalIsPublic = existingArtist.IsPublic;
                    existingArtist.Name = artist.Name;
                    existingArtist.Country = artist.Country;
                    existingArtist.CreationYear = artist.CreationYear;
                    existingArtist.IsPublic = artist.IsPublic;

                    _context.Update(existingArtist);
                    await _context.SaveChangesAsync();

                    if (artist.IsPublic && !originalIsPublic)
                    {
                        existingArtist.IsPublic = false;
                        string changeDescription = $"Сделать артиста '{existingArtist.Name}' публичным";
                        await CreatePublicProposalAsync("Artists", existingArtist.ID, changeDescription, user.ID);
                        _context.Update(existingArtist);
                        await _context.SaveChangesAsync();
                        TempData["ArtistSuccessMessage"] = $"Ваш артист '{existingArtist.Name}' отправлен на модерацию администратором и в скором времени будет рассмотрен.";
                    }
                    else
                    {
                        TempData["ArtistSuccessMessage"] = $"Артист '{existingArtist.Name}' успешно обновлён.";
                    }

                    return Redirect(returnUrl ?? Url.Action("Index"));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ArtistExists(artist.ID))
                    {
                        TempData["ArtistErrorMessage"] = "Артист не найден.";
                        return NotFound();
                    }
                    throw;
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.Message.Contains("UQ_Artists_Name") == true)
                    {
                        ModelState.AddModelError("Name", "Артист с таким названием уже существует в каталоге. Попробуйте найти его через поиск.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Произошла ошибка при сохранении артиста: " + ex.InnerException?.Message ?? ex.Message);
                    }
                }
            }

            ViewData["IsOwnerOrAdmin"] = true;
            ViewBag.ReturnUrl = returnUrl;
            return View(artist);
        }

        // GET: Artists/Delete/5
        public async Task<IActionResult> Delete(int? id, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["ArtistErrorMessage"] = "Гостям запрещено удалять артистов.";
                return Forbid();
            }

            if (id == null)
            {
                TempData["ArtistErrorMessage"] = "Артист не найден.";
                return NotFound();
            }

            var artist = await _context.Artists
                .Include(a => a.Owner)
                .FirstOrDefaultAsync(m => m.ID == id);
            if (artist == null)
            {
                TempData["ArtistErrorMessage"] = "Артист не найден.";
                return NotFound();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null)
            {
                TempData["ArtistErrorMessage"] = "Пользователь не найден.";
                return Forbid();
            }

            if (!User.IsInRole("Admin") && artist.Owner_ID != user.ID)
            {
                TempData["ArtistErrorMessage"] = "Вы можете удалять только своих артистов.";
                return Forbid();
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(artist);
        }

        // POST: Artists/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["ArtistErrorMessage"] = "Гостям запрещено удалять артистов.";
                return Forbid();
            }

            var artist = await _context.Artists.FindAsync(id);
            if (artist == null)
            {
                TempData["ArtistErrorMessage"] = "Артист не найден.";
                return NotFound();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null)
            {
                TempData["ArtistErrorMessage"] = "Пользователь не найден.";
                return Forbid();
            }

            if (!User.IsInRole("Admin") && artist.Owner_ID != user.ID)
            {
                TempData["ArtistErrorMessage"] = "Вы можете удалять только своих артистов.";
                return Forbid();
            }

            _context.Artists.Remove(artist);
            await _context.SaveChangesAsync();
            TempData["ArtistSuccessMessage"] = $"Артист '{artist.Name}' успешно удалён.";
            return Redirect(returnUrl ?? Url.Action("Index"));
        }

        private bool ArtistExists(int id)
        {
            return _context.Artists.Any(e => e.ID == id);
        }
    }
}