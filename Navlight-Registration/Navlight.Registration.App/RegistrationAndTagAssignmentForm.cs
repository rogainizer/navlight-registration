using System.ComponentModel;
using Navlight.Registration.App.Models;
using Navlight.Registration.App.Services;

namespace Navlight.Registration.App;

public sealed class RegistrationAndTagAssignmentForm : Form
{
    private const int MinimumSearchLength = 2;
    private const int SearchDebounceMilliseconds = 250;
    private const int MaximumAssignedTagCount = 3;
    private static readonly TimeSpan AdditionalTagWaitTimeout = TimeSpan.FromSeconds(10);
    private const string CopyCompetitorColumnName = "CopyCompetitor";
    private const string RemoveCompetitorColumnName = "RemoveCompetitor";

    private readonly RegistrationRepository _repository;
    private readonly NavLightTagReader _tagReader;
    private TagReaderOptions _tagReaderOptions;
    private readonly TextBox _searchTextBox;
    private readonly ListBox _searchResultsListBox;
    private readonly TextBox _teamNumberTextBox;
    private readonly TextBox _teamNameTextBox;
    private readonly ComboBox _categoryComboBox;
    private readonly ComboBox _courseComboBox;
    private readonly Label _registrationStatusValueLabel;
    private readonly Label _registeredAtLabel;
    private readonly CheckBox _flightPlanCheckBox;
    private readonly Label _flightPlanAtLabel;
    private readonly Label _tagStatusValueLabel;
    private readonly Label _tagStatusDetailLabel;
    private readonly TextBox _tagCodesTextBox;
    private readonly DataGridView _competitorsGrid;
    private readonly Bitmap _copyActionIcon;
    private readonly Bitmap _pasteActionIcon;
    private readonly Bitmap _deleteActionIcon;
    private readonly Bitmap _addActionIcon;
    private readonly Button _assignTagButton;
    private readonly Button _closeButton;
    private readonly Button _saveButton;
    private readonly Label _statusLabel;
    private readonly System.Windows.Forms.Timer _searchDebounceTimer;
    private readonly BindingList<CompetitorRow> _competitors = [];
    private TeamRegistration? _currentTeam;
    private bool _loadingTeam;
    private bool _suppressDirtyTracking;
    private bool _suppressSelectionHandling;
    private bool _suppressSearchTextHandling;
    private bool _hasUnsavedChanges;
    private bool _busy;
    private bool _detectingReaderPort;
    private int _searchRequestVersion;
    private string _lastSearchText = string.Empty;

    public RegistrationAndTagAssignmentForm()
    {
        Text = "Navlight Registration and Tag Assignment";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1080, 760);

