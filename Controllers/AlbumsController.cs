using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MusicCatalogWebApplication.Context;
using MusicCatalogWebApplication.Models;
using System.Security.Claims;

namespace MusicCatalogWebApplication.Controllers
{
    [Authorize]
    public class AlbumsController : BaseController
    {
        public AlbumsController(ApplicationDbContext context) : base(context) { }

        // GET: Albums
        [AllowAnonymous]
        public async Task<IActionResult> Index(string searchString, int? artistId, int? genreId, int? releaseYearFrom, int? releaseYearTo, int page = 1)
        {
            int pageSize = 10;
            int? userId = null;
            if (User.Identity.IsAuthenticated && !User.IsInRole("Guest"))
            {
                var userLogin = User.Identity.Name;
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == userLogin);
                if (user != null)
                {
                    userId = user.ID;
                }
            }
            var isAdmin = User.IsInRole("Admin");

            var albums = _context.Albums
                .Include(a => a.Artist)
                .Include(a => a.Genre)
                .Include(a => a.Owner)
                .AsQueryable();

            if (!isAdmin)
            {
                albums = userId.HasValue
                    ? albums.Where(a => a.IsPublic || a.Owner_ID == userId)
                    : albums.Where(a => a.IsPublic);
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.ToLower();
                albums = albums.Where(a => a.Title.ToLower().Contains(searchString));
            }

            if (artistId.HasValue)
            {
                albums = albums.Where(a => a.Artist_ID == artistId.Value);
            }

            if (genreId.HasValue)
            {
                albums = albums.Where(a => a.Genre_ID == genreId.Value);
            }

            if (releaseYearFrom.HasValue)
            {
                if (releaseYearFrom.Value < 1900 || releaseYearFrom.Value > 2025)
                {
                    ViewBag.ErrorMessage = "Год выпуска должен быть в диапазоне от 1900 до 2025.";
                    return View(new List<Album>());
                }
                albums = albums.Where(a => a.ReleaseDate.HasValue && a.ReleaseDate.Value.Year >= releaseYearFrom.Value);
            }
            if (releaseYearTo.HasValue)
            {
                if (releaseYearTo.Value < 1900 || releaseYearTo.Value > 2025)
                {
                    ViewBag.ErrorMessage = "Год выпуска должен быть в диапазоне от 1900 до 2025.";
                    return View(new List<Album>());
                }
                albums = albums.Where(a => a.ReleaseDate.HasValue && a.ReleaseDate.Value.Year <= releaseYearTo.Value);
            }

            var totalItems = await albums.CountAsync();
            var albumList = await albums
                .OrderBy(a => a.Owner_ID != userId)
                .ThenBy(a => a.Title)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (!albumList.Any())
            {
                ViewBag.NoResultsMessage = "К сожалению, по вашему запросу ничего не найдено. Попробуйте изменить параметры поиска.";
            }
            else
            {
                ViewBag.NoResultsMessage = null;
            }

            ViewData["Artists"] = new SelectList(await _context.Artists.Select(a => new { a.ID, a.Name }).ToListAsync(), "ID", "Name", artistId);
            ViewData["Genres"] = new SelectList(await _context.Genres.Select(g => new { g.ID, g.Name }).ToListAsync(), "ID", "Name", genreId);
            ViewData["SearchString"] = searchString;
            ViewData["ArtistId"] = artistId;
            ViewData["GenreId"] = genreId;
            ViewData["ReleaseYearFrom"] = releaseYearFrom;
            ViewData["ReleaseYearTo"] = releaseYearTo;
            ViewData["TotalPages"] = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewData["CurrentPage"] = page;
            ViewData["CurrentUserId"] = userId ?? 0;

            return View(albumList);
        }

        // GET: Albums/Tracks/5
        [AllowAnonymous]
        public async Task<IActionResult> Tracks(int albumId, bool fromCatalog = false, string returnUrl = null)
        {            
            var userId = User.Identity.IsAuthenticated && !User.IsInRole("Guest")
                ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0")
                : (int?)null;
            var isAdmin = User.IsInRole("Admin");

            var tracks = isAdmin
                ? _context.Tracks
                    .Where(t => t.Album_ID == albumId)
                    .Include(t => t.Album).ThenInclude(a => a.Artist)
                    .Include(t => t.Owner)
                : userId.HasValue
                ? _context.Tracks
                    .Where(t => t.Album_ID == albumId && (t.IsPublic || t.Owner_ID == userId))
                    .Include(t => t.Album).ThenInclude(a => a.Artist)
                    .Include(t => t.Owner)
                : _context.Tracks
                    .Where(t => t.Album_ID == albumId && t.IsPublic)
                    .Include(t => t.Album).ThenInclude(a => a.Artist)
                    .Include(t => t.Owner);

            var album = await _context.Albums.FindAsync(albumId);
            if (album == null)
            {
                return NotFound();
            }

            ViewData["CurrentUserId"] = userId;
            ViewData["Album"] = album;
            ViewBag.FromCatalog = fromCatalog;
            ViewBag.ReturnUrl = returnUrl;
            return View(await tracks.ToListAsync());
        }

