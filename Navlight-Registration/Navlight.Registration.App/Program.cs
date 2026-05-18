namespace Navlight.Registration.App;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var modeSelectionForm = new ModeSelectionForm();
        if (modeSelectionForm.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        Application.Run(new NavlightApplicationContext(modeSelectionForm.SelectedMode));
    }
}
