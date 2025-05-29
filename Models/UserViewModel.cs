namespace MusicCatalogWebApplication.Models
{
    public class UserViewModel
    {
        public int ID { get; set; }
        public string Login { get; set; }
        public string? Email { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime RegistrationDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public bool IsActive { get; set; }
    }
}