        // GET: Albums/Create
        public IActionResult Create(int? artistId, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["AlbumErrorMessage"] = "Гостям запрещено создавать альбомы.";
                return Forbid();
            }
            ViewData["Artist_ID"] = new SelectList(_context.Artists, "ID", "Name", artistId);
            ViewData["Genre_ID"] = new SelectList(_context.Genres, "ID", "Name");
            ViewBag.ArtistId = artistId;
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: Albums/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,Title,Artist_ID,ReleaseDate,Genre_ID,IsPublic")] Album album, int? artistId, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["AlbumErrorMessage"] = "Гостям запрещено создавать альбомы.";
                return Forbid();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null)
            {
                TempData["AlbumErrorMessage"] = "Пользователь не найден.";
                return Forbid();
            }
            album.Owner_ID = user.ID;

            // Предварительная проверка на уникальность названия альбома для артиста
            if (await _context.Albums.AnyAsync(a => a.Title == album.Title && a.Artist_ID == album.Artist_ID))
            {
                ModelState.AddModelError("Title", "Альбом с таким названием уже существует у этого артиста. Попробуйте найти его через поиск.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(album);
                    await _context.SaveChangesAsync();

                    if (album.IsPublic)
                    {
                        album.IsPublic = false;
                        await CreatePublicProposalAsync("Albums", album.ID, $"Сделать альбом '{album.Title}' публичным", user.ID);
                        _context.Update(album);
                        await _context.SaveChangesAsync();
                        TempData["AlbumSuccessMessage"] = $"Ваш альбом '{album.Title}' отправлен на модерацию администратором и в скором времени будет рассмотрен.";
                    }
                    else
                    {
                        TempData["AlbumSuccessMessage"] = $"Ваш альбом '{album.Title}' успешно добавлен в ваш личный каталог.";
                    }

                    return Redirect(returnUrl ?? (artistId.HasValue ? Url.Action("Albums", "Artists", new { artistId }) : Url.Action("Index")));
                }
                catch (DbUpdateException ex)
                {
                    var artist = await _context.Artists.FindAsync(album.Artist_ID);
                    if (ex.InnerException?.Message.Contains("UQ_Albums_Title_Artist") == true)
                    {
                        ModelState.AddModelError("Title", $"Альбом с названием '{album.Title}' уже существует у артиста '{artist?.Name ?? "Неизвестный"}'. Попробуйте найти его через поиск.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Произошла ошибка при сохранении альбома: " + ex.InnerException?.Message ?? ex.Message);
                    }
                }
            }

            ViewData["Artist_ID"] = new SelectList(_context.Artists, "ID", "Name", album.Artist_ID);
            ViewData["Genre_ID"] = new SelectList(_context.Genres, "ID", "Name", album.Genre_ID);
            ViewBag.ArtistId = artistId;
            ViewBag.ReturnUrl = returnUrl;
            return View(album);
        }

        // GET: Albums/Edit/5
        public async Task<IActionResult> Edit(int? id, int? artistId, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["AlbumErrorMessage"] = "Гостям запрещено редактировать альбомы.";
                return Forbid();
            }

            if (id == null)
            {
                TempData["AlbumErrorMessage"] = "Альбом не найден.";
                return NotFound();
            }

            var album = await _context.Albums.FindAsync(id);
            if (album == null)
            {
                TempData["AlbumErrorMessage"] = "Альбом не найден.";
                return NotFound();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null)
            {
                TempData["AlbumErrorMessage"] = "Пользователь не найден.";
                return Forbid();
            }

            if (!User.IsInRole("Admin") && album.Owner_ID != user.ID)
            {
                TempData["AlbumErrorMessage"] = "Вы можете редактировать только свои альбомы.";
                return Forbid();
            }

            ViewData["Artist_ID"] = new SelectList(_context.Artists, "ID", "Name", album.Artist_ID);
            ViewData["Genre_ID"] = new SelectList(_context.Genres, "ID", "Name", album.Genre_ID);
            ViewData["IsOwnerOrAdmin"] = true;
            ViewBag.ArtistId = artistId;
            ViewBag.ReturnUrl = returnUrl;
            return View(album);
        }

        // POST: Albums/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,Title,Artist_ID,ReleaseDate,Genre_ID,IsPublic")] Album album, int? artistId, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["AlbumErrorMessage"] = "Гостям запрещено редактировать альбомы.";
                return Forbid();
            }

            if (id != album.ID)
            {
                TempData["AlbumErrorMessage"] = "Альбом не найден.";
                return NotFound();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null)
            {
                TempData["AlbumErrorMessage"] = "Пользователь не найден.";
                return Forbid();
            }

            var existingAlbum = await _context.Albums.FindAsync(id);
            if (existingAlbum == null)
            {
                TempData["AlbumErrorMessage"] = "Альбом не найден.";
                return NotFound();
            }

            if (!User.IsInRole("Admin") && existingAlbum.Owner_ID != user.ID)
            {
                TempData["AlbumErrorMessage"] = "Вы можете редактировать только свои альбомы.";
                return Forbid();
            }

            ViewData["IsOwnerOrAdmin"] = true;

            // Предварительная проверка на уникальность названия альбома, исключая текущий альбом
            if (album.Title != existingAlbum.Title &&
                await _context.Albums.AnyAsync(a => a.Title == album.Title && a.Artist_ID == album.Artist_ID && a.ID != album.ID))
            {
                ModelState.AddModelError("Title", "Альбом с таким названием уже существует у этого артиста. Попробуйте найти его через поиск.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var originalIsPublic = existingAlbum.IsPublic;
                    existingAlbum.Title = album.Title;
                    existingAlbum.Artist_ID = album.Artist_ID;
                    existingAlbum.ReleaseDate = album.ReleaseDate;
                    existingAlbum.Genre_ID = album.Genre_ID;
                    existingAlbum.IsPublic = album.IsPublic;

                    _context.Update(existingAlbum);
                    await _context.SaveChangesAsync();

                    if (album.IsPublic && !originalIsPublic)
                    {
                        existingAlbum.IsPublic = false;
                        await CreatePublicProposalAsync("Albums", existingAlbum.ID, $"Сделать альбом '{existingAlbum.Title}' публичным", user.ID);
                        _context.Update(existingAlbum);
                        await _context.SaveChangesAsync();
                        TempData["AlbumSuccessMessage"] = $"Ваш альбом '{existingAlbum.Title}' отправлен на модерацию администратором и в скором времени будет рассмотрен.";
                    }
                    else
                    {
                        TempData["AlbumSuccessMessage"] = $"Альбом '{existingAlbum.Title}' успешно обновлён.";
                    }

                    return Redirect(returnUrl ?? (artistId.HasValue ? Url.Action("Albums", "Artists", new { artistId }) : Url.Action("Index")));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AlbumExists(album.ID))
                    {
                        TempData["AlbumErrorMessage"] = "Альбом не найден.";
                        return NotFound();
                    }
                    throw;
                }
                catch (DbUpdateException ex)
                {
                    var artist = await _context.Artists.FindAsync(album.Artist_ID);
                    if (ex.InnerException?.Message.Contains("UQ_Albums_Title_Artist") == true)
                    {
                        ModelState.AddModelError("Title", $"Альбом с названием '{album.Title}' уже существует у артиста '{artist?.Name ?? "Неизвестный"}'. Попробуйте найти его через поиск.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Произошла ошибка при сохранении альбома: " + ex.InnerException?.Message ?? ex.Message);
                    }
                }
            }

            ViewData["Artist_ID"] = new SelectList(_context.Artists, "ID", "Name", album.Artist_ID);
            ViewData["Genre_ID"] = new SelectList(_context.Genres, "ID", "Name", album.Genre_ID);
            ViewBag.ArtistId = artistId;
            ViewBag.ReturnUrl = returnUrl;
            return View(album);
        }

        // GET: Albums/Delete/5
        public IActionResult Delete(int id, int? artistId, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["AlbumErrorMessage"] = "Гостям запрещено удалять альбомы.";
                return Forbid();
            }

            var album = _context.Albums
                .Include(a => a.Artist)
                .Include(a => a.Genre)
                .FirstOrDefault(a => a.ID == id);
            if (album == null)
            {
                TempData["AlbumErrorMessage"] = "Альбом не найден.";
                return NotFound();
            }

            if (!User.IsInRole("Admin") && album.Owner_ID != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0"))
            {
                TempData["AlbumErrorMessage"] = "Вы можете удалять только свои альбомы.";
                return Forbid();
            }

            ViewBag.ArtistId = artistId;
            ViewBag.ReturnUrl = returnUrl;
            return View(album);
        }

        // POST: Albums/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, int? artistId, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["AlbumErrorMessage"] = "Гостям запрещено удалять альбомы.";
                return Forbid();
            }

            var album = await _context.Albums.FindAsync(id);
            if (album == null)
            {
                TempData["AlbumErrorMessage"] = "Альбом не найден.";
                return NotFound();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (!User.IsInRole("Admin") && album.Owner_ID != user?.ID)
            {
                TempData["AlbumErrorMessage"] = "Вы можете удалять только свои альбомы.";
                return Forbid();
            }

            _context.Albums.Remove(album);
            await _context.SaveChangesAsync();
            TempData["AlbumSuccessMessage"] = $"Альбом '{album.Title}' успешно удалён.";

            return Redirect(returnUrl ?? (artistId.HasValue ? Url.Action("Albums", "Artists", new { artistId }) : Url.Action("Index")));
        }

        private bool AlbumExists(int id)
        {
            return _context.Albums.Any(e => e.ID == id);
        }
    }
}