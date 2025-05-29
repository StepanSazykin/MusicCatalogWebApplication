namespace MusicCatalogWebApplication.Models
{
    public class TrackSearchViewModel
    {
        public string SearchString { get; set; }
        public int? GenreId { get; set; }
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
        public string Tag { get; set; }
        public string SortOrder { get; set; }
        public string GroupBy { get; set; }
        public bool OnlyMine { get; set; }
    }
}
