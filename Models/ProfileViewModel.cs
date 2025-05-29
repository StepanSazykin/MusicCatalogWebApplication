using System.ComponentModel.DataAnnotations;

namespace MusicCatalogWebApplication.Models
{
    public class ProfileViewModel
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "Логин обязателен.")]
        [StringLength(32, ErrorMessage = "Логин не должен превышать 32 символа.")]
        public string Login { get; set; }

        [StringLength(64, ErrorMessage = "Email не должен превышать 64 символа.")]
        [EmailAddress(ErrorMessage = "Некорректный формат email.")]
        public string? Email { get; set; }

        [DataType(DataType.Password)]
        [StringLength(255, ErrorMessage = "Пароль не должен превышать 255 символов.")]
        [MinLength(8, ErrorMessage = "Пароль должен содержать минимум 8 символов.")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Пароли не совпадают.")]
        public string? ConfirmPassword { get; set; }

        [DataType(DataType.Password)]
        [Required(ErrorMessage = "Текущий пароль обязателен для изменений.")]
        public string CurrentPassword { get; set; }
    }
}
