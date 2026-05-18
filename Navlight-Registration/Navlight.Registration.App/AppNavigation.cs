namespace Navlight.Registration.App;

internal static class AppNavigation
{
    public static Action<AppMode>? SwitchMode { get; set; }
    public static int? LastSavedRegistrationTeamId { get; set; }
    public static int? PendingTagAssignmentTeamId { get; set; }
}
