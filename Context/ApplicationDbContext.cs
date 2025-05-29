using Microsoft.EntityFrameworkCore;
using MusicCatalogWebApplication.Models;

namespace MusicCatalogWebApplication.Context
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> contextOptions) : base(contextOptions)
        {

        }

        public DbSet<Genre> Genres { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Artist> Artists { get; set; }
        public DbSet<Album> Albums { get; set; }
        public DbSet<Track> Tracks { get; set; }
        public DbSet<Playlist> Playlists { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<PlaylistTrack> PlaylistTracks { get; set; }
        public DbSet<FavoriteArtist> FavoriteArtists { get; set; }
        public DbSet<FavoriteAlbum> FavoriteAlbums { get; set; }
        public DbSet<FavoriteTrack> FavoriteTracks { get; set; }
        public DbSet<TrackTag> TrackTags { get; set; }
        public DbSet<EditProposal> EditProposals { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Настройка имен таблиц
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<Artist>().ToTable("Artists");
            modelBuilder.Entity<Album>().ToTable("Albums");
            modelBuilder.Entity<Track>().ToTable("Tracks");
            modelBuilder.Entity<Playlist>().ToTable("Playlists");
            modelBuilder.Entity<PlaylistTrack>().ToTable("Playlist_Tracks");
            modelBuilder.Entity<Genre>().ToTable("Genres");
            modelBuilder.Entity<FavoriteArtist>().ToTable("Favorite_Artists");
            modelBuilder.Entity<FavoriteAlbum>().ToTable("Favorite_Albums");
            modelBuilder.Entity<FavoriteTrack>().ToTable("Favorite_Tracks");
            modelBuilder.Entity<EditProposal>().ToTable("Edit_Proposals");
            modelBuilder.Entity<Tag>().ToTable("Tags");
            modelBuilder.Entity<TrackTag>().ToTable("Track_Tags");

            // Настройка составных ключей
            modelBuilder.Entity<FavoriteArtist>()
                .HasKey(fa => new { fa.User_ID, fa.Artist_ID });

            modelBuilder.Entity<FavoriteAlbum>()
                .HasKey(fa => new { fa.User_ID, fa.Album_ID });

            modelBuilder.Entity<FavoriteTrack>()
                .HasKey(ft => new { ft.User_ID, ft.Track_ID });

            modelBuilder.Entity<TrackTag>()
                .HasKey(tt => new { tt.Track_ID, tt.Tag_ID });

            modelBuilder.Entity<PlaylistTrack>()
                .HasKey(pt => pt.ID);

            // Настройка уникальных индексов
            modelBuilder.Entity<Artist>()
                .HasIndex(a => new { a.Name, a.Owner_ID })
                .IsUnique();

            modelBuilder.Entity<Album>()
                .HasIndex(a => new { a.Title, a.Artist_ID })
                .IsUnique();

            modelBuilder.Entity<Track>()
                .HasIndex(t => new { t.Title, t.Album_ID })
                .IsUnique();

            modelBuilder.Entity<PlaylistTrack>()
                .HasIndex(pt => new { pt.Playlist_ID, pt.TrackOrder })
                .IsUnique();

            // Настройка ограничений для EditProposals
            modelBuilder.Entity<EditProposal>()
                .Property(ep => ep.TableName)
                .HasMaxLength(32);

            modelBuilder.Entity<EditProposal>()
                .Property(ep => ep.Status)
                .HasMaxLength(16);
        }
    }
}
