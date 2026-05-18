namespace Navlight.Registration.App;

public enum AppMode
{
    Registration,
    TagAssignment,
    RegistrationAndTagAssignment,
    Admin
}

public sealed class ModeSelectionForm : Form
{
    public AppMode SelectedMode { get; private set; } = AppMode.Registration;

    public ModeSelectionForm()
    {
        Text = "Navlight";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 350);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(20)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headingLabel = new Label
        {
            Text = "Open Navlight in:",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12)
        };
        layout.Controls.Add(headingLabel, 0, 0);

        var registrationButton = new Button
        {
            Text = "Registration",
            Dock = DockStyle.Fill,
            Height = 56,
            Margin = new Padding(0, 0, 0, 10)
        };
        registrationButton.Click += (_, _) => SelectMode(AppMode.Registration);

        var tagAssignmentButton = new Button
        {
            Text = "Tag Assignment",
            Dock = DockStyle.Fill,
            Height = 56,
            Margin = new Padding(0, 0, 0, 10)
        };
        tagAssignmentButton.Click += (_, _) => SelectMode(AppMode.TagAssignment);

        var combinedButton = new Button
        {
            Text = "Registration and Tag Assignment",
            Dock = DockStyle.Fill,
            Height = 56,
            Margin = new Padding(0, 0, 0, 10)
        };
        combinedButton.Click += (_, _) => SelectMode(AppMode.RegistrationAndTagAssignment);

        var adminButton = new Button
        {
            Text = "Admin",
            Dock = DockStyle.Fill,
            Height = 56,
            Margin = new Padding(0)
        };
        adminButton.Click += (_, _) => SelectMode(AppMode.Admin);

        var cancelButton = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(0, 12, 0, 0)
        };

        layout.Controls.Add(registrationButton, 0, 1);
        layout.Controls.Add(tagAssignmentButton, 0, 2);
        layout.Controls.Add(combinedButton, 0, 3);
        layout.Controls.Add(adminButton, 0, 4);
        layout.Controls.Add(cancelButton, 0, 5);

        Controls.Add(layout);
        AcceptButton = registrationButton;
        CancelButton = cancelButton;
    }

    private void SelectMode(AppMode mode)
    {
        SelectedMode = mode;
        DialogResult = DialogResult.OK;
        Close();
    }
}
