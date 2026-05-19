namespace Navlight.Registration.App;

public sealed class TagAssignmentProgressDialog : Form
{
    private readonly Label _statusLabel;
    private readonly ListBox _tagsListBox;
    private readonly Button _closeButton;
    private bool _completed;

    public event EventHandler? StopRequested;

    public TagAssignmentProgressDialog(IReadOnlyCollection<string> initialTags, int maxTags)
    {
        Text = "Assign Tags";
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        KeyPreview = true;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 260);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headingLabel = new Label
        {
            Text = "Reading tags",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        };

        _statusLabel = new Label
        {
            Text = "Waiting for first tag...",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };

        _tagsListBox = new ListBox
        {
            Dock = DockStyle.Fill
        };

        foreach (var tag in initialTags)
        {
            _tagsListBox.Items.Add(tag);
        }

        _closeButton = new Button
        {
            Text = "Close",
            AutoSize = true,
            Anchor = AnchorStyles.Right
        };
        _closeButton.Click += (_, _) => RequestClose();
        AcceptButton = _closeButton;

        layout.Controls.Add(headingLabel, 0, 0);
        layout.Controls.Add(_statusLabel, 0, 1);
        layout.Controls.Add(_tagsListBox, 0, 2);
        layout.Controls.Add(_closeButton, 0, 3);

        Controls.Add(layout);
    }

    public void SetStatus(string status)
    {
        if (IsDisposed)
        {
            return;
        }

        _statusLabel.Text = status;
    }

    public void AddTag(string tagCode)
    {
        if (IsDisposed)
        {
            return;
        }

        if (!_tagsListBox.Items.Contains(tagCode))
        {
            _tagsListBox.Items.Add(tagCode);
        }
    }

    public void Complete(string status, bool autoClose = false)
    {
        if (IsDisposed)
        {
            return;
        }

        _completed = true;
        _statusLabel.Text = status;
        Activate();
        _closeButton.Focus();

        if (autoClose)
        {
            Close();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter)
        {
            RequestClose();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void RequestClose()
    {
        if (!_completed)
        {
            StopRequested?.Invoke(this, EventArgs.Empty);
        }

        Close();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (Owner is null)
        {
            CenterToScreen();
            return;
        }

        var ownerBounds = Owner.Bounds;
        Location = new Point(
            ownerBounds.Left + ((ownerBounds.Width - Width) / 2),
            ownerBounds.Top + ((ownerBounds.Height - Height) / 2));
        Activate();
    }
}