        _repository = new RegistrationRepository(DatabaseOptions.Load());
        _tagReader = new NavLightTagReader();
        _tagReaderOptions = TagReaderOptions.Load();
        _copyActionIcon = CreateCopyActionIcon();
        _pasteActionIcon = CreatePasteActionIcon();
        _deleteActionIcon = CreateDeleteActionIcon();
        _addActionIcon = CreateAddActionIcon();
        _searchDebounceTimer = new System.Windows.Forms.Timer
        {
            Interval = SearchDebounceMilliseconds
        };
        _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12)
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var searchPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1
        };
        searchPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        searchPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        searchPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        searchPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var searchLabel = new Label
        {
            Text = "Search Team Name",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        };

        _searchTextBox = new TextBox
        {
            Dock = DockStyle.Top,
            PlaceholderText = "Enter team name"
        };
        _searchTextBox.KeyDown += SearchTextBox_KeyDown;
        _searchTextBox.TextChanged += SearchTextBox_TextChanged;

        _searchResultsListBox = new ListBox
        {
            Dock = DockStyle.Fill
        };
        _searchResultsListBox.SelectedIndexChanged += SearchResultsListBox_SelectedIndexChanged;
        _searchResultsListBox.KeyDown += SearchResultsListBox_KeyDown;

        _statusLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 12, 0, 0),
            Text = "Ready"
        };

        searchPanel.Controls.Add(searchLabel, 0, 0);
        searchPanel.Controls.Add(_searchTextBox, 0, 1);
        searchPanel.Controls.Add(_searchResultsListBox, 0, 2);
        searchPanel.Controls.Add(_statusLabel, 0, 3);

        var detailsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 11,
            Padding = new Padding(16),
            BackColor = Color.White
        };
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headingLabel = new Label
        {
            Text = "Registration and Tag Assignment",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 16)
        };
        detailsLayout.Controls.Add(headingLabel, 0, 0);
        detailsLayout.SetColumnSpan(headingLabel, 2);

        _teamNumberTextBox = CreateReadOnlyTextBox();
        _teamNameTextBox = new TextBox { Dock = DockStyle.Fill };
        _categoryComboBox = new ComboBox
        {
            Dock = DockStyle.Left,
            Width = 280,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = nameof(CategoryOption.Name),
            ValueMember = nameof(CategoryOption.CategoryId)
        };
        _courseComboBox = new ComboBox
        {
            Dock = DockStyle.Left,
            Width = 280,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = nameof(CourseOption.Name),
            ValueMember = nameof(CourseOption.CourseId)
        };
        _teamNameTextBox.TextChanged += (_, _) => MarkDirty();
        _categoryComboBox.SelectedIndexChanged += (_, _) => MarkDirty();
        _courseComboBox.SelectedIndexChanged += (_, _) => MarkDirty();
        _registrationStatusValueLabel = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 4, 0, 0),
            Text = "Not registered"
        };
        _registeredAtLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(12, 4, 0, 0)
        };
        _flightPlanCheckBox = new CheckBox
        {
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 4)
        };
        _flightPlanCheckBox.CheckedChanged += (_, _) => MarkDirty();
        _flightPlanAtLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(12, 8, 0, 0)
        };

        _tagStatusValueLabel = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 4, 0, 0),
            Text = "Tags not assigned"
        };
        _tagStatusDetailLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(12, 4, 0, 0)
        };

        var registrationPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        registrationPanel.Controls.Add(_registrationStatusValueLabel);
        registrationPanel.Controls.Add(_registeredAtLabel);

        var flightPlanPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        flightPlanPanel.Controls.Add(_flightPlanCheckBox);
        flightPlanPanel.Controls.Add(_flightPlanAtLabel);

        var tagStatusPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        tagStatusPanel.Controls.Add(_tagStatusValueLabel);
        tagStatusPanel.Controls.Add(_tagStatusDetailLabel);

        detailsLayout.Controls.Add(CreateFieldLabel("Team Number"), 0, 1);
        detailsLayout.Controls.Add(_teamNumberTextBox, 1, 1);
        detailsLayout.Controls.Add(CreateFieldLabel("Team Name"), 0, 2);
        detailsLayout.Controls.Add(_teamNameTextBox, 1, 2);
        detailsLayout.Controls.Add(CreateFieldLabel("Category"), 0, 3);
        detailsLayout.Controls.Add(_categoryComboBox, 1, 3);
        detailsLayout.Controls.Add(CreateFieldLabel("Course"), 0, 4);
        detailsLayout.Controls.Add(_courseComboBox, 1, 4);
        detailsLayout.Controls.Add(CreateFieldLabel("Registration"), 0, 5);
        detailsLayout.Controls.Add(registrationPanel, 1, 5);
        detailsLayout.Controls.Add(CreateFieldLabel("Flight Plan"), 0, 6);
        detailsLayout.Controls.Add(flightPlanPanel, 1, 6);
        detailsLayout.Controls.Add(CreateFieldLabel("Tag Status"), 0, 7);
        detailsLayout.Controls.Add(tagStatusPanel, 1, 7);

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
            HeaderText = "Name",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        _competitorsGrid.Columns.Add(CreateActionColumn(CopyCompetitorColumnName, _copyActionIcon, "Copy Competitor Name"));
        _competitorsGrid.Columns.Add(CreateActionColumn(RemoveCompetitorColumnName, _deleteActionIcon, "Delete Competitor"));
        _competitorsGrid.CellContentClick += CompetitorsGrid_CellContentClick;
        _competitorsGrid.CellPainting += CompetitorsGrid_CellPainting;
        _competitorsGrid.CellFormatting += CompetitorsGrid_CellFormatting;
        _competitorsGrid.CellToolTipTextNeeded += CompetitorsGrid_CellToolTipTextNeeded;
        _competitorsGrid.CellEndEdit += CompetitorsGrid_CellEndEdit;
        _competitorsGrid.ColumnHeaderMouseClick += CompetitorsGrid_ColumnHeaderMouseClick;
        _competitors.ListChanged += Competitors_ListChanged;

        var gridContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1
        };
        gridContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        gridContainer.Controls.Add(_competitorsGrid, 0, 0);

        detailsLayout.Controls.Add(gridContainer, 0, 8);
        detailsLayout.SetColumnSpan(gridContainer, 2);

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

        _tagCodesTextBox = new TextBox
        {
            CharacterCasing = CharacterCasing.Upper,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
            PlaceholderText = "Enter tag codes, separated by commas"
        };
        _tagCodesTextBox.TextChanged += TagCodesTextBox_TextChanged;
        tagHeaderPanel.Controls.Add(_tagCodesTextBox, 2, 0);

        detailsLayout.Controls.Add(tagHeaderPanel, 0, 9);
        detailsLayout.SetColumnSpan(tagHeaderPanel, 2);

        _saveButton = new Button
        {
            Text = "Save Registration and Tags",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Height = 36,
            Enabled = false,
            Margin = new Padding(0, 12, 0, 0)
        };
        _saveButton.Click += SaveButton_Click;

        _closeButton = new Button
        {
            Text = "Close",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Height = 36,
            Margin = new Padding(0, 12, 0, 0)
        };
        _closeButton.Click += CloseButton_Click;

        var footerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0)
        };
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footerPanel.Controls.Add(_saveButton, 0, 0);
        footerPanel.Controls.Add(_closeButton, 2, 0);

        var detailsWrapper = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        detailsWrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailsWrapper.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailsWrapper.Controls.Add(detailsLayout, 0, 0);
        detailsWrapper.Controls.Add(footerPanel, 0, 1);

        rootLayout.Controls.Add(searchPanel, 0, 0);
        rootLayout.Controls.Add(detailsWrapper, 1, 0);

        Controls.Add(rootLayout);
        SetEditState(false);
        Shown += RegistrationAndTagAssignmentForm_Shown;
    }

    private async void RegistrationAndTagAssignmentForm_Shown(object? sender, EventArgs e)
    {
        Shown -= RegistrationAndTagAssignmentForm_Shown;
        await InitializeTagReaderAvailabilityAsync();
    }

    private async Task InitializeTagReaderAvailabilityAsync()
    {
        _detectingReaderPort = true;
        UpdateAssignTagButtonState();
        SetStatus("Searching COM ports for the NavLight reader...");

        try
        {
            var detectedPort = await _tagReader.FindReaderPortAsync(_tagReaderOptions.ResponseTimeout).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(detectedPort))
            {
                _tagReaderOptions = _tagReaderOptions.WithPortName(string.Empty);
                SetStatus("No NavLight reader was detected on the scanned COM ports.", true);
                return;
            }

            var previousPort = _tagReaderOptions.PortName;
            _tagReaderOptions = _tagReaderOptions.WithPortName(detectedPort);

            try
            {
                _tagReaderOptions.Save();
            }
            catch (Exception ex)
            {
                SetStatus($"NavLight reader found on {detectedPort}, but appsettings.json could not be updated: {ex.Message}", true);
                return;
            }

            SetStatus(string.Equals(previousPort, detectedPort, StringComparison.OrdinalIgnoreCase)
                ? $"NavLight reader ready on {detectedPort}."
                : $"NavLight reader found on {detectedPort}. Saved for future use.");
        }
        catch (Exception ex)
        {
            _tagReaderOptions = _tagReaderOptions.WithPortName(string.Empty);
            SetStatus($"Unable to search COM ports for the NavLight reader: {ex.Message}", true);
        }
        finally
        {
            _detectingReaderPort = false;
            UpdateAssignTagButtonState();
        }
    }

    private static Label CreateFieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(0, 8, 12, 8)
    };

    private static TextBox CreateReadOnlyTextBox() => new()
    {
        Dock = DockStyle.Left,
        Width = 180,
        ReadOnly = true
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

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        if (_currentTeam is null)
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

        _currentTeam.Name = _teamNameTextBox.Text.Trim();
        _currentTeam.CategoryId = selectedCategory.CategoryId;
        _currentTeam.CourseId = selectedCourse.CourseId;
        _currentTeam.Registered = true;
        _currentTeam.FlightPlan = _flightPlanCheckBox.Checked;
        _currentTeam.Competitors = competitorNames
            .Select(name => new CompetitorRecord { Name = name })
            .ToList();
        _currentTeam.TagCodes = ParseTagCodes(_tagCodesTextBox.Text);

        ToggleBusyState(true, "Saving registration and tag assignment...");

        try
        {
            var conflict = await _repository.GetFirstTagAssignmentConflictAsync(_currentTeam.TagCodes, _currentTeam.TeamId);
            if (conflict.HasValue)
            {
                ShowTagAssignmentConflict(conflict.Value.TagCode, conflict.Value.OwnerDisplay);
                return;
            }

            await _repository.SaveRegistrationAndTagAssignmentsAsync(_currentTeam);
            AppNavigation.LastSavedRegistrationTeamId = _currentTeam.TeamId;
            ResetForNextTeam();
            SetStatus("Registration and tag assignment saved. Ready for next team.");
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
        if (_currentTeam is null)
        {
            return;
        }

        if (!_tagReaderOptions.IsConfigured)
        {
            SetStatus("No NavLight reader is available. Check the reader connection and reopen tag assignment.", true);
            return;
        }

        var tagCodes = ParseTagCodes(_tagCodesTextBox.Text);
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

                var assignedTo = await _repository.GetTagAssignmentOwnerDisplayAsync(tag.TagIdAlpha, _currentTeam.TeamId);
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

    private void CloseButton_Click(object? sender, EventArgs e)
    {
        if (_hasUnsavedChanges && !ConfirmDiscardChanges())
        {
            return;
        }

        AppNavigation.ShowStartupScreen?.Invoke();
    }

    private void Competitors_ListChanged(object? sender, ListChangedEventArgs e)
    {
        if (e.ListChangedType is ListChangedType.ItemAdded or ListChangedType.ItemDeleted)
        {
            MarkDirty();
        }
    }

    private async void SearchResultsListBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressSelectionHandling || _loadingTeam || _searchResultsListBox.SelectedItem is not TeamSearchResult selectedTeam)
        {
            return;
        }

        if (_currentTeam is not null && selectedTeam.TeamId == _currentTeam.TeamId)
        {
            return;
        }

        if (_hasUnsavedChanges && !ConfirmDiscardChanges())
        {
            RestoreCurrentTeamSelection();
            return;
        }

        await LoadSelectedTeamAsync();
    }

    private void AddCompetitor()
    {
        _competitors.Add(new CompetitorRow());
        var rowIndex = _competitors.Count - 1;
        if (rowIndex >= 0)
        {
            _competitorsGrid.CurrentCell = _competitorsGrid.Rows[rowIndex].Cells[0];
            _competitorsGrid.BeginEdit(true);
            _competitorsGrid.InvalidateRow(rowIndex);
        }

        MarkDirty();
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
                MarkDirty();
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
            MarkDirty();
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

        AddCompetitor();
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
            MarkDirty();
        }
    }

    private async void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down)
        {
            e.SuppressKeyPress = true;
            MoveSearchSelection(1);
            return;
        }

        if (e.KeyCode == Keys.Up)
        {
            e.SuppressKeyPress = true;
            MoveSearchSelection(-1);
            return;
        }

        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.SuppressKeyPress = true;
        _searchDebounceTimer.Stop();

        if (_searchResultsListBox.Items.Count > 0)
        {
            if (_searchResultsListBox.SelectedIndex < 0)
            {
                _searchResultsListBox.SelectedIndex = 0;
            }

            await LoadSelectedTeamAsync();
            return;
        }

        await SearchTeamsAsync(forceSearch: true);
    }

    private async void SearchDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _searchDebounceTimer.Stop();
        await SearchTeamsAsync();
    }

    private async void SearchResultsListBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.SuppressKeyPress = true;
        await LoadSelectedTeamAsync();
    }

    private void SearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_suppressSearchTextHandling)
        {
            _lastSearchText = _searchTextBox.Text;
            return;
        }

        if (_hasUnsavedChanges && _currentTeam is not null)
        {
            if (!ConfirmDiscardChanges())
            {
                _suppressSearchTextHandling = true;
                _searchTextBox.Text = _lastSearchText;
                _searchTextBox.SelectionStart = _searchTextBox.TextLength;
                _suppressSearchTextHandling = false;
                return;
            }

            ClearSearchResults();
        }

        _lastSearchText = _searchTextBox.Text;

        var searchTerm = _searchTextBox.Text.Trim();
        if (searchTerm.Length < MinimumSearchLength)
        {
            _searchDebounceTimer.Stop();
            ClearSearchResults();
            SetStatus($"Type at least {MinimumSearchLength} characters to search.");
            return;
        }

        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void TagCodesTextBox_TextChanged(object? sender, EventArgs e)
    {
        UpdateTagStatus(ParseTagCodes(_tagCodesTextBox.Text).Count);
        MarkDirty();
    }

    private async Task SearchTeamsAsync(bool forceSearch = false)
    {
        var searchTerm = _searchTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            ClearSearchResults();
            SetStatus($"Type at least {MinimumSearchLength} characters to search.");
            return;
        }

        if (!forceSearch && searchTerm.Length < MinimumSearchLength)
        {
            ClearSearchResults();
            SetStatus($"Type at least {MinimumSearchLength} characters to search.");
            return;
        }

        var requestVersion = ++_searchRequestVersion;

        ToggleBusyState(true, "Searching teams...");

        try
        {
            var results = await _repository.SearchTeamsAsync(searchTerm);
            if (requestVersion != _searchRequestVersion)
            {
                return;
            }

            _searchResultsListBox.DataSource = results;
            _searchResultsListBox.DisplayMember = nameof(TeamSearchResult.DisplayText);
            _searchResultsListBox.ValueMember = nameof(TeamSearchResult.TeamId);

            if (results.Count > 0)
            {
                _searchResultsListBox.SelectedIndex = 0;
            }

            SetStatus(results.Count == 0 ? "No matching teams found." : $"Found {results.Count} matching team(s).");
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

    private void ClearSearchResults()
    {
        _suppressDirtyTracking = true;
        _suppressSelectionHandling = true;
        _searchResultsListBox.DataSource = null;
        _searchResultsListBox.ClearSelected();
        _currentTeam = null;
        _searchRequestVersion++;
        _teamNumberTextBox.Clear();
        _teamNameTextBox.Clear();
        _categoryComboBox.DataSource = null;
        _courseComboBox.DataSource = null;
        _registrationStatusValueLabel.Text = "Not registered";
        _registeredAtLabel.Text = string.Empty;
        _flightPlanCheckBox.Checked = false;
        _flightPlanAtLabel.Text = string.Empty;
        _tagCodesTextBox.Clear();
        UpdateTagStatus(0);
        _competitors.Clear();
        SetEditState(false);
        _hasUnsavedChanges = false;
        _suppressSelectionHandling = false;
        _suppressDirtyTracking = false;
    }

    private void ResetForNextTeam()
    {
        _searchDebounceTimer.Stop();
        _suppressSearchTextHandling = true;
        _searchTextBox.Clear();
        _lastSearchText = _searchTextBox.Text;
        _suppressSearchTextHandling = false;
        ClearSearchResults();
        _searchTextBox.Focus();
    }

    private void MoveSearchSelection(int direction)
    {
        if (_searchResultsListBox.Items.Count == 0)
        {
            return;
        }

        var nextIndex = _searchResultsListBox.SelectedIndex;
        if (nextIndex < 0)
        {
            nextIndex = direction > 0 ? 0 : _searchResultsListBox.Items.Count - 1;
        }
        else
        {
            nextIndex = Math.Max(0, Math.Min(_searchResultsListBox.Items.Count - 1, nextIndex + direction));
        }

        _searchResultsListBox.SelectedIndex = nextIndex;
    }

    private async Task LoadSelectedTeamAsync()
    {
        if (_loadingTeam || _searchResultsListBox.SelectedItem is not TeamSearchResult selectedTeam)
        {
            return;
        }

        ToggleBusyState(true, "Loading team...");

        try
        {
            _currentTeam = await _repository.GetTeamRegistrationWithTagsAsync(selectedTeam.TeamId);
            await BindTeamAsync();
            SetStatus("Team loaded.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
            SetEditState(false);
        }
        finally
        {
            ToggleBusyState(false);
        }
    }

    private async Task BindTeamAsync()
    {
        if (_currentTeam is null)
        {
            SetEditState(false);
            return;
        }

        _loadingTeam = true;
        _suppressDirtyTracking = true;
        try
        {
            var categories = await _repository.GetCategoriesAsync(_currentTeam.EventId);
            var courses = await _repository.GetCoursesAsync(_currentTeam.EventId);
            var selectedCategory = categories.FirstOrDefault(category => category.CategoryId == _currentTeam.CategoryId);
            var selectedCourse = courses.FirstOrDefault(course => course.CourseId == _currentTeam.CourseId);

            _teamNumberTextBox.Text = _currentTeam.TeamNumber;
            _teamNameTextBox.Text = _currentTeam.Name;
            _registrationStatusValueLabel.Text = _currentTeam.Registered ? "Registered" : "Not registered";
            _registeredAtLabel.Text = _currentTeam.RegisteredAt.HasValue
                ? $"Registered at {_currentTeam.RegisteredAt.Value:G}"
                : string.Empty;
            _flightPlanCheckBox.Checked = _currentTeam.FlightPlan;
            _flightPlanAtLabel.Text = _currentTeam.FlightPlanAt.HasValue
                ? $"Returned at {_currentTeam.FlightPlanAt.Value:G}"
                : string.Empty;

            _categoryComboBox.DataSource = null;
            _categoryComboBox.DataSource = categories;
            _categoryComboBox.SelectedItem = selectedCategory;

            _courseComboBox.DataSource = null;
            _courseComboBox.DataSource = courses;
            _courseComboBox.SelectedItem = selectedCourse;

            _competitors.Clear();
            foreach (var competitor in _currentTeam.Competitors.OrderBy(item => item.Name))
            {
                _competitors.Add(new CompetitorRow { Name = competitor.Name });
            }

            _tagCodesTextBox.Text = string.Join(", ", _currentTeam.TagCodes);
            UpdateTagStatus(_currentTeam.TagCodes.Count);

            SetEditState(true);
            _hasUnsavedChanges = false;
        }
        finally
        {
            _suppressDirtyTracking = false;
            _loadingTeam = false;
        }
    }

    private void UpdateTagStatus(int tagCount)
    {
        _tagStatusValueLabel.Text = tagCount > 0 ? "Tags assigned" : "Tags not assigned";
        _tagStatusDetailLabel.Text = tagCount > 0 ? $"{tagCount} tag(s) entered" : string.Empty;
    }

    private static List<string> ParseTagCodes(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tagCode => !string.IsNullOrWhiteSpace(tagCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void MarkDirty()
    {
        if (_suppressDirtyTracking || _loadingTeam || _currentTeam is null)
        {
            return;
        }

        _hasUnsavedChanges = true;
    }

    private bool ConfirmDiscardChanges()
    {
        var result = MessageBox.Show(
            this,
            "You have unsaved changes. Press OK to loose the changes, or Cancel to save the changes",
            "Unsaved Changes",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        return result == DialogResult.OK;
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

    private void RestoreCurrentTeamSelection()
    {
        _suppressSelectionHandling = true;
        try
        {
            if (_currentTeam is null)
            {
                _searchResultsListBox.ClearSelected();
                return;
            }

            for (var index = 0; index < _searchResultsListBox.Items.Count; index++)
            {
                if (_searchResultsListBox.Items[index] is TeamSearchResult result && result.TeamId == _currentTeam.TeamId)
                {
                    _searchResultsListBox.SelectedIndex = index;
                    return;
                }
            }

            _searchResultsListBox.ClearSelected();
        }
        finally
        {
            _suppressSelectionHandling = false;
        }
    }

    private void SetEditState(bool enabled)
    {
        _teamNameTextBox.Enabled = enabled;
        _categoryComboBox.Enabled = enabled;
        _courseComboBox.Enabled = enabled;
        _flightPlanCheckBox.Enabled = enabled;
        _competitorsGrid.Enabled = enabled;
        _tagCodesTextBox.Enabled = enabled;
        UpdateAssignTagButtonState();
        _saveButton.Enabled = enabled;
    }

    private void ToggleBusyState(bool busy, string? status = null)
    {
        _busy = busy;
        UseWaitCursor = busy;
        _searchTextBox.Enabled = !busy;
        _searchResultsListBox.Enabled = !busy;
        UpdateAssignTagButtonState();
        _saveButton.Enabled = !busy && _currentTeam is not null;
        _closeButton.Enabled = !busy;
        if (status is not null)
        {
            SetStatus(status);
        }
    }

    private void UpdateAssignTagButtonState()
    {
        _assignTagButton.Enabled = !_busy && !_detectingReaderPort && _currentTeam is not null && _tagReaderOptions.IsConfigured;
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