using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace MusicCatalogWebApplication.Models
{
    public class Genre
    {
        [Key]
        public int ID { get; set; }

        [Required]
        [StringLength(32)]
        public string Name { get; set; } = null!;

        public ICollection<Album> Albums { get; set; } = new List<Album>();
    }

    public class User
    {
        [Key]
        public int ID { get; set; }

        [Required]
        [StringLength(32)]
        public string Login { get; set; } = null!;

        [Required]
        [StringLength(255)]
        public string Password { get; set; } = null!;

        [StringLength(64)]
        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        public bool IsAdmin { get; set; } = false;

        [Required]
        public DateTime RegistrationDate { get; set; }

        public DateTime? LastLoginDate { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

        public ICollection<Artist> OwnedArtists { get; set; } = new List<Artist>();
        public ICollection<Album> OwnedAlbums { get; set; } = new List<Album>();
        public ICollection<Track> OwnedTracks { get; set; } = new List<Track>();
        public ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();
        public ICollection<FavoriteArtist> FavoriteArtists { get; set; } = new List<FavoriteArtist>();
        public ICollection<FavoriteAlbum> FavoriteAlbums { get; set; } = new List<FavoriteAlbum>();
        public ICollection<FavoriteTrack> FavoriteTracks { get; set; } = new List<FavoriteTrack>();
        public ICollection<EditProposal> EditProposals { get; set; } = new List<EditProposal>();
    }

    public class Artist
    {
        [Key]
        public int ID { get; set; }

        [Required(ErrorMessage = "Поле Название обязательно для заполнения.")]
        [StringLength(64, ErrorMessage = "Название должно содержать не более 64 символов.")]
        [Display(Name = "Название")]
        public string Name { get; set; } = null!;

        [Display(Name = "Владелец")]
        public int Owner_ID { get; set; }

        [Display(Name = "Публичный")]
        public bool IsPublic { get; set; } = false;

        [StringLength(64, ErrorMessage = "Страна должна содержать не более 64 символов.")]
        [Display(Name = "Страна")]
        public string? Country { get; set; }

        [Range(1900, 2025, ErrorMessage = "Год создания должен быть в диапазоне от 1900 до 2025.")]
        [Display(Name = "Год создания")]
        public short? CreationYear { get; set; }

        [ForeignKey("Owner_ID")]
        [ValidateNever]
        public User Owner { get; set; } = null!;

        public ICollection<Album> Albums { get; set; } = new List<Album>();
        public ICollection<FavoriteArtist> FavoriteArtists { get; set; } = new List<FavoriteArtist>();
    }

    public class Album
    {
        [Key]
        public int ID { get; set; }

        [Required(ErrorMessage = "Поле Название обязательно для заполнения.")]
        [StringLength(64, ErrorMessage = "Название должно содержать не более 64 символов.")]
        [Display(Name = "Название")]
        public string Title { get; set; } = null!;

        [Display(Name = "Артист")]
        public int Artist_ID { get; set; }

        [Display(Name = "Дата выпуска")]
        public DateTime? ReleaseDate { get; set; }

        [Display(Name = "Жанр")]
        public int Genre_ID { get; set; }

        public int Owner_ID { get; set; }

        [Display(Name = "Публичный")]
        public bool IsPublic { get; set; } = false;

        [ForeignKey("Artist_ID")]
        [ValidateNever]
        public Artist Artist { get; set; } = null!;

        [ForeignKey("Genre_ID")]
        [ValidateNever]
        public Genre Genre { get; set; } = null!;

        [ForeignKey("Owner_ID")]
        [ValidateNever]
        public User Owner { get; set; } = null!;

        public ICollection<Track> Tracks { get; set; } = new List<Track>();
        public ICollection<FavoriteAlbum> FavoriteAlbums { get; set; } = new List<FavoriteAlbum>();
    }

    public class Track
    {
        [Key]
        public int ID { get; set; }

        [Required(ErrorMessage = "Название трека обязательно")]
        [StringLength(64, ErrorMessage = "Название трека не может превышать 64 символа")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Выберите альбом")]
        public int Album_ID { get; set; }

        public short Duration { get; set; }

        public int Owner_ID { get; set; }

        [Required]
        public bool IsPublic { get; set; } = false;

        [ForeignKey("Album_ID")]
        [ValidateNever]
        public Album Album { get; set; } = null!;

        [ForeignKey("Owner_ID")]
        [ValidateNever]
        public User Owner { get; set; } = null!;

        public ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
        public ICollection<FavoriteTrack> FavoriteTracks { get; set; } = new List<FavoriteTrack>();
        public ICollection<TrackTag> TrackTags { get; set; } = new List<TrackTag>();
    }

    public class Playlist
    {
        [Key]
        public int ID { get; set; }

        [Required]
        [StringLength(64)]
        public string Name { get; set; } = null!;

        [Column("User_ID")]
        public int User_ID { get; set; }

        [Required]
        public bool IsPublic { get; set; } = false;

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(255)]
        public string? Description { get; set; }

        [ForeignKey("User_ID")]
        public User User { get; set; }

        public ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
    }

    public class Tag
    {
        [Key]
        public int ID { get; set; }

        [Required]
        [StringLength(32)]
        public string Name { get; set; } = null!;

        public ICollection<TrackTag> TrackTags { get; set; } = new List<TrackTag>();
    }

    public class PlaylistTrack
    {
        [Key]
        public int ID { get; set; }

        public int Playlist_ID { get; set; }

        public int Track_ID { get; set; }

        [Required]
        public short TrackOrder { get; set; }

        [ForeignKey("Playlist_ID")]
        public Playlist Playlist { get; set; }

        [ForeignKey("Track_ID")]
        public Track Track { get; set; }
    }

    public class FavoriteArtist
    {
        public int User_ID { get; set; }

        public int Artist_ID { get; set; }

        [ForeignKey("User_ID")]
        public User User { get; set; } = null!;

        [ForeignKey("Artist_ID")]
        public Artist Artist { get; set; } = null!;
    }

    public class FavoriteAlbum
    {
        public int User_ID { get; set; }

        public int Album_ID { get; set; }

        [ForeignKey("User_ID")]
        public User User { get; set; } = null!;

        [ForeignKey("Album_ID")]
        public Album Album { get; set; } = null!;
    }

    public class FavoriteTrack
    {
        public int User_ID { get; set; }

        public int Track_ID { get; set; }

        [ForeignKey("User_ID")]
        public User User { get; set; } = null!;

        [ForeignKey("Track_ID")]
        public Track Track { get; set; } = null!;
    }

    public class TrackTag
    {
        public int Track_ID { get; set; }

        public int Tag_ID { get; set; }

        [ForeignKey("Track_ID")]
        public Track Track { get; set; } = null!;

        [ForeignKey("Tag_ID")]
        public Tag Tag { get; set; } = null!;
    }

    public class EditProposal
    {
        [Key]
        public int ID { get; set; }

        public int User_ID { get; set; }

        [Required]
        [StringLength(32)]
        public string TableName { get; set; } = null!;

        public int Record_ID { get; set; }

        [Required]
        [StringLength(128)]
        public string ProposedChange { get; set; } = null!;

        [Required]
        [StringLength(16)]
        public string Status { get; set; } = null!;

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [ForeignKey("User_ID")]
        public User User { get; set; } = null!;
    }
}
