namespace Navlight.Registration.App.Models;

public sealed class TeamRegistration
{
    public int TeamId { get; init; }
    public int EventId { get; init; }
    public string TeamNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public int CourseId { get; set; }
    public bool Registered { get; set; }
    public DateTime? RegisteredAt { get; set; }
    public bool FlightPlan { get; set; }
    public DateTime? FlightPlanAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public List<CompetitorRecord> Competitors { get; set; } = [];
    public List<string> TagCodes { get; set; } = [];
}
