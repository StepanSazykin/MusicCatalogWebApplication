using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicCatalogWebApplication.Context;
using MusicCatalogWebApplication.Models;

namespace MusicCatalogWebApplication.Controllers
{
    [Authorize]
    public class PlaylistsController : BaseController
    {
        public PlaylistsController(ApplicationDbContext context) : base(context) { }

        // GET: Playlists
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
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

            var playlists = isAdmin

                ? _context.Playlists.Include(p => p.User).OrderBy(p => p.User_ID != userId).ThenBy(p => p.Name)
                : userId.HasValue
                ? _context.Playlists
                    .Where(p => p.IsPublic || p.User_ID == userId)
                    .Include(p => p.User)
                    .OrderBy(p => p.User_ID != userId)
                    .ThenBy(p => p.Name)
                : _context.Playlists
                    .Where(p => p.IsPublic)
                    .Include(p => p.User)
                    .OrderBy(p => p.Name);

            ViewData["CurrentUserId"] = userId ?? 0;
            return View(await playlists.ToListAsync());
        }

        // GET: Playlists/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                TempData["PlaylistPlaylistErrorMessage"] = "Плейлист не найден.";
                return NotFound();
            }

            var playlist = await _context.Playlists
                .Include(p => p.User)
                .Include(p => p.PlaylistTracks).ThenInclude(pt => pt.Track).ThenInclude(t => t.Album).ThenInclude(a => a.Artist)
                .FirstOrDefaultAsync(m => m.ID == id);
            if (playlist == null)
            {
                TempData["PlaylistPlaylistErrorMessage"] = "Плейлист не найден.";
                return NotFound();
            }

            int? userId = null;
            if (User.Identity.IsAuthenticated && !User.IsInRole("Guest"))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
                if (user != null)
                {
                    userId = user.ID;
                }
            }

            if (!playlist.IsPublic && !User.IsInRole("Admin") && playlist.User_ID != userId)
            {
                TempData["PlaylistPlaylistErrorMessage"] = "У вас нет доступа к этому плейлисту.";
                return Forbid();
            }

            ViewData["CurrentUserId"] = userId ?? 0;
            return View(playlist);
        }

        // GET: Playlists/Create
        public async Task<IActionResult> Create()
        {
            if (User.IsInRole("Guest"))
            {
                TempData["PlaylistPlaylistErrorMessage"] = "Гостям запрещено создавать плейлисты.";
                return Forbid();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null)
            {
                TempData["PlaylistPlaylistErrorMessage"] = "Пользователь не найден.";
                return Forbid();
            }

            var tracks = await _context.Tracks
                .Include(t => t.Album)
                .ThenInclude(a => a.Artist)
                .Where(t => t.IsPublic || t.Owner_ID == user.ID)
                .ToListAsync();

            if (!tracks.Any())
            {
                TempData["PlaylistPlaylistErrorMessage"] = "Нет доступных треков для добавления в плейлист.";
                return View("NoTracksAvailable");
            }

            ViewData["Tracks"] = tracks;
            return View();
        }

        // POST: Playlists/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,IsPublic,Description")] Playlist playlist, int[] trackIds)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["PlaylistPlaylistErrorMessage"] = "Гостям запрещено создавать плейлисты.";
                return Forbid();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null)
            {
                TempData["PlaylistPlaylistErrorMessage"] = "Пользователь не найден.";
                return Forbid();
            }
            playlist.User_ID = user.ID;
            playlist.CreatedDate = DateTime.Now;

            // Очищаем ModelState для User
            ModelState.Remove("User");
            ModelState.Remove("User_ID"); // На случай, если User_ID валидируется

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                TempData["PlaylistPlaylistErrorMessage"] = string.Join("; ", errors);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(playlist);
                    await _context.SaveChangesAsync();

                    if (trackIds != null && trackIds.Any())
                    {
                        short order = 1;
                        foreach (var trackId in trackIds)
                        {
                            var track = await _context.Tracks.FindAsync(trackId);
                            if (track != null && (track.IsPublic || track.Owner_ID == user.ID))
                            {
                                var playlistTrack = new PlaylistTrack
                                {
                                    Playlist_ID = playlist.ID,
                                    Track_ID = trackId,
                                    TrackOrder = order++
                                };
                                _context.PlaylistTracks.Add(playlistTrack);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    if (playlist.IsPublic)
                    {
                        playlist.IsPublic = false;
                        string changeDescription = $"Сделать плейлист '{playlist.Name}' публичным";
                        await CreatePublicProposalAsync("Playlists", playlist.ID, changeDescription, user.ID);
                        _context.Update(playlist);
                        await _context.SaveChangesAsync();
                        TempData["PlaylistPlaylistSuccessMessage"] = $"Ваш плейлист '{playlist.Name}' отправлен на модерацию администратором и в скором времени будет рассмотрен.";
                    }
                    else
                    {
                        TempData["PlaylistPlaylistSuccessMessage"] = "Плейлист успешно создан.";
                    }

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    TempData["PlaylistPlaylistErrorMessage"] = $"Произошла ошибка при сохранении плейлиста: {ex.InnerException?.Message ?? ex.Message}";
                }
            }

            var tracks = await _context.Tracks
                .Include(t => t.Album)
                .ThenInclude(a => a.Artist)
                .Where(t => t.IsPublic || t.Owner_ID == user.ID)
                .ToListAsync();
            ViewData["Tracks"] = tracks;
            return View(playlist);
        }

        // GET: Playlists/AddTrack/5
        public async Task<IActionResult> AddTrack(int? id)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["PlaylistPlaylistErrorMessage"] = "Гостям запрещено добавлять треки.";
                return Forbid();
            }

            if (id == null)
            {
                TempData["PlaylistPlaylistErrorMessage"] = "Плейлист не найден.";
                return NotFound();
            }

            var playlist = await _context.Playlists.FindAsync(id);
            if (playlist == null)
            {
                TempData["PlaylistPlaylistErrorMessage"] = "Плейлист не найден.";
                return NotFound();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null || !await CanEdit(playlist.User_ID))
            {
                TempData["PlaylistPlaylistErrorMessage"] = "У вас нет доступа для добавления треков в этот плейлист.";
                return Forbid();
            }

            var tracks = await _context.Tracks
                .Include(t => t.Album)
                .ThenInclude(a => a.Artist)
                .Where(t => t.IsPublic || t.Owner_ID == user.ID)
                .ToListAsync();
            if (!tracks.Any())
            {
                TempData["PlaylistPlaylistErrorMessage"] = "Нет доступных треков для добавления.";
                return View("NoTracksAvailable");
            }

            ViewData["Tracks"] = tracks;
            ViewData["PlaylistId"] = id;
            return View();
        }

        // POST: Playlists/AddTrack/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTrack(int id, int trackId)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["PlaylistPlaylistErrorMessage"] = "Гостям запрещено добавлять треки.";
                return Forbid();
            }

            var playlist = await _context.Playlists.FindAsync(id);
            if (playlist == null)
            {
                TempData["PlaylistPlaylistErrorMessage"] = "Плейлист не найден.";
                return NotFound();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null || !await CanEdit(playlist.User_ID))
            {
                TempData["PlaylistPlaylistErrorMessage"] = "У вас нет доступа для добавления треков в этот плейлист.";
                return Forbid();
            }

            var track = await _context.Tracks.FindAsync(trackId);
            if (track == null)
            {
                TempData["PlaylistPlaylistErrorMessage"] = "Трек не найден.";
                return NotFound();
            }

            if (await _context.PlaylistTracks.AnyAsync(pt => pt.Playlist_ID == id && pt.Track_ID == trackId))
            {
                TempData["PlaylistPlaylistErrorMessage"] = "Этот трек уже есть в плейлисте.";
                var tracks = await _context.Tracks
                    .Include(t => t.Album)
                    .ThenInclude(a => a.Artist)
                    .Where(t => t.IsPublic || t.Owner_ID == user.ID)
                    .ToListAsync();
                ViewData["Tracks"] = tracks;
                ViewData["PlaylistId"] = id;
                return View();
            }

            var maxOrder = await _context.PlaylistTracks
                .Where(pt => pt.Playlist_ID == id)
                .MaxAsync(pt => (short?)pt.TrackOrder) ?? 0;

            var playlistTrack = new PlaylistTrack
            {
                Playlist_ID = id,
                Track_ID = trackId,
                TrackOrder = (short)(maxOrder + 1)
            };

            try
            {
                _context.PlaylistTracks.Add(playlistTrack);
                await _context.SaveChangesAsync();
                TempData["PlaylistPlaylistSuccessMessage"] = "Трек успешно добавлен в плейлист.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (DbUpdateException ex)
            {
                TempData["PlaylistErrorMessage"] = $"Произошла ошибка при добавлении трека: {ex.InnerException?.Message ?? ex.Message}";
                var tracks = await _context.Tracks
                    .Include(t => t.Album)
                    .ThenInclude(a => a.Artist)
                    .Where(t => t.IsPublic || t.Owner_ID == user.ID)
                    .ToListAsync();
                ViewData["Tracks"] = tracks;
                ViewData["PlaylistId"] = id;
                return View();
            }
        }

        // POST: Playlists/RemoveTrack
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveTrack(int playlistId, int trackId)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["PlaylistErrorMessage"] = "Гостям запрещено удалять треки.";
                return Forbid();
            }

            var playlist = await _context.Playlists.FindAsync(playlistId);
            if (playlist == null)
            {
                TempData["PlaylistErrorMessage"] = "Плейлист не найден.";
                return NotFound();
            }

            if (!await CanEdit(playlist.User_ID))
            {
                TempData["PlaylistErrorMessage"] = "У вас нет доступа для удаления треков из этого плейлиста.";
                return Forbid();
            }

            var playlistTrack = await _context.PlaylistTracks
                .FirstOrDefaultAsync(pt => pt.Playlist_ID == playlistId && pt.Track_ID == trackId);
            if (playlistTrack == null)
            {
                TempData["PlaylistErrorMessage"] = "Трек не найден в плейлисте.";
                return NotFound();
            }

            try
            {
                _context.PlaylistTracks.Remove(playlistTrack);
                var remainingTracks = await _context.PlaylistTracks
                    .Where(pt => pt.Playlist_ID == playlistId && pt.TrackOrder > playlistTrack.TrackOrder)
                    .ToListAsync();
                foreach (var track in remainingTracks)
                {
                    track.TrackOrder--;
                }
                await _context.SaveChangesAsync();
                TempData["PlaylistSuccessMessage"] = "Трек успешно удален из плейлиста.";
                return RedirectToAction(nameof(Details), new { id = playlistId });
            }
            catch (DbUpdateException ex)
            {
                TempData["PlaylistErrorMessage"] = $"Произошла ошибка при удалении трека: {ex.InnerException?.Message ?? ex.Message}";
                return RedirectToAction(nameof(Details), new { id = playlistId });
            }
        }

        // GET: Playlists/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["PlaylistErrorMessage"] = "Гостям запрещено редактировать плейлисты.";
                return Forbid();
            }

            if (id == null)
            {
                TempData["PlaylistErrorMessage"] = "Плейлист не найден.";
                return NotFound();
            }

            var playlist = await _context.Playlists.FindAsync(id);
            if (playlist == null)
            {
                TempData["PlaylistErrorMessage"] = "Плейлист не найден.";
                return NotFound();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null)
            {
                TempData["PlaylistErrorMessage"] = "Пользователь не найден.";
                return Forbid();
            }

            ViewData["IsOwnerOrAdmin"] = await CanEdit(playlist.User_ID);
            return View(playlist);
        }

        // POST: Playlists/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,Name,IsPublic,Description")] Playlist playlist)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["PlaylistErrorMessage"] = "Гостям запрещено редактировать плейлисты.";
                return Forbid();
            }

            if (id != playlist.ID)
            {
                TempData["PlaylistErrorMessage"] = "Плейлист не найден.";
                return NotFound();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            if (user == null)
            {
                TempData["PlaylistErrorMessage"] = "Пользователь не найден.";
                return Forbid();
            }

            var existingPlaylist = await _context.Playlists.FindAsync(id);
            if (existingPlaylist == null)
            {
                TempData["PlaylistErrorMessage"] = "Плейлист не найден.";
                return NotFound();
            }

            bool isOwnerOrAdmin = await CanEdit(existingPlaylist.User_ID);
            ViewData["IsOwnerOrAdmin"] = isOwnerOrAdmin;

            playlist.User_ID = existingPlaylist.User_ID;
            playlist.CreatedDate = existingPlaylist.CreatedDate;

            ModelState.Remove("User");
            ModelState.Remove("User_ID");

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                TempData["PlaylistErrorMessage"] = string.Join("; ", errors);
                return View(playlist);
            }

            try
            {
                if (isOwnerOrAdmin)
                {
                    existingPlaylist.Name = playlist.Name;
                    existingPlaylist.IsPublic = playlist.IsPublic;
                    existingPlaylist.Description = playlist.Description;

                    if (playlist.IsPublic && !existingPlaylist.IsPublic)
                    {
                        existingPlaylist.IsPublic = false;
                        string changeDescription = $"Сделать плейлист '{playlist.Name}' публичным";
                        await CreatePublicProposalAsync("Playlists", playlist.ID, changeDescription, user.ID);
                    }
                    _context.Update(existingPlaylist);
                    await _context.SaveChangesAsync();
                    TempData["PlaylistSuccessMessage"] = $"Плейлист {playlist.Name} успешно обновлен.";
                }
                else
                {
                    string name = playlist.Name.Length > 30 ? playlist.Name.Substring(0, 30) : playlist.Name;
                    string description = playlist.Description != null && playlist.Description.Length > 50 ? playlist.Description.Substring(0, 50) : playlist.Description ?? "";
                    string proposedChange = $"Редактирование: Название={name}, Публичный={playlist.IsPublic}, Описание={description}";
                    await CreatePublicProposalAsync("Playlists", playlist.ID, proposedChange, user.ID);
                    TempData["PlaylistSuccessMessage"] = "Ваше предложение на редактирование успешно отправлено.";
                }
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                TempData["PlaylistErrorMessage"] = $"Произошла ошибка при сохранении плейлиста: {ex.InnerException?.Message ?? ex.Message}";
                return View(playlist);
            }
        }

        // GET: Playlists/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["PlaylistErrorMessage"] = "Гостям запрещено удалять плейлисты.";
                return Forbid();
            }

            if (id == null)
            {
                TempData["PlaylistErrorMessage"] = "Плейлист не найден.";
                return NotFound();
            }

            var playlist = await _context.Playlists
                .Include(p => p.User)
                .FirstOrDefaultAsync(m => m.ID == id);
            if (playlist == null)
            {
                TempData["PlaylistErrorMessage"] = "Плейлист не найден.";
                return NotFound();
            }

            if (!await CanEdit(playlist.User_ID))
            {
                TempData["PlaylistErrorMessage"] = "У вас нет доступа для удаления этого плейлиста.";
                return Forbid();
            }

            return View(playlist);
        }

        // POST: Playlists/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (User.IsInRole("Guest"))
            {
                TempData["PlaylistErrorMessage"] = "Гостям запрещено удалять плейлисты.";
                return Forbid();
            }

            var playlist = await _context.Playlists.FindAsync(id);
            if (playlist == null)
            {
                TempData["PlaylistErrorMessage"] = "Плейлист не найден.";
                return NotFound();
            }

            if (!await CanEdit(playlist.User_ID))
            {
                TempData["PlaylistErrorMessage"] = "У вас нет доступа для удаления этого плейлиста.";
                return Forbid();
            }

            try
            {
                _context.Playlists.Remove(playlist);
                await _context.SaveChangesAsync();
                TempData["PlaylistSuccessMessage"] = "Плейлист успешно удален.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                TempData["PlaylistErrorMessage"] = $"Произошла ошибка при удалении плейлиста: {ex.InnerException?.Message ?? ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
    }
}