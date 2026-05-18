namespace Navlight.Registration.App.Models;

public sealed class TeamTagAssignment
{
    public int TeamId { get; init; }
    public string TeamNumber { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string CourseName { get; init; } = string.Empty;
    public bool Registered { get; init; }
    public DateTime? RegisteredAt { get; init; }
    public DateTime LastUpdatedAt { get; init; }
    public List<string> Competitors { get; } = [];
    public List<string> TagCodes { get; } = [];
}
