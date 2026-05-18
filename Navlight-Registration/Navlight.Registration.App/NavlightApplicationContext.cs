namespace Navlight.Registration.App;

internal sealed class NavlightApplicationContext : ApplicationContext
{
    public NavlightApplicationContext(AppMode initialMode)
    {
        AppNavigation.SwitchMode = SwitchMode;
        SwitchMode(initialMode);
    }

    private void SwitchMode(AppMode mode)
    {
        Form nextForm;
        if (mode == AppMode.Registration)
        {
            nextForm = new MainForm();
        }
        else if (mode == AppMode.RegistrationAndTagAssignment)
        {
            nextForm = new RegistrationAndTagAssignmentForm();
        }
        else if (mode == AppMode.Admin)
        {
            nextForm = new AdminForm();
        }
        else
        {
            var initialTeamId = AppNavigation.PendingTagAssignmentTeamId;
            AppNavigation.PendingTagAssignmentTeamId = null;
            nextForm = new TagAssignmentForm(initialTeamId);
        }

        nextForm.FormClosed += ActiveFormClosed;

        var previousForm = MainForm;
        MainForm = nextForm;
        nextForm.Show();

        if (previousForm is null)
        {
            return;
        }

        previousForm.FormClosed -= ActiveFormClosed;
        previousForm.Close();
        previousForm.Dispose();
    }

    private void ActiveFormClosed(object? sender, FormClosedEventArgs e)
    {
        if (sender == MainForm)
        {
            ExitThread();
        }
    }
}
