using Ghosts.Pandora.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Pandora.Infrastructure;

public class DataContext : DbContext
{
    public DataContext(DbContextOptions<DataContext> options) : base(options)
    {
        Database.EnsureCreated();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure relationships
        modelBuilder.Entity<Post>()
            .HasOne(p => p.User)
            .WithMany(u => u.Posts)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);


        modelBuilder.Entity<Like>()
            .HasOne(l => l.Post)
            .WithMany(p => p.Likes)
            .HasForeignKey(l => l.PostId);

        modelBuilder.Entity<Like>()
            .HasOne(l => l.User)
            .WithMany(u => u.Likes)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Post)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.PostId);

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.User)
            .WithMany(u => u.Comments)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DirectMessage>()
            .HasOne(dm => dm.FromUser)
            .WithMany(u => u.SentMessages)
            .HasForeignKey(dm => dm.FromUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DirectMessage>()
            .HasOne(dm => dm.ToUser)
            .WithMany(u => u.ReceivedMessages)
            .HasForeignKey(dm => dm.ToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Followers>()
            .HasKey(f => new { f.UserId, f.FollowerUserId });

        modelBuilder.Entity<Followers>()
            .HasOne(f => f.Followee)
            .WithMany(u => u.Followers)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Followers>()
            .HasOne(f => f.Follower)
            .WithMany(u => u.Following)
            .HasForeignKey(f => f.FollowerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure indexes for common queries
        modelBuilder.Entity<Post>()
            .HasIndex(p => new { p.Theme, p.CreatedUtc })
            .HasDatabaseName("IX_Post_Theme_CreatedUtc");

        modelBuilder.Entity<Post>()
            .HasIndex(p => new { p.UserId, p.Theme, p.CreatedUtc })
            .HasDatabaseName("IX_Post_User_Theme_CreatedUtc");

        modelBuilder.Entity<Like>()
            .HasIndex(l => new { l.UserId, l.PostId })
            .IsUnique()
            .HasDatabaseName("IX_Like_User_Post_Unique");

        modelBuilder.Entity<DirectMessage>()
            .HasIndex(dm => new { dm.ToUserId, dm.CreatedUtc })
            .HasDatabaseName("IX_DirectMessage_ToUser_CreatedUtc");

        modelBuilder.Entity<DirectMessage>()
            .HasIndex(dm => new { dm.FromUserId, dm.CreatedUtc })
            .HasDatabaseName("IX_DirectMessage_FromUser_CreatedUtc");

        modelBuilder.Entity<Followers>()
            .HasIndex(f => new { f.UserId, f.FollowerUserId })
            .IsUnique()
            .HasDatabaseName("IX_Followers_User_Follower");

        modelBuilder.Entity<Followers>()
            .HasIndex(f => f.UserId)
            .HasDatabaseName("IX_Followers_User");

        modelBuilder.Entity<Followers>()
            .HasIndex(f => f.FollowerUserId)
            .HasDatabaseName("IX_Followers_Follower");

        modelBuilder.Entity<User>()
            .HasIndex(u => new { u.Username, u.Theme })
            .IsUnique()
            .HasDatabaseName("IX_User_Username_Theme_Unique");

        base.OnModelCreating(modelBuilder);
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<Like> Likes { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<DirectMessage> DirectMessages { get; set; }
    public DbSet<Followers> Followers { get; set; }
    public DbSet<LawEnforcementRequest> LawEnforcementRequests { get; set; }
}
