using Ghosts.Socializer.Infrastructure;

namespace Ghosts.Socializer.Services;

public class DatabaseSeeder
{
    private readonly DataContext _context;
    private readonly IUserService _userService;
    private readonly IThemeService _themeService;
    private readonly IPostService _postService;

    public DatabaseSeeder(DataContext context, IUserService userService, IThemeService themeService, IPostService postService)
    {
        _context = context;
        _userService = userService;
        _themeService = themeService;
        _postService = postService;
    }

    public async Task SeedAsync()
    {
        // Create sample users
        var users = new List<User>();

        var usernames = new[] { "alice", "bob", "charlie", "diana", "eve", "frank", "grace", "henry", "ivy", "jack" };

        foreach (var username in usernames)
        {
            if (!await _userService.UsernameExistsAsync(username))
            {
                var user = await _userService.CreateUserAsync(
                    username,
                    $"{char.ToUpper(username[0])}{username.Substring(1)}"
                );
                users.Add(user);
            }
            else
            {
                users.Add(await _userService.GetUserByUsernameAsync(username));
            }
        }

        // Get all themes
        var themes = await _themeService.GetActiveThemesAsync();

        // Create sample posts for each theme
        var sampleMessages = new[]
        {
            "Just had an amazing day at the beach! 🌅",
            "Working on some exciting new projects 💻",
            "Beautiful sunset from my balcony tonight 🌇",
            "Coffee and coding - perfect morning combo ☕",
            "Excited to share my latest creation with you all! 🎨",
            "Weekend adventures are the best adventures 🏔️",
            "Learning something new every day 📚",
            "Great team meeting today - so much progress! 🚀",
            "Nature never fails to amaze me 🌿",
            "Celebrating small wins today 🎉"
        };

        var random = new Random();

        // Create posts for each theme
        foreach (var theme in themes)
        {
            for (int i = 0; i < 15; i++) // 15 posts per theme
            {
                var user = users[random.Next(users.Count)];
                var message = sampleMessages[random.Next(sampleMessages.Length)];

                // Add theme-specific context to messages
                message = theme.Name switch
                {
                    "facebook" => $"{message} #facebook #social",
                    "instagram" => $"{message} #photooftheday #instagram",
                    "x" => $"{message} #twitter #x",
                    "linkedin" => $"Professional update: {message} #linkedin #networking",
                    "reddit" => $"Thoughts on: {message} What do you think?",
                    "youtube" => $"New video idea: {message} #youtube #content",
                    "discord" => $"{message} Anyone want to chat about this?",
                    _ => message
                };

                await _postService.CreatePostAsync(user.Username, theme.Id, message);

                // Random delay to spread out creation times
                await Task.Delay(10);
            }
        }

        Console.WriteLine($"Seeded database with {users.Count} users, {themes.Count} themes, and {themes.Count * 15} posts");
    }
}
