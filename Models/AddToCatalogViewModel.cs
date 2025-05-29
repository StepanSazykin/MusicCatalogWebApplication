using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MusicCatalogWebApplication.Models
{
    public class AddToCatalogViewModel
    {
        [Required(ErrorMessage = "Выберите тип сущности.")]
        public string EntityType { get; set; }

        // Поля для артиста
        [StringLength(64, ErrorMessage = "Имя не должно превышать 64 символа.")]
        public string ArtistName { get; set; }

        // Поля для альбома
        [StringLength(128, ErrorMessage = "Название не должно превышать 128 символов.")]
        public string AlbumTitle { get; set; }
        public int? ArtistId { get; set; }
        public int? GenreId { get; set; }
        public DateTime? ReleaseDate { get; set; }

        // Поля для трека
        [StringLength(128, ErrorMessage = "Название не должно превышать 128 символов.")]
        public string TrackTitle { get; set; }
        public int? AlbumId { get; set; }
        public int Duration { get; set; }
        public bool IsPublic { get; set; }

        // Списки для выпадающих меню
        public SelectList Artists { get; set; }
        public SelectList Genres { get; set; }
        public SelectList Albums { get; set; }
    }
}