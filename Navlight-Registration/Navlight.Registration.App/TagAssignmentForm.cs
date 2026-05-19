using Navlight.Registration.App.Models;
using Navlight.Registration.App.Services;

namespace Navlight.Registration.App;

public sealed class TagAssignmentForm : Form
{
    private const int MinimumSearchLength = 2;
    private const int SearchDebounceMilliseconds = 250;
    private const int MaximumAssignedTagCount = 3;
    private static readonly TimeSpan AdditionalTagWaitTimeout = TimeSpan.FromSeconds(10);

    private readonly RegistrationRepository _repository;
    private readonly NavLightTagReader _tagReader;
    private readonly TagReaderOptions _tagReaderOptions;
    private readonly TextBox _searchTextBox;
    private readonly ListBox _searchResultsListBox;
    private readonly TextBox _teamNumberTextBox;
    private readonly TextBox _teamNameTextBox;
    private readonly Label _tagStatusValueLabel;
    private readonly Label _tagStatusDetailLabel;
    private readonly TextBox _tagCodesTextBox;
    private readonly Button _readAndClearTagButton;
    private readonly Button _saveButton;
    private readonly Button _switchModeButton;
    private readonly Label _statusLabel;
    private readonly System.Windows.Forms.Timer _searchDebounceTimer;
    private readonly int? _initialTeamId;
    private TeamTagAssignment? _currentTeam;
    private bool _loadingTeam;
    private bool _initialTeamLoaded;
    private int _searchRequestVersion;
    private string _loadedTagCodes = string.Empty;

    public TagAssignmentForm(int? initialTeamId = null)
    {
        Text = "Navlight Tag Assignment";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1080, 720);
        _initialTeamId = initialTeamId;

