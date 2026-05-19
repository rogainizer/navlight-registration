using Navlight.Registration.App.Models;

namespace Navlight.Registration.App;

public sealed class AdminCourseSelectionDialog : Form
{
    private readonly CheckedListBox _coursesCheckedListBox;
    private readonly Button _previewButton;
    private readonly Button _printButton;

    public bool PreviewRequested { get; private set; }

    public AdminCourseSelectionDialog(IReadOnlyList<CourseOption> courses)
    {
        Text = "Select Courses";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Width = 420;
        Height = 460;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var promptLabel = new Label
        {
            Text = "Select one or more courses to print.",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };

        _coursesCheckedListBox = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            IntegralHeight = false
        };

        foreach (var course in courses)
        {
            _coursesCheckedListBox.Items.Add(course, true);
        }

        _coursesCheckedListBox.ItemCheck += (_, _) => BeginInvoke(UpdatePrintButtonState);

        _previewButton = new Button
        {
            Text = "Preview",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 8, 0)
        };
        _previewButton.Click += PreviewButton_Click;

        _printButton = new Button
        {
            Text = "Print",
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
        _printButton.Click += PrintButton_Click;

        var cancelButton = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            DialogResult = DialogResult.Cancel
        };

        var footerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 12, 0, 0)
        };
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footerPanel.Controls.Add(_previewButton, 0, 0);
        footerPanel.Controls.Add(_printButton, 1, 0);
        footerPanel.Controls.Add(cancelButton, 3, 0);

        layout.Controls.Add(promptLabel, 0, 0);
        layout.Controls.Add(_coursesCheckedListBox, 0, 1);
        layout.Controls.Add(footerPanel, 0, 2);

        Controls.Add(layout);
        AcceptButton = _printButton;
        CancelButton = cancelButton;
        UpdatePrintButtonState();
    }

    public IReadOnlyList<CourseOption> SelectedCourses => _coursesCheckedListBox.CheckedItems
        .OfType<CourseOption>()
        .ToList();

    private void PreviewButton_Click(object? sender, EventArgs e)
    {
        if (SelectedCourses.Count == 0)
        {
            MessageBox.Show(
                this,
                "Select at least one course to preview.",
                "No Courses Selected",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        PreviewRequested = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void PrintButton_Click(object? sender, EventArgs e)
    {
        if (SelectedCourses.Count == 0)
        {
            MessageBox.Show(
                this,
                "Select at least one course to print.",
                "No Courses Selected",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        PreviewRequested = false;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpdatePrintButtonState()
    {
        var hasSelection = _coursesCheckedListBox.CheckedItems.Count > 0;
        _previewButton.Enabled = hasSelection;
        _printButton.Enabled = hasSelection;
    }
}