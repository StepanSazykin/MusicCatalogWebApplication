using System.ComponentModel.DataAnnotations;

namespace MusicCatalogWebApplication.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Поле Логин обязательно для заполнения.")]
        [StringLength(32, ErrorMessage = "Логин должен содержать не более 32 символов.")]
        public string Login { get; set; }

        [EmailAddress(ErrorMessage = "Некорректный формат email.")]
        [StringLength(64, ErrorMessage = "Email должен содержать не более 64 символов.")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Поле Пароль обязательно для заполнения.")]
        [StringLength(255, ErrorMessage = "Пароль должен содержать не более 255 символов.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Поле Подтверждение пароля обязательно для заполнения.")]
        [Compare("Password", ErrorMessage = "Пароли не совпадают.")]
        public string ConfirmPassword { get; set; }
    }
}
