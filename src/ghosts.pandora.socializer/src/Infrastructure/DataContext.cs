using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Socializer.Infrastructure;

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

    public DbSet<Post> Posts { get; set; }
    public DbSet<Like> Likes { get; set; }
}

public class Like
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string UserId { get; set; }
    public string PostId { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class Post
{
    [Key]
    public string Id { get; set; }
    public string User { get; set; }
    public string Message { get; set; }
    public IList<Like> Likes { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class User
{
    public string Name { get; set; }
    public string Avatar { get; set; }
    public string Status { get; set; }
}
