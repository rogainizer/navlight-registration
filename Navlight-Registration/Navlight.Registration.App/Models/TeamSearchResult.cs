namespace Navlight.Registration.App.Models;

public sealed class TeamSearchResult
{
    public int TeamId { get; init; }
    public string TeamNumber { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool Registered { get; init; }

    public string DisplayText => $"{Name} ({TeamNumber}){(Registered ? " - registered" : string.Empty)}";
}
