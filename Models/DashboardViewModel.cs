namespace MusicCatalogWebApplication.Models
{
    public class DashboardViewModel
    {
        public int TrackCount { get; set; }
        public int PlaylistCount { get; set; }
        public int FavoriteCount { get; set; }
        public bool HasContent { get; set; }
        public List<RecentTrackViewModel> RecentTracks { get; set; } = new List<RecentTrackViewModel>();
    }
}
