using Navlight.Registration.App.Models;
using Navlight.Registration.App.Services;

namespace Navlight.Registration.App;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        EnsureLocalMySqlIsRunning();

        try
        {
            using var modeSelectionForm = new ModeSelectionForm();
            if (modeSelectionForm.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            Application.Run(new NavlightApplicationContext(modeSelectionForm.SelectedMode));
        }
        finally
        {
            MySqlAutoStarter.StopIfStarted();
        }
    }

    private static void EnsureLocalMySqlIsRunning()
    {
        try
        {
            var databaseOptions = DatabaseOptions.Load();
            MySqlAutoStarter.EnsureStartedIfNeeded(databaseOptions);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Navlight could not start local MySQL automatically." + Environment.NewLine + Environment.NewLine +
                ex.Message + Environment.NewLine + Environment.NewLine +
                "If this computer should host MySQL, start it using the Navlight Start MySQL shortcut and retry.",
                "MySQL Startup Warning",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
