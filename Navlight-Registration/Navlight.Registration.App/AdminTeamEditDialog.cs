using System.ComponentModel;
using Navlight.Registration.App.Models;
using Navlight.Registration.App.Services;

namespace Navlight.Registration.App;

public sealed class AdminTeamEditDialog : Form
{
    private const int MaximumAssignedTagCount = 3;
    private static readonly TimeSpan AdditionalTagWaitTimeout = TimeSpan.FromSeconds(10);
    private const string CopyCompetitorColumnName = "CopyCompetitor";
    private const string RemoveCompetitorColumnName = "RemoveCompetitor";

    private readonly int? _teamId;
    private readonly RegistrationRepository _repository;
    private readonly NavLightTagReader _tagReader;
    private readonly TagReaderOptions _tagReaderOptions;
    private readonly TextBox _teamNumberTextBox;
    private readonly TextBox _teamNameTextBox;
    private readonly ComboBox _categoryComboBox;
    private readonly ComboBox _courseComboBox;
    private readonly CheckBox _registeredCheckBox;
    private readonly CheckBox _flightPlanCheckBox;
    private readonly DateTimePicker _flightPlanAtPicker;
    private readonly TextBox _tagCodesTextBox;
    private readonly Button _assignTagButton;
    private readonly DataGridView _competitorsGrid;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;
    private readonly Label _statusLabel;
    private readonly Bitmap _copyActionIcon;
    private readonly Bitmap _pasteActionIcon;
    private readonly Bitmap _deleteActionIcon;
    private readonly Bitmap _addActionIcon;
    private readonly BindingList<CompetitorRow> _competitors = [];
    private TeamRegistration? _team;

