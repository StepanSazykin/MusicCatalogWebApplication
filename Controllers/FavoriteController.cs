using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicCatalogWebApplication.Context;
using MusicCatalogWebApplication.Models;
using System.Security.Claims;

namespace MusicCatalogWebApplication.Controllers
{
    [Authorize]
    public class FavoriteController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FavoriteController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Favorite
        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Guest"))
            {
                TempData["ErrorMessage"] = "Гостям запрещен доступ к избранному.";
                return Forbid();
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var favoriteArtists = await _context.FavoriteArtists
                .Where(fa => fa.User_ID == userId)
                .Include(fa => fa.Artist)
                .Select(fa => new { fa.Artist.ID, fa.Artist.Name })
                .ToListAsync();

            var favoriteAlbums = await _context.FavoriteAlbums
                .Where(fa => fa.User_ID == userId)
                .Include(fa => fa.Album).ThenInclude(a => a.Artist)
                .Select(fa => new { fa.Album.ID, Title = fa.Album.Title, ArtistName = fa.Album.Artist.Name })
                .ToListAsync();

            var favoriteTracks = await _context.FavoriteTracks
                .Where(ft => ft.User_ID == userId)
                .Include(ft => ft.Track).ThenInclude(t => t.Album).ThenInclude(a => a.Artist)
                .Select(ft => new
                {
                    ft.Track.ID,
                    ft.Track.Title,
                    ArtistName = ft.Track.Album.Artist.Name,
                    AlbumTitle = ft.Track.Album.Title
                })
                .ToListAsync();

            var model = new FavoriteViewModel
            {
                Artists = favoriteArtists.Select(a => new FavoriteItemViewModel { Id = a.ID, Name = a.Name }).ToList(),
                Albums = favoriteAlbums.Select(a => new FavoriteItemViewModel { Id = a.ID, Name = $"{a.Title} - {a.ArtistName}" }).ToList(),
                Tracks = favoriteTracks.Select(t => new FavoriteItemViewModel { Id = t.ID, Name = $"{t.Title} - {t.ArtistName} ({t.AlbumTitle})" }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AddFavoriteArtist(int artistId, string returnUrl = null)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (!_context.FavoriteArtists.Any(f => f.User_ID == userId && f.Artist_ID == artistId))
            {
                _context.FavoriteArtists.Add(new FavoriteArtist { User_ID = userId, Artist_ID = artistId });
                await _context.SaveChangesAsync();
                TempData["ArtistSuccessMessage"] = $"Артист успешно добавлен в избранное.";
                TempData["MessageType"] = "success";
            }
            else
            {
                TempData["ArtistSuccessMessage"] = "Артист уже в избранном.";
                TempData["MessageType"] = "warning";
            }
            return Redirect(returnUrl ?? Url.Action("Index", "Favorite"));
        }

        [HttpPost]
        public async Task<IActionResult> AddFavoriteAlbum(int albumId, string returnUrl = null)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (!_context.FavoriteAlbums.Any(f => f.User_ID == userId && f.Album_ID == albumId))
            {
                _context.FavoriteAlbums.Add(new FavoriteAlbum { User_ID = userId, Album_ID = albumId });
                await _context.SaveChangesAsync();
                TempData["AlbumSuccessMessage"] = "Альбом успешно добавлен в избранное.";
                TempData["MessageType"] = "success";
            }
            else
            {
                TempData["AlbumSuccessMessage"] = "Альбом уже в избранном.";
                TempData["MessageType"] = "warning";
            }
            return Redirect(returnUrl ?? Url.Action("Index", "Favorite"));
        }

        [HttpPost]
        public async Task<IActionResult> AddFavoriteTrack(int trackId, string returnUrl = null)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (!_context.FavoriteTracks.Any(f => f.User_ID == userId && f.Track_ID == trackId))
            {
                _context.FavoriteTracks.Add(new FavoriteTrack { User_ID = userId, Track_ID = trackId });
                await _context.SaveChangesAsync();
                TempData["TrackSuccessMessage"] = "Трек успешно добавлен в избранное.";
                TempData["MessageType"] = "success";
            }
            else
            {
                TempData["TrackSuccessMessage"] = "Трек уже в избранном.";
                TempData["MessageType"] = "warning";
            }
            return Redirect(returnUrl ?? Url.Action("Index", "Favorite"));
        }
    }
}
