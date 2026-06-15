namespace Navlight.Registration.App.Models;

public sealed class AdminTeamOverviewRow
{
    public int TeamId { get; init; }
    public string TeamNumber { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string CourseName { get; init; } = string.Empty;
    public string Competitors { get; init; } = string.Empty;
    public string Tags { get; init; } = string.Empty;
    public bool FlightPlan { get; init; }
    public string Status { get; init; } = string.Empty;
}
