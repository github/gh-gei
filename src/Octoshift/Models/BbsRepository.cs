namespace Octoshift.Models;

public record BbsRepository
{
    public string Id { get; init; }
    public string Name { get; init; }
    public ulong? Size { get; init; }
    public bool Archived { get; init; }
}