    public AdminTeamEditDialog(int? teamId = null)
    {
        _teamId = teamId;
        _repository = new RegistrationRepository(DatabaseOptions.Load());
        _tagReader = new NavLightTagReader();
        _tagReaderOptions = TagReaderOptions.Load();
        _copyActionIcon = CreateCopyActionIcon();
        _pasteActionIcon = CreatePasteActionIcon();
        _deleteActionIcon = CreateDeleteActionIcon();
        _addActionIcon = CreateAddActionIcon();

        Text = _teamId.HasValue ? "Edit Team" : "Add Team";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 700);
        Width = 980;
        Height = 760;

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var detailsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9,
            Padding = new Padding(16),
            BackColor = Color.White
        };
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headingLabel = new Label
        {
            Text = _teamId.HasValue ? "Edit Team" : "Add Team",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 16)
        };
        detailsLayout.Controls.Add(headingLabel, 0, 0);
        detailsLayout.SetColumnSpan(headingLabel, 2);

        _teamNumberTextBox = new TextBox { Dock = DockStyle.Left, Width = 200 };
        _teamNameTextBox = new TextBox { Dock = DockStyle.Fill };
        _categoryComboBox = new ComboBox
        {
            Dock = DockStyle.Left,
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = nameof(CategoryOption.Name),
            ValueMember = nameof(CategoryOption.CategoryId)
        };
        _courseComboBox = new ComboBox
        {
            Dock = DockStyle.Left,
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = nameof(CourseOption.Name),
            ValueMember = nameof(CourseOption.CourseId)
        };
        _registeredCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "Registered",
            Margin = new Padding(0, 8, 0, 8)
        };
        _flightPlanCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "Returned",
            Margin = new Padding(0, 8, 12, 8)
        };
        _flightPlanCheckBox.CheckedChanged += FlightPlanCheckBox_CheckedChanged;
        _flightPlanAtPicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy HH:mm",
            Width = 180,
            Margin = new Padding(0, 4, 0, 4)
        };
        _tagCodesTextBox = new TextBox
        {
            CharacterCasing = CharacterCasing.Upper,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
            PlaceholderText = "Enter tag codes, separated by commas"
        };

        detailsLayout.Controls.Add(CreateFieldLabel("Team Number"), 0, 1);
        detailsLayout.Controls.Add(_teamNumberTextBox, 1, 1);
        detailsLayout.Controls.Add(CreateFieldLabel("Team Name"), 0, 2);
        detailsLayout.Controls.Add(_teamNameTextBox, 1, 2);
        detailsLayout.Controls.Add(CreateFieldLabel("Category"), 0, 3);
        detailsLayout.Controls.Add(_categoryComboBox, 1, 3);
        detailsLayout.Controls.Add(CreateFieldLabel("Course"), 0, 4);
        detailsLayout.Controls.Add(_courseComboBox, 1, 4);
        detailsLayout.Controls.Add(CreateFieldLabel("Status"), 0, 5);
        detailsLayout.Controls.Add(_registeredCheckBox, 1, 5);

        var flightPlanPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };
        flightPlanPanel.Controls.Add(_flightPlanCheckBox);
        flightPlanPanel.Controls.Add(_flightPlanAtPicker);

        detailsLayout.Controls.Add(CreateFieldLabel("Flight Plan"), 0, 6);
        detailsLayout.Controls.Add(flightPlanPanel, 1, 6);

        _competitorsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            BackgroundColor = SystemColors.Window,
            DataSource = _competitors,
            Margin = new Padding(0, 0, 0, 8),
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ShowCellToolTips = true
        };
        _competitorsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CompetitorRow.Name),
            HeaderText = "Competitors",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        _competitorsGrid.Columns.Add(CreateActionColumn(CopyCompetitorColumnName, _copyActionIcon, "Copy Competitor Name"));
        _competitorsGrid.Columns.Add(CreateActionColumn(RemoveCompetitorColumnName, _deleteActionIcon, "Delete Competitor"));
        _competitorsGrid.CellContentClick += CompetitorsGrid_CellContentClick;
        _competitorsGrid.CellFormatting += CompetitorsGrid_CellFormatting;
        _competitorsGrid.CellToolTipTextNeeded += CompetitorsGrid_CellToolTipTextNeeded;
        _competitorsGrid.CellEndEdit += CompetitorsGrid_CellEndEdit;
        _competitorsGrid.CellPainting += CompetitorsGrid_CellPainting;
        _competitorsGrid.ColumnHeaderMouseClick += CompetitorsGrid_ColumnHeaderMouseClick;

        detailsLayout.Controls.Add(_competitorsGrid, 0, 7);
        detailsLayout.SetColumnSpan(_competitorsGrid, 2);

        var tagHeaderPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8)
        };
        tagHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tagHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tagHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tagHeaderPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var tagsLabel = new Label
        {
            Text = "Tag Codes",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 8)
        };
        tagHeaderPanel.Controls.Add(tagsLabel, 0, 0);

        _assignTagButton = new Button
        {
            Text = "Assign Tag",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Height = 36,
            Enabled = false,
            Margin = new Padding(0, 0, 12, 0)
        };
        _assignTagButton.Click += AssignTagButton_Click;
        tagHeaderPanel.Controls.Add(_assignTagButton, 1, 0);
        tagHeaderPanel.Controls.Add(_tagCodesTextBox, 2, 0);

        detailsLayout.Controls.Add(tagHeaderPanel, 0, 8);
        detailsLayout.SetColumnSpan(tagHeaderPanel, 2);

        _saveButton = new Button
        {
            Text = "Save",
            AutoSize = true,
            Height = 36,
            Anchor = AnchorStyles.Left
        };
        _saveButton.Click += SaveButton_Click;

        _cancelButton = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            Height = 36,
            Anchor = AnchorStyles.Right,
            DialogResult = DialogResult.Cancel
        };

        var footerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 12, 0, 0)
        };
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footerPanel.Controls.Add(_saveButton, 0, 0);
        footerPanel.Controls.Add(_cancelButton, 2, 0);

        _statusLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 12, 0, 0),
            Text = "Loading team..."
        };

        rootLayout.Controls.Add(detailsLayout, 0, 0);
        rootLayout.Controls.Add(footerPanel, 0, 1);
        rootLayout.Controls.Add(_statusLabel, 0, 2);

        Controls.Add(rootLayout);
        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
        Shown += async (_, _) => await LoadTeamAsync();
    }

    private static Label CreateFieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(0, 8, 12, 8)
    };

    private static DataGridViewImageColumn CreateActionColumn(string name, Bitmap image, string tooltipText) => new()
    {
        Name = name,
        HeaderText = string.Empty,
        Image = image,
        Width = 36,
        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
        ImageLayout = DataGridViewImageCellLayout.Zoom,
        ToolTipText = tooltipText
    };

    private static Bitmap CreateCopyActionIcon()
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var pen = new Pen(Color.DimGray, 1.4f);
        using var brush = new SolidBrush(Color.WhiteSmoke);

        var backPage = new Rectangle(4, 2, 8, 9);
        var frontPage = new Rectangle(2, 5, 8, 9);

        graphics.FillRectangle(brush, backPage);
        graphics.DrawRectangle(pen, backPage);
        graphics.FillRectangle(brush, frontPage);
        graphics.DrawRectangle(pen, frontPage);

        graphics.DrawLine(pen, 4, 8, 8, 8);
        graphics.DrawLine(pen, 4, 10, 8, 10);
        graphics.DrawLine(pen, 4, 12, 7, 12);

        return bitmap;
    }

    private static Bitmap CreatePasteActionIcon()
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var pen = new Pen(Color.SteelBlue, 1.4f);
        using var brush = new SolidBrush(Color.FromArgb(255, 240, 247, 255));

        var board = new Rectangle(4, 4, 8, 10);
        graphics.FillRectangle(brush, board);
        graphics.DrawRectangle(pen, board);
        graphics.DrawArc(pen, 5, 1, 6, 5, 200, 140);
        graphics.DrawLine(pen, 8, 6, 8, 11);
        graphics.DrawLine(pen, 6, 9, 8, 11);
        graphics.DrawLine(pen, 10, 9, 8, 11);

        return bitmap;
    }

    private static Bitmap CreateDeleteActionIcon()
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var pen = new Pen(Color.Firebrick, 1.5f);
        using var brush = new SolidBrush(Color.FromArgb(255, 252, 242, 242));

        graphics.FillRectangle(brush, 4, 5, 8, 8);
        graphics.DrawRectangle(pen, 4, 5, 8, 8);
        graphics.DrawLine(pen, 3, 5, 13, 5);
        graphics.DrawLine(pen, 6, 3, 10, 3);
        graphics.DrawLine(pen, 7, 2, 9, 2);
        graphics.DrawLine(pen, 6, 7, 6, 11);
        graphics.DrawLine(pen, 8, 7, 8, 11);
        graphics.DrawLine(pen, 10, 7, 10, 11);

        return bitmap;
    }

    private static Bitmap CreateAddActionIcon()
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var pen = new Pen(Color.SeaGreen, 1.6f);
        using var brush = new SolidBrush(Color.FromArgb(230, 240, 250, 242));

        graphics.FillEllipse(brush, 1.5f, 1.5f, 13f, 13f);
        graphics.DrawEllipse(pen, 1.5f, 1.5f, 13f, 13f);
        graphics.DrawLine(pen, 8, 4.5f, 8, 11.5f);
        graphics.DrawLine(pen, 4.5f, 8, 11.5f, 8);

        return bitmap;
    }

    private async Task LoadTeamAsync()
    {
        ToggleBusyState(true, "Loading team...");

        try
        {
            if (_teamId.HasValue)
            {
                _team = await _repository.GetTeamRegistrationWithTagsAsync(_teamId.Value);
            }
            else
            {
                var eventId = await _repository.GetDefaultEventIdAsync();
                _team = new TeamRegistration
                {
                    TeamId = 0,
                    EventId = eventId,
                    Registered = false
                };
                _competitors.Add(new CompetitorRow());
            }

            var categories = await _repository.GetCategoriesAsync(_team.EventId);
            var courses = await _repository.GetCoursesAsync(_team.EventId);

            _teamNumberTextBox.Text = _team.TeamNumber;
            _teamNameTextBox.Text = _team.Name;
            _categoryComboBox.DataSource = categories;
            _categoryComboBox.SelectedItem = categories.FirstOrDefault(item => item.CategoryId == _team.CategoryId);
            _courseComboBox.DataSource = courses;
            _courseComboBox.SelectedItem = courses.FirstOrDefault(item => item.CourseId == _team.CourseId);
            _registeredCheckBox.Checked = _team.Registered;
            _flightPlanCheckBox.Checked = _team.FlightPlan;
            _flightPlanAtPicker.Value = _team.FlightPlanAt ?? DateTime.Now;
            UpdateFlightPlanControls();
            _tagCodesTextBox.Text = string.Join(", ", _team.TagCodes);

            if (_teamId.HasValue)
            {
                _competitors.Clear();
                foreach (var competitor in _team.Competitors.OrderBy(item => item.Name))
                {
                    _competitors.Add(new CompetitorRow { Name = competitor.Name });
                }
            }

            SetStatus("Ready");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
            _saveButton.Enabled = false;
        }
        finally
        {
            ToggleBusyState(false);
        }
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        if (_team is null)
        {
            return;
        }

        if (_categoryComboBox.SelectedItem is not CategoryOption selectedCategory)
        {
            SetStatus("Select a category before saving.", true);
            return;
        }

        if (_courseComboBox.SelectedItem is not CourseOption selectedCourse)
        {
            SetStatus("Select a course before saving.", true);
            return;
        }

        var teamNumber = _teamNumberTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(teamNumber))
        {
            SetStatus("Team number is required.", true);
            return;
        }

        var teamName = _teamNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(teamName))
        {
            SetStatus("Team name is required.", true);
            return;
        }

        var competitorNames = _competitors
            .Select(row => row.Name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToList();

        if (competitorNames.Count == 0)
        {
            SetStatus("At least one competitor is required.", true);
            return;
        }

        _team.TeamNumber = teamNumber;
        _team.Name = teamName;
        _team.CategoryId = selectedCategory.CategoryId;
        _team.CourseId = selectedCourse.CourseId;
        _team.Registered = _registeredCheckBox.Checked;
        _team.FlightPlan = _flightPlanCheckBox.Checked;
        _team.FlightPlanAt = _flightPlanCheckBox.Checked ? _flightPlanAtPicker.Value : null;
        _team.Competitors = competitorNames.Select(name => new CompetitorRecord { Name = name }).ToList();
        _team.TagCodes = _tagCodesTextBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tagCode => !string.IsNullOrWhiteSpace(tagCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        ToggleBusyState(true, "Saving team...");

        try
        {
            var conflict = await _repository.GetFirstTagAssignmentConflictAsync(_team.TagCodes, _teamId);
            if (conflict.HasValue)
            {
                ShowTagAssignmentConflict(conflict.Value.TagCode, conflict.Value.OwnerDisplay);
                return;
            }

            await _repository.SaveAdminTeamAsync(_team);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            ToggleBusyState(false);
        }
    }

    private async void AssignTagButton_Click(object? sender, EventArgs e)
    {
        if (_team is null)
        {
            return;
        }

        if (!_tagReaderOptions.IsConfigured)
        {
            SetStatus("Tag reader COM port is not configured. Set TagReader.PortName in appsettings.json.", true);
            return;
        }

        var tagCodes = _tagCodesTextBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tagCode => !string.IsNullOrWhiteSpace(tagCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tagCodes.Count >= MaximumAssignedTagCount)
        {
            SetStatus($"A maximum of {MaximumAssignedTagCount} tags can be assigned.", true);
            return;
        }

        ToggleBusyState(true, $"Waiting for tag on {_tagReaderOptions.PortName}...");
        var progressDialog = new TagAssignmentProgressDialog(tagCodes, MaximumAssignedTagCount);
        using var assignmentCancellation = new CancellationTokenSource();
        progressDialog.StopRequested += (_, _) => assignmentCancellation.Cancel();
        progressDialog.Show(this);
        var timedOutWaitingForAdditionalTag = false;
        var stoppedByUser = false;

        try
        {
            while (tagCodes.Count < MaximumAssignedTagCount)
            {
                var isFirstTag = tagCodes.Count == 0;
                progressDialog.SetStatus(isFirstTag
                    ? $"Waiting for first tag on {_tagReaderOptions.PortName}..."
                    : $"Waiting up to {AdditionalTagWaitTimeout.TotalSeconds:0} seconds for another tag...");

                NavLightTagReadResult tag;
                try
                {
                    tag = await _tagReader.ReadAndClearTagAsync(
                        _tagReaderOptions.PortName,
                        assignmentCancellation.Token,
                        _tagReaderOptions.ResponseTimeout,
                        isFirstTag ? _tagReaderOptions.TagDetectTimeout : AdditionalTagWaitTimeout,
                        _tagReaderOptions.ResetInterface);
                }
                catch (OperationCanceledException) when (assignmentCancellation.IsCancellationRequested)
                {
                    stoppedByUser = true;
                    break;
                }
                catch (TimeoutException) when (!isFirstTag)
                {
                    timedOutWaitingForAdditionalTag = true;
                    break;
                }

                if (tagCodes.Contains(tag.TagIdAlpha, StringComparer.OrdinalIgnoreCase))
                {
                    progressDialog.SetStatus($"Read tag {tag.TagIdAlpha} again. Waiting for a different tag...");
                    continue;
                }

                var assignedTo = await _repository.GetTagAssignmentOwnerDisplayAsync(tag.TagIdAlpha, _teamId);
                if (assignedTo is not null)
                {
                    progressDialog.SetStatus($"Tag {tag.TagIdAlpha} is already assigned to {assignedTo}.");
                    ShowTagAssignmentConflict(tag.TagIdAlpha, assignedTo);
                    continue;
                }

                tagCodes.Add(tag.TagIdAlpha);
                progressDialog.AddTag(tag.TagIdAlpha);
                progressDialog.SetStatus($"Read tag {tag.TagIdAlpha}.");

                _tagCodesTextBox.Text = string.Join(", ", tagCodes);
                _tagCodesTextBox.SelectionStart = _tagCodesTextBox.TextLength;
            }

            var assignedCount = tagCodes.Count;
            var completeMessage = stoppedByUser
                ? assignedCount == 1
                    ? "Stopped after reading 1 tag."
                    : $"Stopped after reading {assignedCount} tags."
                : assignedCount == 1
                    ? "Finished assigning 1 tag."
                    : $"Finished assigning {assignedCount} tags.";
            progressDialog.Complete(completeMessage, autoClose: timedOutWaitingForAdditionalTag);
            SetStatus(completeMessage);
            BeginInvoke(() =>
            {
                if (_saveButton.Enabled && Visible)
                {
                    _saveButton.Focus();
                }
            });
        }
        catch (Exception ex)
        {
            progressDialog.Complete(ex.Message);
            SetStatus(ex.Message, true);
        }
        finally
        {
            ToggleBusyState(false);
        }
    }

    private void CompetitorsGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        if (_competitorsGrid.Rows[e.RowIndex].DataBoundItem is not CompetitorRow selected)
        {
            return;
        }

        var columnName = _competitorsGrid.Columns[e.ColumnIndex].Name;
        if (columnName == CopyCompetitorColumnName)
        {
            if (string.IsNullOrWhiteSpace(selected.Name))
            {
                var clipboardText = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(clipboardText))
                {
                    SetStatus("Clipboard does not contain a competitor name.", true);
                    return;
                }

                selected.Name = clipboardText;
                _competitorsGrid.Refresh();
                SetStatus($"Pasted '{clipboardText}' from clipboard.");
                return;
            }

            Clipboard.SetText(selected.Name);
            SetStatus($"Copied '{selected.Name}' to clipboard.");
            return;
        }

        if (columnName == RemoveCompetitorColumnName)
        {
            _competitors.Remove(selected);
        }
    }

    private void CompetitorsGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        var columnName = _competitorsGrid.Columns[e.ColumnIndex].Name;
        if (columnName != CopyCompetitorColumnName)
        {
            return;
        }

        if (_competitorsGrid.Rows[e.RowIndex].DataBoundItem is not CompetitorRow competitor)
        {
            return;
        }

        e.Value = string.IsNullOrWhiteSpace(competitor.Name) ? _pasteActionIcon : _copyActionIcon;
        e.FormattingApplied = true;
    }

    private void CompetitorsGrid_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            if (e.ColumnIndex >= 0 && _competitorsGrid.Columns[e.ColumnIndex].Name == RemoveCompetitorColumnName)
            {
                e.ToolTipText = "Add Competitor";
            }

            return;
        }

        var columnName = _competitorsGrid.Columns[e.ColumnIndex].Name;
        if (_competitorsGrid.Rows[e.RowIndex].DataBoundItem is not CompetitorRow competitor)
        {
            return;
        }

        if (columnName == CopyCompetitorColumnName)
        {
            e.ToolTipText = string.IsNullOrWhiteSpace(competitor.Name) ? "Paste Competitor Name" : "Copy Competitor Name";
            return;
        }

        if (columnName == RemoveCompetitorColumnName)
        {
            e.ToolTipText = "Delete Competitor";
        }
    }

    private void CompetitorsGrid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0)
        {
            _competitorsGrid.InvalidateRow(e.RowIndex);
        }
    }

    private void CompetitorsGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex != -1 || e.ColumnIndex < 0 || _competitorsGrid.Columns[e.ColumnIndex].Name != RemoveCompetitorColumnName)
        {
            return;
        }

        if (e.Graphics is null)
        {
            return;
        }

        e.PaintBackground(e.CellBounds, false);
        e.PaintContent(e.CellBounds);

        var imageX = e.CellBounds.Left + (e.CellBounds.Width - _addActionIcon.Width) / 2;
        var imageY = e.CellBounds.Top + (e.CellBounds.Height - _addActionIcon.Height) / 2;
        e.Graphics.DrawImage(_addActionIcon, imageX, imageY, _addActionIcon.Width, _addActionIcon.Height);
        e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);
        e.Handled = true;
    }

    private void CompetitorsGrid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0 || _competitorsGrid.Columns[e.ColumnIndex].Name != RemoveCompetitorColumnName || !_competitorsGrid.Enabled)
        {
            return;
        }

        _competitors.Add(new CompetitorRow());
        var rowIndex = _competitors.Count - 1;
        if (rowIndex >= 0)
        {
            _competitorsGrid.CurrentCell = _competitorsGrid.Rows[rowIndex].Cells[0];
            _competitorsGrid.BeginEdit(true);
            _competitorsGrid.InvalidateRow(rowIndex);
        }
    }

    private void FlightPlanCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        UpdateFlightPlanControls(setDefaultTimestamp: true);
    }

    private void UpdateFlightPlanControls(bool setDefaultTimestamp = false)
    {
        var isReturned = _flightPlanCheckBox.Checked;
        _flightPlanAtPicker.Enabled = isReturned;

        if (!isReturned)
        {
            return;
        }

        if (setDefaultTimestamp && _team?.FlightPlanAt is null)
        {
            _flightPlanAtPicker.Value = DateTime.Now;
        }
    }

    private void ToggleBusyState(bool busy, string? status = null)
    {
        UseWaitCursor = busy;
        _teamNumberTextBox.Enabled = !busy;
        _teamNameTextBox.Enabled = !busy;
        _categoryComboBox.Enabled = !busy;
        _courseComboBox.Enabled = !busy;
        _registeredCheckBox.Enabled = !busy;
        _flightPlanCheckBox.Enabled = !busy;
        _flightPlanAtPicker.Enabled = !busy && _flightPlanCheckBox.Checked;
        _tagCodesTextBox.Enabled = !busy;
        _assignTagButton.Enabled = !busy && _tagReaderOptions.IsConfigured;
        _competitorsGrid.Enabled = !busy;
        _saveButton.Enabled = !busy;
        _cancelButton.Enabled = !busy;
        if (status is not null)
        {
            SetStatus(status);
        }
    }

    private void ShowTagAssignmentConflict(string tagCode, string ownerDisplay)
    {
        MessageBox.Show(
            this,
            $"Tag {tagCode} is already assigned to {ownerDisplay}.",
            "Tag Already Assigned",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void SetStatus(string message, bool isError = false)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = isError ? Color.Firebrick : Color.DimGray;
    }

    private sealed class CompetitorRow
    {
        public string? Name { get; set; }
    }
}