namespace Navlight.Registration.App;

internal static class NavlightBranding
{
    private static readonly Image Logo = LoadLogo();
    private static readonly Icon WindowIcon = LoadWindowIcon();

    public static Image AppLogo => Logo;

    public static void ApplyWindowIcon(Form form)
    {
        form.Icon = WindowIcon;
    }

    private static Image LoadLogo()
    {
        using var stream = typeof(NavlightBranding).Assembly.GetManifestResourceStream(
            "Navlight.Registration.App.rogainizer.png")
            ?? throw new InvalidOperationException("The Navlight logo resource could not be found.");
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private static Icon LoadWindowIcon()
    {
        using var stream = typeof(NavlightBranding).Assembly.GetManifestResourceStream(
            "Navlight.Registration.App.rogainizer.ico")
            ?? throw new InvalidOperationException("The Navlight window icon resource could not be found.");
        return new Icon(stream);
    }
}
