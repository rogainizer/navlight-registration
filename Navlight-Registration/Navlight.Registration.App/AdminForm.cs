using Navlight.Registration.App.Models;
using Navlight.Registration.App.Services;

namespace Navlight.Registration.App;

public sealed class AdminForm : Form
{
    private const string EditColumnName = "EditTeam";
    private const string DeleteColumnName = "DeleteTeam";

    private readonly RegistrationRepository _repository;
    private readonly DataGridView _teamsGrid;
    private readonly TextBox _teamSearchTextBox;
    private readonly Button _clearDatabaseButton;
    private readonly Button _exportButton;
    private readonly Button _loadButton;
    private readonly Button _refreshButton;
    private readonly Label _statusLabel;
    private readonly Bitmap _editActionIcon;
    private readonly Bitmap _deleteActionIcon;
    private readonly Bitmap _addActionIcon;
    private IReadOnlyList<AdminTeamOverviewRow> _allTeams = [];
    private string _sortPropertyName = nameof(AdminTeamOverviewRow.TeamNumber);
    private SortOrder _sortOrder = SortOrder.Ascending;

    public AdminForm()
    {
        Text = "Navlight Admin";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1200, 720);

        _repository = new RegistrationRepository(DatabaseOptions.Load());
        _editActionIcon = CreateEditActionIcon();
        _deleteActionIcon = CreateDeleteActionIcon();
        _addActionIcon = CreateAddActionIcon();

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headingPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 12)
        };
        headingPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headingPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headingPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headingPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headingPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headingPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headingPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headingPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headingLabel = new Label
        {
            Text = "Admin Overview",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            Margin = new Padding(0, 4, 0, 12)
        };

        var searchLabel = new Label
        {
            Text = "Team Search",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 8, 8, 0)
        };

        _teamSearchTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Search by team number or team name",
            Margin = new Padding(0, 4, 12, 0)
        };
        _teamSearchTextBox.TextChanged += (_, _) => ApplyTeamView();

        _clearDatabaseButton = new Button
        {
            Text = "Clear Database",
            AutoSize = true,
            Height = 36,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 0, 8, 0)
        };
        _clearDatabaseButton.Click += async (_, _) => await ClearDatabaseAsync();

        _exportButton = new Button
        {
            Text = "Export",
            AutoSize = true,
            Height = 36,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 0, 8, 0)
        };
        _exportButton.Click += async (_, _) => await ExportDatabaseAsync();

        _loadButton = new Button
        {
            Text = "Load",
            AutoSize = true,
            Height = 36,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 0, 8, 0)
        };
        _loadButton.Click += async (_, _) => await OpenImportDialogAsync();

        _refreshButton = new Button
        {
            Text = "Refresh",
            AutoSize = true,
            Height = 36,
            Anchor = AnchorStyles.Right
        };
        _refreshButton.Click += async (_, _) => await LoadTeamsAsync();

        headingPanel.Controls.Add(headingLabel, 0, 0);
    headingPanel.SetColumnSpan(headingLabel, 6);
        headingPanel.Controls.Add(searchLabel, 0, 1);
        headingPanel.Controls.Add(_teamSearchTextBox, 1, 1);
        headingPanel.Controls.Add(_clearDatabaseButton, 2, 1);
    headingPanel.Controls.Add(_exportButton, 3, 1);
    headingPanel.Controls.Add(_loadButton, 4, 1);
    headingPanel.Controls.Add(_refreshButton, 5, 1);

        _teamsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            BackgroundColor = SystemColors.Window,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                WrapMode = DataGridViewTriState.True
            }
        };
        _teamsGrid.Columns.Add(CreateTextColumn(nameof(AdminTeamOverviewRow.TeamNumber), "Team #", 90));
        _teamsGrid.Columns.Add(CreateTextColumn(nameof(AdminTeamOverviewRow.TeamName), "Team", 180));
        _teamsGrid.Columns.Add(CreateTextColumn(nameof(AdminTeamOverviewRow.CategoryName), "Category", 140));
        _teamsGrid.Columns.Add(CreateTextColumn(nameof(AdminTeamOverviewRow.CourseName), "Course", 140));
        _teamsGrid.Columns.Add(CreateFillColumn(nameof(AdminTeamOverviewRow.Competitors), "Competitors", 260));
        _teamsGrid.Columns.Add(CreateFillColumn(nameof(AdminTeamOverviewRow.Tags), "Tags", 200));
        _teamsGrid.Columns.Add(CreateFillColumn(nameof(AdminTeamOverviewRow.Status), "Status", 180));
        _teamsGrid.Columns.Add(CreateActionColumn(EditColumnName, _editActionIcon, "Edit team"));
        _teamsGrid.Columns.Add(CreateActionColumn(DeleteColumnName, _deleteActionIcon, "Delete team"));
        _teamsGrid.CellPainting += TeamsGrid_CellPainting;
        _teamsGrid.ColumnHeaderMouseClick += TeamsGrid_ColumnHeaderMouseClick;
        _teamsGrid.CellContentClick += TeamsGrid_CellContentClick;
        _teamsGrid.CellToolTipTextNeeded += TeamsGrid_CellToolTipTextNeeded;

        _statusLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            Text = "Loading teams...",
            Margin = new Padding(0, 12, 0, 0)
        };

        rootLayout.Controls.Add(headingPanel, 0, 0);
        rootLayout.Controls.Add(_teamsGrid, 0, 1);
        rootLayout.Controls.Add(_statusLabel, 0, 2);

        Controls.Add(rootLayout);
        Shown += async (_, _) => await LoadTeamsAsync();
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(string propertyName, string headerText, int width) => new()
    {
        DataPropertyName = propertyName,
        HeaderText = headerText,
        Width = width,
        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
        SortMode = DataGridViewColumnSortMode.Programmatic,
        DefaultCellStyle = new DataGridViewCellStyle
        {
            WrapMode = DataGridViewTriState.True
        }
    };

    private static DataGridViewTextBoxColumn CreateFillColumn(string propertyName, string headerText, int minimumWidth) => new()
    {
        DataPropertyName = propertyName,
        HeaderText = headerText,
        MinimumWidth = minimumWidth,
        AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        SortMode = DataGridViewColumnSortMode.Programmatic,
        DefaultCellStyle = new DataGridViewCellStyle
        {
            WrapMode = DataGridViewTriState.True
        }
    };

    private static DataGridViewImageColumn CreateActionColumn(string name, Bitmap image, string tooltipText) => new()
    {
        Name = name,
        HeaderText = string.Empty,
        Image = image,
        Width = 36,
        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
        ImageLayout = DataGridViewImageCellLayout.Normal,
        ToolTipText = tooltipText,
        SortMode = DataGridViewColumnSortMode.NotSortable,
        DefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            NullValue = null
        }
    };

    private static Bitmap CreateEditActionIcon()
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var pen = new Pen(Color.SteelBlue, 1.4f);
        using var brush = new SolidBrush(Color.FromArgb(255, 240, 247, 255));

        graphics.FillRectangle(brush, 2, 11, 9, 3);
        graphics.DrawRectangle(pen, 2, 11, 9, 3);
        graphics.DrawLine(pen, 4, 10, 11, 3);
        graphics.DrawLine(pen, 5, 11, 12, 4);
        graphics.DrawLine(pen, 10, 2.5f, 13, 5.5f);

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

    private async Task LoadTeamsAsync()
    {
        ToggleBusyState(true, "Loading teams...");

        try
        {
            _allTeams = await _repository.GetAdminTeamOverviewAsync();
            ApplyTeamView();
        }
        catch (Exception ex)
        {
            _allTeams = [];
            _teamsGrid.DataSource = null;
            SetStatus(ex.Message, true);
        }
        finally
        {
            ToggleBusyState(false);
        }
    }

    private async Task OpenImportDialogAsync()
    {
        using var dialog = new AdminSpreadsheetImportDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await LoadTeamsAsync();
        }
    }

    private async Task ExportDatabaseAsync()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Tab-separated file (*.tsv)|*.tsv|Text file (*.txt)|*.txt|All Files (*.*)|*.*",
            Title = "Save test export file",
            FileName = "TagNums.txt",
            OverwritePrompt = true,
            AddExtension = true,
            DefaultExt = "tsv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ToggleBusyState(true, "Exporting database...");

        try
        {
            var teams = (await _repository.GetAdminTeamOverviewAsync())
                .OrderBy(team => ParseTeamNumber(team.TeamNumber))
                .ThenBy(team => team.TeamNumber, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var dataLines = teams.SelectMany(BuildExportLines).ToList();
            var lines = new List<string>
            {
                string.Join('\t', "TagID", "TEAM", "CLASS", "COURSE", "NAMES", "HANDICAP", "TEAMNAME"),
                string.Empty,
                "START:"
            };
            lines.AddRange(dataLines);
            lines.Add("END:");

            await File.WriteAllLinesAsync(dialog.FileName, lines);
            SetStatus($"Exported {dataLines.Count} row(s) for {teams.Count} team(s) to {Path.GetFileName(dialog.FileName)}.");
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

    private async Task ClearDatabaseAsync()
    {
        var result = MessageBox.Show(
            this,
            "This will permanently delete all events, teams, competitors, and tag assignments. Continue?",
            "Clear Database",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.OK)
        {
            return;
        }

        ToggleBusyState(true, "Clearing database...");

        try
        {
            await _repository.ClearDatabaseAsync();
            _allTeams = [];
            ApplyTeamView();
            SetStatus("Database cleared.");
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

    private void TeamsGrid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0)
        {
            return;
        }

        var column = _teamsGrid.Columns[e.ColumnIndex];
        if (column.Name == DeleteColumnName)
        {
            using var dialog = new AdminTeamEditDialog();
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _ = LoadTeamsAsync();
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(column.DataPropertyName))
        {
            return;
        }

        if (_sortPropertyName == column.DataPropertyName)
        {
            _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
        }
        else
        {
            _sortPropertyName = column.DataPropertyName;
            _sortOrder = SortOrder.Ascending;
        }

        ApplyTeamView();
    }

    private void TeamsGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.ColumnIndex < 0)
        {
            return;
        }

        if (e.Graphics is null)
        {
            return;
        }

        var columnName = _teamsGrid.Columns[e.ColumnIndex].Name;

        if (e.RowIndex >= 0 && (columnName == EditColumnName || columnName == DeleteColumnName))
        {
            var actionIcon = columnName == EditColumnName ? _editActionIcon : _deleteActionIcon;

            e.PaintBackground(e.CellBounds, e.State.HasFlag(DataGridViewElementStates.Selected));

            var actionImageX = e.CellBounds.Left + (e.CellBounds.Width - actionIcon.Width) / 2;
            var actionImageY = e.CellBounds.Top + (e.CellBounds.Height - actionIcon.Height) / 2;
            e.Graphics.DrawImage(actionIcon, actionImageX, actionImageY, actionIcon.Width, actionIcon.Height);

            e.Paint(e.ClipBounds, DataGridViewPaintParts.Border | DataGridViewPaintParts.Focus);
            e.Handled = true;
            return;
        }

        if (e.RowIndex != -1 || columnName != DeleteColumnName)
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

    private void TeamsGrid_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex >= 0 || e.ColumnIndex < 0 || _teamsGrid.Columns[e.ColumnIndex].Name != DeleteColumnName)
        {
            return;
        }

        e.ToolTipText = "Add Team";
    }

    private async void TeamsGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        if (_teamsGrid.Rows[e.RowIndex].DataBoundItem is not AdminTeamOverviewRow team)
        {
            return;
        }

        var columnName = _teamsGrid.Columns[e.ColumnIndex].Name;
        if (columnName == EditColumnName)
        {
            using var dialog = new AdminTeamEditDialog(team.TeamId);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                await LoadTeamsAsync();
            }

            return;
        }

        if (columnName != DeleteColumnName)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"Delete team '{team.TeamNumber} - {team.TeamName}'?",
            "Confirm Delete",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.OK)
        {
            return;
        }

        ToggleBusyState(true, "Deleting team...");

        try
        {
            await _repository.DeleteTeamAsync(team.TeamId);
            await LoadTeamsAsync();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
            ToggleBusyState(false);
        }
    }

    private void ApplyTeamView()
    {
        IEnumerable<AdminTeamOverviewRow> filteredTeams = _allTeams;
        var searchTerm = _teamSearchTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            filteredTeams = filteredTeams.Where(team =>
                team.TeamNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                team.TeamName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        filteredTeams = SortTeams(filteredTeams);
        var visibleTeams = filteredTeams.ToList();

        _teamsGrid.DataSource = visibleTeams;
        UpdateSortGlyphs();

        var searchSuffix = string.IsNullOrWhiteSpace(searchTerm)
            ? string.Empty
            : $" matching '{searchTerm}'";
        SetStatus($"Showing {visibleTeams.Count} team(s){searchSuffix}.");
    }

    private IEnumerable<AdminTeamOverviewRow> SortTeams(IEnumerable<AdminTeamOverviewRow> teams)
    {
        var ascending = _sortOrder != SortOrder.Descending;

        return _sortPropertyName switch
        {
            nameof(AdminTeamOverviewRow.TeamNumber) => ascending
                ? teams.OrderBy(team => ParseTeamNumber(team.TeamNumber))
                    .ThenBy(team => team.TeamNumber, StringComparer.OrdinalIgnoreCase)
                : teams.OrderByDescending(team => ParseTeamNumber(team.TeamNumber))
                    .ThenByDescending(team => team.TeamNumber, StringComparer.OrdinalIgnoreCase),
            nameof(AdminTeamOverviewRow.TeamName) => ascending
                ? teams.OrderBy(team => team.TeamName, StringComparer.OrdinalIgnoreCase)
                : teams.OrderByDescending(team => team.TeamName, StringComparer.OrdinalIgnoreCase),
            nameof(AdminTeamOverviewRow.CategoryName) => ascending
                ? teams.OrderBy(team => team.CategoryName, StringComparer.OrdinalIgnoreCase)
                : teams.OrderByDescending(team => team.CategoryName, StringComparer.OrdinalIgnoreCase),
            nameof(AdminTeamOverviewRow.CourseName) => ascending
                ? teams.OrderBy(team => team.CourseName, StringComparer.OrdinalIgnoreCase)
                : teams.OrderByDescending(team => team.CourseName, StringComparer.OrdinalIgnoreCase),
            nameof(AdminTeamOverviewRow.Competitors) => ascending
                ? teams.OrderBy(team => team.Competitors, StringComparer.OrdinalIgnoreCase)
                : teams.OrderByDescending(team => team.Competitors, StringComparer.OrdinalIgnoreCase),
            nameof(AdminTeamOverviewRow.Tags) => ascending
                ? teams.OrderBy(team => team.Tags, StringComparer.OrdinalIgnoreCase)
                : teams.OrderByDescending(team => team.Tags, StringComparer.OrdinalIgnoreCase),
            nameof(AdminTeamOverviewRow.Status) => ascending
                ? teams.OrderBy(team => team.Status, StringComparer.OrdinalIgnoreCase)
                : teams.OrderByDescending(team => team.Status, StringComparer.OrdinalIgnoreCase),
            _ => teams
        };
    }

    private void UpdateSortGlyphs()
    {
        foreach (DataGridViewColumn column in _teamsGrid.Columns)
        {
            column.HeaderCell.SortGlyphDirection = column.DataPropertyName == _sortPropertyName
                ? _sortOrder
                : SortOrder.None;
        }
    }

    private void ToggleBusyState(bool busy, string? status = null)
    {
        UseWaitCursor = busy;
        _clearDatabaseButton.Enabled = !busy;
        _exportButton.Enabled = !busy;
        _loadButton.Enabled = !busy;
        _refreshButton.Enabled = !busy;
        _teamSearchTextBox.Enabled = !busy;
        _teamsGrid.Enabled = !busy;
        if (status is not null)
        {
            SetStatus(status);
        }
    }

    private void SetStatus(string message, bool isError = false)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = isError ? Color.Firebrick : Color.DimGray;
    }

    private static string SanitizeExportField(string value)
    {
        return value
            .Replace("\t", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }

    private static IEnumerable<string> BuildExportLines(AdminTeamOverviewRow team)
    {
        var tags = team.Tags
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (tags.Length == 0)
        {
            yield return " " + string.Join('\t',
                string.Empty,
                SanitizeExportField(team.TeamNumber),
                SanitizeExportField(team.CategoryName),
                SanitizeExportField(team.CourseName),
                SanitizeExportField(team.Competitors),
                string.Empty,
                SanitizeExportField(team.TeamName));
            yield break;
        }

        yield return " " + string.Join('\t',
            SanitizeExportField(tags[0]),
            SanitizeExportField(team.TeamNumber),
            SanitizeExportField(team.CategoryName),
            SanitizeExportField(team.CourseName),
            SanitizeExportField(team.Competitors),
            string.Empty,
            SanitizeExportField(team.TeamName));

        foreach (var tag in tags.Skip(1))
        {
            yield return " " + string.Join('\t',
                SanitizeExportField(tag),
                SanitizeExportField(team.TeamNumber),
                SanitizeExportField(team.CategoryName),
                SanitizeExportField(team.CourseName));
        }
    }

    private static int ParseTeamNumber(string teamNumber)
    {
        return int.TryParse(teamNumber, out var parsedTeamNumber)
            ? parsedTeamNumber
            : int.MaxValue;
    }
}
