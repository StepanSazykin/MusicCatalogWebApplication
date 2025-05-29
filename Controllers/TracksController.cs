using System;
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
    public class TracksController : BaseController
    {
        public TracksController(ApplicationDbContext context) : base(context) { }

        // GET: Tracks
        [AllowAnonymous]
        public async Task<IActionResult> Index(string searchString, int? genreId, int? yearFrom, int? yearTo, string tag, string sortOrder, bool onlyMine = false, int page = 1)
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

            var tracks = _context.Tracks
                .Include(t => t.Album).ThenInclude(a => a.Artist)
                .Include(t => t.Album).ThenInclude(a => a.Genre)
                .Include(t => t.Owner)
                .Include(t => t.TrackTags).ThenInclude(tt => tt.Tag)
                .AsQueryable();

            if (onlyMine && userId.HasValue)
            {
                tracks = tracks.Where(t => t.Owner_ID == userId);
            }
            else if (!isAdmin)
            {
                tracks = userId.HasValue
                    ? tracks.Where(t => t.IsPublic || t.Owner_ID == userId)
                    : tracks.Where(t => t.IsPublic);
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.ToLower();
                tracks = tracks.Where(t => t.Title.ToLower().Contains(searchString) ||
                                          t.Album.Title.ToLower().Contains(searchString) ||
                                          t.Album.Artist.Name.ToLower().Contains(searchString));
            }

            if (genreId.HasValue)
            {
                tracks = tracks.Where(t => t.Album.Genre_ID == genreId.Value);
            }

            if (yearFrom.HasValue)
            {
                if (yearFrom.Value < 1900 || yearFrom.Value > 2025)
                {
                    ViewBag.ErrorMessage = "Год выпуска должен быть в диапазоне от 1900 до 2025.";
                    return View(new TrackSearchViewModel());
                }
                tracks = tracks.Where(t => t.Album.ReleaseDate.HasValue && t.Album.ReleaseDate.Value.Year >= yearFrom.Value);
            }
            if (yearTo.HasValue)
            {
                if (yearTo.Value < 1900 || yearTo.Value > 2025)
                {
                    ViewBag.ErrorMessage = "Год выпуска должен быть в диапазоне от 1900 до 2025.";
                    return View(new TrackSearchViewModel());
                }
                tracks = tracks.Where(t => t.Album.ReleaseDate.HasValue && t.Album.ReleaseDate.Value.Year <= yearTo.Value);
            }

            if (!string.IsNullOrEmpty(tag))
            {
                tracks = tracks.Where(t => t.TrackTags.Any(tt => tt.Tag.Name.ToLower() == tag.ToLower()));
            }

            tracks = sortOrder switch
            {
                "title_desc" => tracks.OrderByDescending(t => t.Title),
                "duration" => tracks.OrderBy(t => t.Duration),
                "duration_desc" => tracks.OrderByDescending(t => t.Duration),
                _ => tracks.OrderBy(t => t.Title)
            };

            var totalItems = await tracks.CountAsync();
            var trackList = await tracks
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (!trackList.Any())
            {
                if (!string.IsNullOrEmpty(tag))
                {
                    ViewBag.NoResultsMessage = "По данному тегу ничего не найдено.";
                }
                else
                {
                    ViewBag.NoResultsMessage = "К сожалению, по вашему запросу ничего не найдено. Попробуйте изменить параметры поиска.";
                }
            }
            else
            {
                ViewBag.NoResultsMessage = null;
            }

            var genreData = await _context.Albums
                .Include(a => a.Genre)
                .Include(a => a.Tracks)
                .GroupBy(a => new { a.Genre.ID, a.Genre.Name })
                .Select(g => new
                {
                    ID = g.Key.ID,
                    Name = g.Key.Name,
                    TrackCount = g.Sum(a => a.Tracks.Count)
                })
                .OrderBy(g => g.Name)
                .ToListAsync();

            var genreList = genreData.Select(g => new
            {
                g.ID,
                Name = $"{g.Name} ({g.TrackCount})"
            }).ToList();

            var tagList = await _context.Tags
                .Select(t => new { t.ID, t.Name })
                .OrderBy(t => t.Name)
                .ToListAsync();

            ViewData["AppliedFilters"] = new
            {
                SearchString = searchString,
                Genre = genreId.HasValue ? _context.Genres.Find(genreId.Value)?.Name : null,
                YearFrom = yearFrom,
                YearTo = yearTo,
                Tag = tag,
                OnlyMine = onlyMine ? "Только мои" : null
            };

            ViewData["ShowAddTrackSuggestion"] = onlyMine && !trackList.Any() && string.IsNullOrEmpty(searchString) && !genreId.HasValue && !yearFrom.HasValue && !yearTo.HasValue && string.IsNullOrEmpty(tag);

            ViewData["SearchString"] = searchString;
            ViewData["GenreId"] = genreId;
            ViewData["YearFrom"] = yearFrom;
            ViewData["YearTo"] = yearTo;
            ViewData["Tag"] = tag;
            ViewData["SortOrder"] = sortOrder;
            ViewData["OnlyMine"] = onlyMine.ToString();
            ViewData["TotalPages"] = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewData["CurrentPage"] = page;
            ViewData["Tracks"] = trackList;
            ViewData["Genres"] = new SelectList(genreList, "ID", "Name", genreId);
            ViewData["Tags"] = new SelectList(tagList, "Name", "Name", tag);
            ViewData["CurrentUserId"] = userId ?? 0;

            return View(new TrackSearchViewModel
            {
                SearchString = searchString,
                GenreId = genreId,
                YearFrom = yearFrom,
                YearTo = yearTo,
                Tag = tag,
                SortOrder = sortOrder,
                OnlyMine = onlyMine
            });
        }

        // GET: Tracks/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id, string returnUrl = null)
        {
            if (id == null)
            {
                TempData["TrackErrorMessage"] = "Трек не указан.";
                return RedirectToAction(nameof(Index));
            }

            var track = await _context.Tracks
                .Include(t => t.Album).ThenInclude(a => a.Artist)
                .Include(t => t.Album).ThenInclude(a => a.Genre)
                .Include(t => t.Owner)
                .Include(t => t.TrackTags).ThenInclude(tt => tt.Tag)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (track == null)
            {
                TempData["TrackErrorMessage"] = "Трек не найден.";
                return RedirectToAction(nameof(Index));
            }

            var userId = User.Identity.IsAuthenticated && !User.IsInRole("Guest")
                ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0")
                : (int?)null;
            if (!track.IsPublic && !User.IsInRole("Admin") && track.Owner_ID != userId)
            {
                TempData["TrackErrorMessage"] = "У вас нет доступа к этому треку.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(track);
        }

        // GET: Tracks/Create
        public IActionResult Create(int? albumId, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                return Forbid();
            }

            ViewData["Album_ID"] = new SelectList(_context.Albums.Include(a => a.Artist), "ID", "Title", albumId);
            ViewData["Tags"] = new MultiSelectList(_context.Tags, "ID", "Name");
            ViewBag.AlbumId = albumId;
            ViewBag.ReturnUrl = returnUrl;
            return View(/*new TrackViewModel { Album_ID = ViewBag.AlbumId }*/);
        }

        // POST: Tracks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Album_ID,Duration,IsPublic,SelectedTagIds,DurationString")] TrackViewModel viewModel, int? albumId, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["TrackErrorMessage"] = "Гостям запрещено создавать треки.";
                return Forbid();
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            if (!string.IsNullOrEmpty(viewModel.DurationString))
            {
                if (TimeSpan.TryParseExact(viewModel.DurationString, @"mm\:ss", null, out var duration))
                {
                    viewModel.Duration = (short)duration.TotalSeconds;
                }
                else
                {
                    ModelState.AddModelError("DurationString", "Неверный формат длительности. Используйте MM:SS (например, 03:45).");
                }
            }

            // Предварительная проверка на уникальность названия трека в альбоме
            var album = await _context.Albums.Include(a => a.Artist).FirstOrDefaultAsync(a => a.ID == viewModel.Album_ID);
            if (await _context.Tracks.AnyAsync(t => t.Title == viewModel.Title && t.Album_ID == viewModel.Album_ID))
            {
                ModelState.AddModelError("Title", $"Трек с названием '{viewModel.Title}' уже существует в альбоме '{album?.Title ?? "Неизвестный"}' артиста '{album?.Artist?.Name ?? "Неизвестный"}'. Попробуйте найти его через поиск.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var track = new Track
                    {
                        Title = viewModel.Title,
                        Album_ID = viewModel.Album_ID,
                        Duration = viewModel.Duration,
                        IsPublic = viewModel.IsPublic,
                        Owner_ID = userId
                    };

                    _context.Add(track);
                    await _context.SaveChangesAsync();

                    if (viewModel.SelectedTagIds != null && viewModel.SelectedTagIds.Any())
                    {
                        foreach (var tagId in viewModel.SelectedTagIds)
                        {
                            _context.TrackTags.Add(new TrackTag { Track_ID = track.ID, Tag_ID = tagId });
                        }
                        await _context.SaveChangesAsync();
                    }

                    if (track.IsPublic)
                    {
                        track.IsPublic = false;
                        await CreatePublicProposalAsync("Tracks", track.ID, $"Сделать трек '{track.Title}' публичным", userId);
                        _context.Update(track);
                        await _context.SaveChangesAsync();
                        TempData["TrackSuccessMessage"] = $"Ваш трек '{track.Title}' отправлен на модерацию администратором и в скором времени будет рассмотрен.";
                    }
                    else
                    {
                        TempData["TrackSuccessMessage"] = $"Ваш трек '{track.Title}' успешно добавлен в ваш личный каталог.";
                    }

                    return Redirect(returnUrl ?? (albumId.HasValue ? Url.Action("Tracks", "Albums", new { albumId }) : Url.Action("Index")));
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.Message.Contains("UQ_Tracks_Title_Album") == true)
                    {
                        ModelState.AddModelError("Title", $"Трек с названием '{viewModel.Title}' уже существует в альбоме '{album?.Title ?? "Неизвестный"}' артиста '{album?.Artist?.Name ?? "Неизвестный"}'. Попробуйте найти его через поиск.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Произошла ошибка при сохранении трека: " + ex.InnerException?.Message ?? ex.Message);
                    }
                }
            }

            ViewData["Album_ID"] = new SelectList(_context.Albums.Include(a => a.Artist).OrderBy(a => a.Title), "ID", "Title", viewModel.Album_ID);
            ViewData["Tags"] = new MultiSelectList(_context.Tags, "ID", "Name", viewModel.SelectedTagIds);
            ViewBag.AlbumId = albumId;
            ViewBag.ReturnUrl = returnUrl;
            return View(viewModel);
        }

        // GET: Tracks/Edit/5
        public async Task<IActionResult> Edit(int id, int? albumId, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["TrackErrorMessage"] = "Гостям запрещено редактировать треки.";
                return Forbid();
            }

            var track = await _context.Tracks
                .Include(t => t.Album).ThenInclude(a => a.Artist)
                .Include(t => t.TrackTags)
                .FirstOrDefaultAsync(t => t.ID == id);
            if (track == null)
            {
                TempData["TrackErrorMessage"] = "Трек не найден.";
                return NotFound();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null)
            {
                TempData["TrackErrorMessage"] = "Пользователь не найден.";
                return Forbid();
            }

            var viewModel = new TrackViewModel
            {
                ID = track.ID,
                Title = track.Title,
                Album_ID = track.Album_ID,
                Duration = track.Duration,
                DurationString = track.Duration > 0 ? TimeSpan.FromSeconds(track.Duration).ToString(@"mm\:ss") : "",
                IsPublic = track.IsPublic,
                SelectedTagIds = track.TrackTags.Select(t => t.Tag_ID).ToList()
            };

            ViewData["Album_ID"] = new SelectList(_context.Albums.Include(a => a.Artist), "ID", "Title", track.Album_ID);
            ViewData["Tags"] = new MultiSelectList(_context.Tags, "ID", "Name", viewModel.SelectedTagIds);
            ViewBag.AlbumId = albumId;
            ViewBag.IsOwnerOrAdmin = await CanEdit(track.Owner_ID); // Флаг для определения прав
            ViewBag.AlbumTitle = track.Album?.Title; // Для сообщения
            ViewBag.ArtistName = track.Album?.Artist?.Name; // Для сообщения
            ViewBag.ReturnUrl = returnUrl;
            return View(viewModel);
        }

        // POST: Tracks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,Title,Album_ID,Duration,DurationString,IsPublic,SelectedTagIds")] TrackViewModel viewModel, int? albumId, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["TrackErrorMessage"] = "Гостям запрещено редактировать треки.";
                return Forbid();
            }

            if (id != viewModel.ID)
            {
                TempData["TrackErrorMessage"] = "Трек не найден.";
                return NotFound();
            }

            var track = await _context.Tracks
                .Include(t => t.Album).ThenInclude(a => a.Artist)
                .Include(t => t.TrackTags)
                .FirstOrDefaultAsync(t => t.ID == id);
            if (track == null)
            {
                TempData["TrackErrorMessage"] = "Трек не найден.";
                return NotFound();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null)
            {
                TempData["TrackErrorMessage"] = "Пользователь не найден.";
                return Forbid();
            }

            bool isOwnerOrAdmin = await CanEdit(track.Owner_ID);
            ViewBag.IsOwnerOrAdmin = isOwnerOrAdmin;
            ViewBag.AlbumId = albumId;
            ViewBag.AlbumTitle = track.Album?.Title;
            ViewBag.ArtistName = track.Album?.Artist?.Name;

            // Конвертация DurationString в Duration (только для владельцев/админов)
            if (isOwnerOrAdmin && !string.IsNullOrEmpty(viewModel.DurationString))
            {
                if (TimeSpan.TryParseExact(viewModel.DurationString, @"mm\:ss", null, out var duration))
                {
                    viewModel.Duration = (short)duration.TotalSeconds;
                }
                else
                {
                    ModelState.AddModelError("DurationString", "Неверный формат длительности. Используйте MM:SS (например, 03:45).");
                }
            }

            if (!isOwnerOrAdmin)
            {
                // Для не-владельцев/админов валидируем только SelectedTagIds
                ModelState.Remove("Title");
                ModelState.Remove("Album_ID");
                ModelState.Remove("Duration");
                ModelState.Remove("DurationString");
                ModelState.Remove("IsPublic");
            }

            // Предварительная проверка на уникальность названия трека в альбоме, исключая текущий трек
            if (isOwnerOrAdmin && viewModel.Title != track.Title &&
                await _context.Tracks.AnyAsync(t => t.Title == viewModel.Title && t.Album_ID == viewModel.Album_ID && t.ID != viewModel.ID))
            {
                ModelState.AddModelError("Title", $"Трек с названием '{viewModel.Title}' уже существует в альбоме '{track.Album?.Title ?? "Неизвестный"}' артиста '{track.Album?.Artist?.Name ?? "Неизвестный"}'. Попробуйте найти его через поиск.");
            }

            if (!ModelState.IsValid)
            {
                ViewData["Album_ID"] = new SelectList(_context.Albums.Include(a => a.Artist), "ID", "Title", viewModel.Album_ID);
                ViewData["Tags"] = new MultiSelectList(_context.Tags, "ID", "Name", viewModel.SelectedTagIds);
                TempData["TrackErrorMessage"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return View(viewModel);
            }

            try
            {
                if (isOwnerOrAdmin)
                {
                    // Прямое редактирование для владельца или админа
                    track.Title = viewModel.Title;
                    track.Album_ID = viewModel.Album_ID;
                    track.Duration = viewModel.Duration;
                    var originalIsPublic = track.IsPublic;
                    track.IsPublic = viewModel.IsPublic;

                    _context.Update(track);

                    // Обновление тегов
                    var existingTagIds = track.TrackTags.Select(tt => tt.Tag_ID).ToList();
                    var newTagIds = viewModel.SelectedTagIds ?? new List<int>();

                    // Удаление старых тегов
                    var tagsToRemove = existingTagIds.Except(newTagIds).ToList();
                    _context.TrackTags.RemoveRange(track.TrackTags.Where(tt => tagsToRemove.Contains(tt.Tag_ID)));

                    // Добавление новых тегов
                    var tagsToAdd = newTagIds.Except(existingTagIds).ToList();
                    foreach (var tagId in tagsToAdd)
                    {
                        _context.TrackTags.Add(new TrackTag { Track_ID = track.ID, Tag_ID = tagId });
                    }

                    await _context.SaveChangesAsync();

                    if (viewModel.IsPublic && !originalIsPublic)
                    {
                        track.IsPublic = false;
                        await CreatePublicProposalAsync("Tracks", track.ID, $"Сделать трек '{track.Title}' публичным", user.ID);
                        _context.Update(track);
                        await _context.SaveChangesAsync();
                        TempData["TrackSuccessMessage"] = $"Ваш трек '{track.Title}' отправлен на модерацию администратором и в скором времени будет рассмотрен.";
                    }
                    else
                    {
                        TempData["TrackSuccessMessage"] = $"Трек '{track.Title}' успешно обновлён.";
                    }
                }
                else
                {
                    // Предложение тегов для остальных пользователей
                    var proposedTagIds = viewModel.SelectedTagIds ?? new List<int>();
                    var proposedTagNames = await _context.Tags
                        .Where(t => proposedTagIds.Contains(t.ID))
                        .Select(t => t.Name)
                        .ToListAsync();
                    var tagsString = proposedTagNames.Any() ? string.Join(", ", proposedTagNames) : "отсутствуют";

                    var proposedChange = $"Предложенные теги: {tagsString} для трека '{track.Title}' из альбома '{track.Album?.Title ?? "Не указан"}' исполнителя '{track.Album?.Artist?.Name ?? "Не указан"}'";
                    await CreatePublicProposalAsync("Tracks", track.ID, proposedChange, user.ID);

                    TempData["TrackSuccessMessage"] = $"{proposedChange} отправлены на модерацию и скоро будут рассмотрены.";
                }

                return Redirect(returnUrl ?? (albumId.HasValue ? Url.Action("Tracks", "Albums", new { albumId }) : Url.Action("Index")));
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.Message.Contains("UQ_Tracks_Title_Album") == true)
                {
                    ModelState.AddModelError("Title", $"Трек с названием '{viewModel.Title}' уже существует в альбоме '{track.Album?.Title ?? "Неизвестный"}' артиста '{track.Album?.Artist?.Name ?? "Неизвестный"}'. Попробуйте найти его через поиск.");
                }
                else
                {
                    ModelState.AddModelError("", $"Произошла ошибка при сохранении: {ex.InnerException?.Message ?? ex.Message}");
                }
                ViewData["Album_ID"] = new SelectList(_context.Albums.Include(a => a.Artist), "ID", "Title", viewModel.Album_ID);
                ViewData["Tags"] = new MultiSelectList(_context.Tags, "ID", "Name", viewModel.SelectedTagIds);
                ViewBag.ReturnUrl = returnUrl;
                return View(viewModel);
            }
        }

        // GET: Tracks/Delete/5
        public IActionResult Delete(int id, int? albumId, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                return Forbid();
            }

            var track = _context.Tracks
                .Include(t => t.Album).ThenInclude(a => a.Artist)
                .FirstOrDefault(t => t.ID == id);
            if (track == null)
            {
                return NotFound();
            }

            if (!User.IsInRole("Admin") && track.Owner_ID != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0"))
            {
                return Forbid();
            }

            ViewBag.AlbumId = albumId;
            ViewBag.ReturnUrl = returnUrl;
            return View(track);
        }

        // POST: Tracks/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, int? albumId, string returnUrl = null)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["TrackErrorMessage"] = "Гостям запрещено удалять треки.";
                return Forbid();
            }

            var track = await _context.Tracks.FindAsync(id);
            if (track == null)
            {
                TempData["TrackErrorMessage"] = "Трек не найден.";
                return NotFound();
            }

            if (!User.IsInRole("Admin") && track.Owner_ID != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0"))
            {
                TempData["TrackErrorMessage"] = "У вас нет прав для удаления этого трека.";
                return Forbid();
            }

            _context.Tracks.Remove(track);
            await _context.SaveChangesAsync();

            TempData["TrackSuccessMessage"] = $"Трек '{track.Title}' успешно удален.";

            return Redirect(returnUrl ?? (albumId.HasValue ? Url.Action("Tracks", "Albums", new { albumId }) : Url.Action("Index")));
        }

        private bool TrackExists(int id)
        {
            return _context.Tracks.Any(e => e.ID == id);
        }       
    }
}