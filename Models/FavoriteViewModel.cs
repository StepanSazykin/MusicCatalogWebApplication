namespace MusicCatalogWebApplication.Models
{
    public class FavoriteViewModel
    {
        public List<FavoriteItemViewModel> Artists { get; set; } = new List<FavoriteItemViewModel>();
        public List<FavoriteItemViewModel> Albums { get; set; } = new List<FavoriteItemViewModel>();
        public List<FavoriteItemViewModel> Tracks { get; set; } = new List<FavoriteItemViewModel>();
    }

    public class FavoriteItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}