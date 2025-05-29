using System.ComponentModel.DataAnnotations;

namespace MusicCatalogWebApplication.Models
{
    public class TrackViewModel
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "Поле Название обязательно для заполнения.")]
        [StringLength(64, ErrorMessage = "Название должно содержать не более 64 символов.")]
        [Display(Name = "Название")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Выберите альбом.")]
        [Display(Name = "Альбом")]
        public int Album_ID { get; set; }

        [Required(ErrorMessage = "Длительность обязательна для заполнения.")]
        [RegularExpression(@"^\d{1,2}:\d{2}$", ErrorMessage = "Длительность должна быть в формате MM:SS (например, 03:45)")]
        public string DurationString { get; set; }

        public short Duration { get; set; }

        [Display(Name = "Публичный")]
        public bool IsPublic { get; set; } = false;

        [Display(Name = "Теги")]
        [Required(ErrorMessage = "Необходимо выбрать хотя бы один тег.")]
        public List<int> SelectedTagIds { get; set; } = new List<int>();

        public bool CanEditDirectly { get; set; }
    }
}