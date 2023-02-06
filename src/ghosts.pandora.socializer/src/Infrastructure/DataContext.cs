using Microsoft.EntityFrameworkCore;

namespace Socializer.Infrastructure;

public class DataContext: DbContext
{
    protected readonly IConfiguration Configuration;

    public DataContext(IConfiguration configuration)
    {
        Configuration = configuration;
        Database.EnsureCreated();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        // Console.WriteLine($"Database here: {AppContext.BaseDirectory}socializer.db");
        // in memory database used for simplicity, change to a real db for production applications
        options.UseSqlite($"Data Source={AppContext.BaseDirectory}socializer.db");
        
    }

    public DbSet<Post> Posts
    {
        get;
        set;
    }
}

public class Post
{
    public string Id { get; set; }
    public string User { get; set; }
    public string Message { get; set; }
    public DateTime CreatedUtc { get; set; }
}
