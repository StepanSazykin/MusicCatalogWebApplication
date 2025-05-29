using System.ComponentModel.DataAnnotations;

namespace MusicCatalogWebApplication.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Поле Логин обязательно для заполнения.")]
        [StringLength(32, ErrorMessage = "Логин должен содержать не более 32 символов.")]
        public string Login { get; set; }

        [Required(ErrorMessage = "Поле Пароль обязательно для заполнения.")]
        [StringLength(255, ErrorMessage = "Пароль должен содержать не более 255 символов.")]
        public string Password { get; set; }

        public string ReturnUrl { get; set; }
    }
}