        _repository = new RegistrationRepository(DatabaseOptions.Load());
        _tagReader = new NavLightTagReader();
        _tagReaderOptions = TagReaderOptions.Load();
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
        _searchResultsListBox.SelectedIndexChanged += async (_, _) => await LoadSelectedTeamAsync();

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
            RowCount = 6,
            Padding = new Padding(16),
            BackColor = Color.White
        };
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < 5; index++)
        {
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var headingLabel = new Label
        {
            Text = "Tag Assignment",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 16)
        };
        detailsLayout.Controls.Add(headingLabel, 0, 0);
        detailsLayout.SetColumnSpan(headingLabel, 2);

        _teamNumberTextBox = CreateReadOnlyTextBox();
        _teamNameTextBox = CreateReadOnlyTextBox(fill: true);
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

        var statusPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        statusPanel.Controls.Add(_tagStatusValueLabel);
        statusPanel.Controls.Add(_tagStatusDetailLabel);

        detailsLayout.Controls.Add(CreateFieldLabel("Team Number"), 0, 1);
        detailsLayout.Controls.Add(_teamNumberTextBox, 1, 1);
        detailsLayout.Controls.Add(CreateFieldLabel("Team Name"), 0, 2);
        detailsLayout.Controls.Add(_teamNameTextBox, 1, 2);
        detailsLayout.Controls.Add(CreateFieldLabel("Status"), 0, 3);
        detailsLayout.Controls.Add(statusPanel, 1, 3);

        var tagHeaderPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 8, 0, 8)
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

        _readAndClearTagButton = new Button
        {
            Text = "Assign Tag",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Height = 36,
            Enabled = false,
            Margin = new Padding(0, 0, 12, 0)
        };
        _readAndClearTagButton.Click += AssignTagButton_Click;
        tagHeaderPanel.Controls.Add(_readAndClearTagButton, 1, 0);

        _tagCodesTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
            PlaceholderText = "Enter tag codes, separated by commas"
        };
        _tagCodesTextBox.TextChanged += TagCodesTextBox_TextChanged;
        tagHeaderPanel.Controls.Add(_tagCodesTextBox, 2, 0);

        detailsLayout.Controls.Add(tagHeaderPanel, 0, 4);
        detailsLayout.SetColumnSpan(tagHeaderPanel, 2);

        _saveButton = new Button
        {
            Text = "Save Tag Assignment",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Height = 36,
            Enabled = false,
            Margin = new Padding(0, 12, 0, 0)
        };
        _saveButton.Click += SaveButton_Click;

        _switchModeButton = new Button
        {
            Text = "Open Registration",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Height = 36,
            Margin = new Padding(0, 12, 0, 0)
        };
        _switchModeButton.Click += SwitchModeButton_Click;

        var footerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0)
        };
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footerPanel.Controls.Add(_saveButton, 0, 0);
        footerPanel.Controls.Add(_switchModeButton, 2, 0);

        detailsLayout.Controls.Add(footerPanel, 0, 5);
        detailsLayout.SetColumnSpan(footerPanel, 2);

        rootLayout.Controls.Add(searchPanel, 0, 0);
        rootLayout.Controls.Add(detailsLayout, 1, 0);

        Controls.Add(rootLayout);
        SetEditState(false);
        Shown += TagAssignmentForm_Shown;
    }

    private static Label CreateFieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(0, 8, 12, 8)
    };

    private static TextBox CreateReadOnlyTextBox(bool fill = false) => new()
    {
        Dock = fill ? DockStyle.Fill : DockStyle.Left,
        Width = fill ? 0 : 180,
        ReadOnly = true
    };

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        if (_currentTeam is null)
        {
            return;
        }

        var tagCodes = ParseTagCodes(_tagCodesTextBox.Text);

        ToggleBusyState(true, "Saving tag assignment...");

        try
        {
            var conflict = await _repository.GetFirstTagAssignmentConflictAsync(tagCodes, _currentTeam.TeamId);
            if (conflict.HasValue)
            {
                ShowTagAssignmentConflict(conflict.Value.TagCode, conflict.Value.OwnerDisplay);
                return;
            }

            await _repository.SaveTagAssignmentsAsync(_currentTeam.TeamId, _currentTeam.LastUpdatedAt, tagCodes);
            ResetForNextTeam();
            SetStatus("Tag assignment saved. Ready for next team.");
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
            SetStatus("Tag reader COM port is not configured. Set TagReader.PortName in appsettings.json.", true);
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

    private async void TagAssignmentForm_Shown(object? sender, EventArgs e)
    {
        if (_initialTeamLoaded || !_initialTeamId.HasValue)
        {
            return;
        }

        _initialTeamLoaded = true;
        await LoadTeamAsync(_initialTeamId.Value, "Loaded last saved registration.");
    }

    private void SwitchModeButton_Click(object? sender, EventArgs e)
    {
        if (HasUnsavedTagChanges() && !ConfirmDiscardChanges())
        {
            return;
        }

        AppNavigation.SwitchMode?.Invoke(AppMode.Registration);
    }

    private async void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.SuppressKeyPress = true;
        _searchDebounceTimer.Stop();
        await SearchTeamsAsync(forceSearch: true);
    }

    private async void SearchDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _searchDebounceTimer.Stop();
        await SearchTeamsAsync();
    }

    private void SearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        var searchTerm = _searchTextBox.Text.Trim();
        if (searchTerm.Length < MinimumSearchLength)
        {
            _searchDebounceTimer.Stop();
            ClearTeamDetails();
            SetStatus($"Type at least {MinimumSearchLength} characters to search.");
            return;
        }

        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void TagCodesTextBox_TextChanged(object? sender, EventArgs e)
    {
        UpdateTagStatus(ParseTagCodes(_tagCodesTextBox.Text).Count);
    }

    private async Task SearchTeamsAsync(bool forceSearch = false)
    {
        var searchTerm = _searchTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            ClearTeamDetails();
            SetStatus($"Type at least {MinimumSearchLength} characters to search.");
            return;
        }

        if (!forceSearch && searchTerm.Length < MinimumSearchLength)
        {
            ClearTeamDetails();
            SetStatus($"Type at least {MinimumSearchLength} characters to search.");
            return;
        }

        var requestVersion = ++_searchRequestVersion;
        ToggleBusyState(true, "Searching teams...");

        try
        {
            var results = await _repository.SearchTeamsAsync(searchTerm, registeredOnly: true);
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

    private async Task LoadSelectedTeamAsync()
    {
        if (_loadingTeam || _searchResultsListBox.SelectedItem is not TeamSearchResult selectedTeam)
        {
            return;
        }

        await LoadTeamAsync(selectedTeam.TeamId, "Team loaded.");
    }

    private async Task LoadTeamAsync(int teamId, string successStatus)
    {
        if (_loadingTeam)
        {
            return;
        }

        ToggleBusyState(true, "Loading team...");

        try
        {
            _currentTeam = await _repository.GetTeamTagAssignmentAsync(teamId);
            BindTeam();
            SetStatus(successStatus);
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

    private void BindTeam()
    {
        if (_currentTeam is null)
        {
            SetEditState(false);
            return;
        }

        _loadingTeam = true;
        try
        {
            _teamNumberTextBox.Text = _currentTeam.TeamNumber;
            _teamNameTextBox.Text = _currentTeam.Name;
            _loadedTagCodes = string.Join(", ", _currentTeam.TagCodes);
            _tagCodesTextBox.Text = _loadedTagCodes;
            UpdateTagStatus(_currentTeam.TagCodes.Count);
            SetEditState(true);
            BeginInvoke(() =>
            {
                if (_readAndClearTagButton.Enabled && Visible)
                {
                    _readAndClearTagButton.Focus();
                }
            });
        }
        finally
        {
            _loadingTeam = false;
        }
    }

    private void ClearTeamDetails()
    {
        _searchResultsListBox.DataSource = null;
        _currentTeam = null;
        _searchRequestVersion++;
        _teamNumberTextBox.Clear();
        _teamNameTextBox.Clear();
        _loadedTagCodes = string.Empty;
        _tagCodesTextBox.Clear();
        UpdateTagStatus(0);
        SetEditState(false);
    }

    private void ResetForNextTeam()
    {
        _searchDebounceTimer.Stop();
        _searchTextBox.Clear();
        ClearTeamDetails();
        BeginInvoke(() =>
        {
            if (_searchTextBox.Enabled && Visible)
            {
                _searchTextBox.Focus();
            }
        });
    }

    private void SetEditState(bool enabled)
    {
        _tagCodesTextBox.Enabled = enabled;
        _saveButton.Enabled = enabled;
        _readAndClearTagButton.Enabled = enabled && _tagReaderOptions.IsConfigured;
    }

    private void ToggleBusyState(bool busy, string? status = null)
    {
        UseWaitCursor = busy;
        _searchTextBox.Enabled = !busy;
        _searchResultsListBox.Enabled = !busy;
        _readAndClearTagButton.Enabled = !busy && _currentTeam is not null && _tagReaderOptions.IsConfigured;
        _saveButton.Enabled = !busy && _currentTeam is not null;
        _switchModeButton.Enabled = !busy;
        if (status is not null)
        {
            SetStatus(status);
        }
    }

    private bool HasUnsavedTagChanges()
    {
        if (_currentTeam is null)
        {
            return false;
        }

        return NormalizeTagCodes(_tagCodesTextBox.Text) != NormalizeTagCodes(_loadedTagCodes);
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

    private static string NormalizeTagCodes(string value)
    {
        return string.Join(",", ParseTagCodes(value).Select(item => item.ToUpperInvariant()));
    }

    private void SetStatus(string message, bool isError = false)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = isError ? Color.Firebrick : Color.DimGray;
    }
}
