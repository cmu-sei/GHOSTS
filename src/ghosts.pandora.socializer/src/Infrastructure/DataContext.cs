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
            .HasForeignKey(p => p.Username);


        modelBuilder.Entity<Like>()
            .HasOne(l => l.Post)
            .WithMany(p => p.Likes)
            .HasForeignKey(l => l.PostId);

        modelBuilder.Entity<Like>()
            .HasOne(l => l.User)
            .WithMany(u => u.Likes)
            .HasForeignKey(l => l.Username);

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Post)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.PostId);

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.User)
            .WithMany(u => u.Comments)
            .HasForeignKey(c => c.Username);

        modelBuilder.Entity<DirectMessage>()
            .HasOne(dm => dm.FromUser)
            .WithMany(u => u.SentMessages)
            .HasForeignKey(dm => dm.FromUsername);

        modelBuilder.Entity<DirectMessage>()
            .HasOne(dm => dm.ToUser)
            .WithMany(u => u.ReceivedMessages)
            .HasForeignKey(dm => dm.ToUsername);


        // Configure indexes for common queries
        modelBuilder.Entity<Post>()
            .HasIndex(p => new { p.Theme, p.CreatedUtc })
            .HasDatabaseName("IX_Post_Theme_CreatedUtc");

        modelBuilder.Entity<Post>()
            .HasIndex(p => new { p.Username, p.Theme, p.CreatedUtc })
            .HasDatabaseName("IX_Post_User_Theme_CreatedUtc");

        modelBuilder.Entity<Like>()
            .HasIndex(l => new { l.Username, l.PostId })
            .IsUnique()
            .HasDatabaseName("IX_Like_User_Post_Unique");

        modelBuilder.Entity<DirectMessage>()
            .HasIndex(dm => new { dm.ToUsername, dm.CreatedUtc })
            .HasDatabaseName("IX_DirectMessage_ToUser_CreatedUtc");

        modelBuilder.Entity<DirectMessage>()
            .HasIndex(dm => new { dm.FromUsername, dm.CreatedUtc })
            .HasDatabaseName("IX_DirectMessage_FromUser_CreatedUtc");



        base.OnModelCreating(modelBuilder);
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<Like> Likes { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<DirectMessage> DirectMessages { get; set; }
}

public class User
{
    [Key]
    [Required]
    [MaxLength(50)]
    public string Username { get; set; }

    [MaxLength(500)]
    public string Bio { get; set; }

    [MaxLength(255)]
    public string Avatar { get; set; }

    [MaxLength(50)]
    public string Status { get; set; }

    [MaxLength(50)]
    public string Theme { get; set; } = "default";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
    public virtual ICollection<Like> Likes { get; set; } = new List<Like>();
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public virtual ICollection<DirectMessage> SentMessages { get; set; } = new List<DirectMessage>();
    public virtual ICollection<DirectMessage> ReceivedMessages { get; set; } = new List<DirectMessage>();
}


public class Post
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Username { get; set; }

    [Required]
    [MaxLength(5000)]
    public string Message { get; set; }

    [MaxLength(50)]
    public string Theme { get; set; } = "default";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; }
    public virtual ICollection<Like> Likes { get; set; } = new List<Like>();
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
}

public class Like
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string Username { get; set; }

    [Required]
    public Guid PostId { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; }
    public virtual Post Post { get; set; }
}

public class Comment
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string Username { get; set; }

    [Required]
    public Guid PostId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Message { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; }
    public virtual Post Post { get; set; }
}

public class DirectMessage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string FromUsername { get; set; }

    [Required]
    [MaxLength(50)]
    public string ToUsername { get; set; }


    [Required]
    [MaxLength(2000)]
    public string Message { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadUtc { get; set; }

    public virtual User FromUser { get; set; }
    public virtual User ToUser { get; set; }
}
