namespace Navlight.Registration.App.Models;

public sealed class CourseOption
{
    public int CourseId { get; init; }
    public string Name { get; init; } = string.Empty;

    public override string ToString() => Name;
}