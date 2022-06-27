namespace Octoshift.Models;

public record AdoRepository
{
    public string Id { get; init; }
    public string Name { get; init; }
    public uint Size { get; init; }
    public bool IsDisabled { get; init; }
}
