using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicCatalogWebApplication.Context;
using MusicCatalogWebApplication.Models;

namespace MusicCatalogWebApplication.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        // GET: /
        public async Task<IActionResult> Index()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            int? userId = null;
            if (!User.IsInRole("Guest"))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
                if (user != null)
                {
                    userId = user.ID;
                }
            }

            var model = new DashboardViewModel
            {
                TrackCount = await _context.Tracks.CountAsync(t => t.IsPublic),
                PlaylistCount = userId.HasValue
                    ? await _context.Playlists.CountAsync(p => p.IsPublic || p.User_ID == userId)
                    : await _context.Playlists.CountAsync(p => p.IsPublic),
                FavoriteCount = userId.HasValue
                    ? await _context.FavoriteArtists.CountAsync(fa => fa.User_ID == userId.Value) +
                      await _context.FavoriteAlbums.CountAsync(fa => fa.User_ID == userId.Value) +
                      await _context.FavoriteTracks.CountAsync(ft => ft.User_ID == userId.Value)
                    : 0,
                RecentTracks = await _context.Tracks
                    .Where(t => t.IsPublic)
                    .Include(t => t.Album).ThenInclude(a => a.Artist)
                    .OrderByDescending(t => t.ID)
                    .Take(5)
                    .Select(t => new RecentTrackViewModel
                    {
                        TrackId = t.ID,
                        Title = t.Title,
                        ArtistName = t.Album.Artist.Name
                    })
                    .ToListAsync()
            };

            model.HasContent = model.TrackCount > 0 || model.PlaylistCount > 0 || model.FavoriteCount > 0;
            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}