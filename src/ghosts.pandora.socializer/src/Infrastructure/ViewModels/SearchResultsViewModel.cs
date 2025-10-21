namespace Ghosts.Socializer.Infrastructure.ViewModels;

using System;
using Ghosts.Socializer.Infrastructure;

public class SearchResultsViewModel
{
    public string Query { get; set; }
    public string Theme { get; set; }
    public IReadOnlyList<User> Users { get; set; } = Array.Empty<User>();
    public IReadOnlyList<Post> Posts { get; set; } = Array.Empty<Post>();

    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);
}
