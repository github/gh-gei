namespace Octoshift.Models;

public record GitlabProject
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Slug { get; init; }
}
