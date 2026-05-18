namespace Navlight.Registration.App.Models;

public sealed class CategoryOption
{
    public int CategoryId { get; init; }
    public string Name { get; init; } = string.Empty;

    public override string ToString() => Name;
}
