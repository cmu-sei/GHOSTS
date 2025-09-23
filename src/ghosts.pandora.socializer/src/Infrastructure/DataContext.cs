using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Socializer.Infrastructure;

public class DataContext : DbContext
{
    protected readonly IConfiguration Configuration;

    public DataContext(IConfiguration configuration)
    {
        Configuration = configuration;
        Database.EnsureCreated();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        Directory.CreateDirectory($"{AppContext.BaseDirectory}/db");
        // Console.WriteLine($"Database here: {AppContext.BaseDirectory}socializer.db");
        // in memory database used for simplicity, change to a real db for production applications
        options.UseSqlite($"Data Source={AppContext.BaseDirectory}/db/socializer.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure relationships
        modelBuilder.Entity<Post>()
            .HasOne(p => p.User)
            .WithMany(u => u.Posts)
            .HasForeignKey(p => p.UserId);

        modelBuilder.Entity<Post>()
            .HasOne(p => p.Theme)
            .WithMany(t => t.Posts)
            .HasForeignKey(p => p.ThemeId);

        modelBuilder.Entity<Like>()
            .HasOne(l => l.Post)
            .WithMany(p => p.Likes)
            .HasForeignKey(l => l.PostId);

        modelBuilder.Entity<Like>()
            .HasOne(l => l.User)
            .WithMany(u => u.Likes)
            .HasForeignKey(l => l.UserId);

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Post)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.PostId);

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.User)
            .WithMany(u => u.Comments)
            .HasForeignKey(c => c.UserId);

        // Configure indexes for common queries
        modelBuilder.Entity<Post>()
            .HasIndex(p => new { p.ThemeId, p.CreatedUtc })
            .HasDatabaseName("IX_Post_Theme_CreatedUtc");

        modelBuilder.Entity<Post>()
            .HasIndex(p => new { p.UserId, p.ThemeId, p.CreatedUtc })
            .HasDatabaseName("IX_Post_User_Theme_CreatedUtc");

        modelBuilder.Entity<Like>()
            .HasIndex(l => new { l.UserId, l.PostId })
            .IsUnique()
            .HasDatabaseName("IX_Like_User_Post_Unique");

        // Seed data
        modelBuilder.Entity<Theme>().HasData(
            new Theme { Id = 1, Name = "facebook", DisplayName = "Facebook", Description = "Facebook-style social media theme", IsActive = true },
            new Theme { Id = 2, Name = "instagram", DisplayName = "Instagram", Description = "Instagram-style photo sharing theme", IsActive = true },
            new Theme { Id = 3, Name = "x", DisplayName = "X (Twitter)", Description = "X/Twitter-style microblogging theme", IsActive = true },
            new Theme { Id = 4, Name = "linkedin", DisplayName = "LinkedIn", Description = "LinkedIn-style professional networking theme", IsActive = true },
            new Theme { Id = 5, Name = "reddit", DisplayName = "Reddit", Description = "Reddit-style forum theme", IsActive = true },
            new Theme { Id = 6, Name = "youtube", DisplayName = "YouTube", Description = "YouTube-style video platform theme", IsActive = true },
            new Theme { Id = 7, Name = "discord", DisplayName = "Discord", Description = "Discord-style chat theme", IsActive = true }
        );

        base.OnModelCreating(modelBuilder);
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<Like> Likes { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<Theme> Themes { get; set; }
}

public class User
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(50)]
    public string Username { get; set; }

    [MaxLength(100)]
    public string DisplayName { get; set; }

    [MaxLength(500)]
    public string Bio { get; set; }

    [MaxLength(255)]
    public string Avatar { get; set; }

    [MaxLength(50)]
    public string Status { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime LastActiveUtc { get; set; }

    // Navigation properties
    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
    public virtual ICollection<Like> Likes { get; set; } = new List<Like>();
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
}

public class Theme
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } // facebook, instagram, x, etc.

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } // Facebook, Instagram, X (Twitter), etc.

    [MaxLength(500)]
    public string Description { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
}

public class Post
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string UserId { get; set; }

    [Required]
    public int ThemeId { get; set; }

    [Required]
    [MaxLength(5000)]
    public string Message { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }

    // Navigation properties
    public virtual User User { get; set; }
    public virtual Theme Theme { get; set; }
    public virtual ICollection<Like> Likes { get; set; } = new List<Like>();
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
}

public class Like
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; }

    [Required]
    public string PostId { get; set; }

    public DateTime CreatedUtc { get; set; }

    // Navigation properties
    public virtual User User { get; set; }
    public virtual Post Post { get; set; }
}

public class Comment
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; }

    [Required]
    public string PostId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Message { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }

    // Navigation properties
    public virtual User User { get; set; }
    public virtual Post Post { get; set; }
}
