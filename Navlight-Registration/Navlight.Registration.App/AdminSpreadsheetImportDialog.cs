using ClosedXML.Excel;
using Microsoft.VisualBasic.FileIO;
using Navlight.Registration.App.Models;
using Navlight.Registration.App.Services;

namespace Navlight.Registration.App;

public sealed class AdminSpreadsheetImportDialog : Form
{
    private readonly RegistrationRepository _repository;
    private readonly TextBox _eventNameTextBox;
    private readonly DateTimePicker _eventDatePicker;
    private readonly ComboBox _fileTypeComboBox;
    private readonly CheckBox _hasHeaderCheckBox;
    private readonly TextBox _filePathTextBox;
    private readonly Button _browseButton;
    private readonly Button _importButton;
    private readonly Button _cancelButton;
    private readonly Label _statusLabel;
    private readonly Label _summaryLabel;
    private readonly Label _instructionsLabel;

    public AdminSpreadsheetImportDialog() : this(DatabaseOptions.Load())
    {
    }

    internal AdminSpreadsheetImportDialog(DatabaseOptions databaseOptions)
    {
        _repository = new RegistrationRepository(databaseOptions);

        Text = "Load Teams from File";
        NavlightBranding.ApplyWindowIcon(this);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(780, 600);
        Width = 820;
        Height = 650;

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16)
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headingLabel = new Label
        {
            Text = "Import Teams and Competitors",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12)
        };

        var fileLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 6,
            Margin = new Padding(0, 0, 0, 12)
        };
        fileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fileLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fileLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fileLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fileLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fileLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fileLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var eventNameLabel = new Label
        {
            Text = "Event Name",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 0)
        };

        _eventNameTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Enter the event name",
            Margin = new Padding(0, 4, 12, 0)
        };

        var eventDateLabel = new Label
        {
            Text = "Event Date",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 0)
        };

        _eventDatePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Width = 160,
            Margin = new Padding(0, 4, 12, 0),
            Value = DateTime.Today
        };

        var fileTypeLabel = new Label
        {
            Text = "File Type",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 0)
        };

        _fileTypeComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 12, 0)
        };
        _fileTypeComboBox.Items.AddRange(["Spreadsheet", "Navlight"]);
        _fileTypeComboBox.SelectedIndex = 0;

        var fileLabel = new Label
        {
            Text = "File",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 0)
        };

        _filePathTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Select an Excel workbook (.xlsx or .xlsm)",
            Margin = new Padding(0, 4, 12, 0)
        };

        _browseButton = new Button
        {
            Text = "Browse...",
            AutoSize = true,
            Height = 36,
            Anchor = AnchorStyles.Right
        };
        _browseButton.Click += BrowseButton_Click;

        _instructionsLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(680, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 8, 0, 0),
            Text = "First worksheet columns: Team Number, Category, Competitor Name, Team Name, Course. One row per competitor. Teams are grouped by number; missing categories and courses are created for the selected event."
        };

        _hasHeaderCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "File includes a header row",
            Checked = true,
            Margin = new Padding(0, 8, 0, 0)
        };

        fileLayout.Controls.Add(eventNameLabel, 0, 0);
        fileLayout.Controls.Add(_eventNameTextBox, 1, 0);
        fileLayout.SetColumnSpan(_eventNameTextBox, 2);
        fileLayout.Controls.Add(eventDateLabel, 0, 1);
        fileLayout.Controls.Add(_eventDatePicker, 1, 1);
        fileLayout.Controls.Add(fileTypeLabel, 0, 2);
        fileLayout.Controls.Add(_fileTypeComboBox, 1, 2);
        fileLayout.SetColumnSpan(_fileTypeComboBox, 2);
        fileLayout.Controls.Add(_filePathTextBox, 1, 3);
        fileLayout.Controls.Add(_browseButton, 2, 3);
        fileLayout.Controls.Add(fileLabel, 0, 3);
        fileLayout.Controls.Add(_hasHeaderCheckBox, 1, 4);
        fileLayout.SetColumnSpan(_hasHeaderCheckBox, 2);
        fileLayout.Controls.Add(_instructionsLabel, 0, 5);
        fileLayout.SetColumnSpan(_instructionsLabel, 3);

        var summaryPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(12),
            AutoScroll = true
        };

        _summaryLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(660, 0),
            Text = "No file selected."
        };
        summaryPanel.Controls.Add(_summaryLabel);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0)
        };

        _importButton = new Button
        {
            Text = "Load",
            AutoSize = true,
            Height = 36,
            Width = 100
        };
        _importButton.Click += async (_, _) => await ImportButton_ClickAsync();

        _cancelButton = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            Height = 36,
            Width = 100,
            DialogResult = DialogResult.Cancel
        };

        _statusLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 9, 12, 0),
            Text = "Ready"
        };

        buttonPanel.Controls.Add(_importButton);
        buttonPanel.Controls.Add(_cancelButton);
        buttonPanel.Controls.Add(_statusLabel);

        rootLayout.Controls.Add(headingLabel, 0, 0);
        rootLayout.Controls.Add(fileLayout, 0, 1);
        rootLayout.Controls.Add(summaryPanel, 0, 2);
        rootLayout.Controls.Add(buttonPanel, 0, 3);

        Controls.Add(rootLayout);
        _fileTypeComboBox.SelectedIndexChanged += FileTypeComboBox_SelectedIndexChanged;
        AcceptButton = _importButton;
        CancelButton = _cancelButton;
    }

    private void FileTypeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var isNavlight = _fileTypeComboBox.SelectedIndex == 1;
        _filePathTextBox.Clear();
        _filePathTextBox.PlaceholderText = isNavlight
            ? "Select a Navlight CSV file"
            : "Select an Excel workbook (.xlsx or .xlsm)";
        _instructionsLabel.Text = isNavlight
            ? "CSV columns: Team No, unused, Grade, Name, Team Name, Event Code. Grade is the category; Event Code is the course. One row per competitor."
            : "First worksheet columns: Team Number, Category, Competitor Name, Team Name, Course. One row per competitor. Teams are grouped by number; missing categories and courses are created for the selected event.";
        _summaryLabel.Text = "No file selected.";
        _summaryLabel.ForeColor = SystemColors.ControlText;
        SetStatus("Ready");
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        var isNavlight = _fileTypeComboBox.SelectedIndex == 1;
        using var dialog = new OpenFileDialog
        {
            Filter = isNavlight
                ? "CSV files (*.csv)|*.csv|All Files (*.*)|*.*"
                : "Excel Workbook (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All Files (*.*)|*.*",
            Title = isNavlight ? "Select Navlight CSV file" : "Select team import spreadsheet",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _filePathTextBox.Text = dialog.FileName;
        _summaryLabel.Text = $"Selected file: {Path.GetFileName(dialog.FileName)}";
        _summaryLabel.ForeColor = SystemColors.ControlText;
        SetStatus("Ready");
    }

    internal async Task ImportButton_ClickAsync()
    {
        var eventName = _eventNameTextBox.Text.Trim();
        var filePath = _filePathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(eventName))
        {
            SetStatus("Enter an event name before loading.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            SetStatus("Select a file before loading.", true);
            return;
        }

        if (!File.Exists(filePath))
        {
            SetStatus("The selected file does not exist.", true);
            return;
        }

        ToggleBusyState(true, "Loading file...");

        var databaseUpdatesStarted = false;
        try
        {
            var eventDate = _eventDatePicker.Value.Date;
            var importedTeams = _fileTypeComboBox.SelectedIndex == 1
                ? ReadNavlightCsv(filePath, _hasHeaderCheckBox.Checked)
                : ReadSpreadsheet(filePath, _hasHeaderCheckBox.Checked);
            if (importedTeams.Count == 0)
            {
                throw new InvalidOperationException("The file does not contain any team rows.");
            }

            databaseUpdatesStarted = true;
            var eventId = await _repository.GetOrCreateEventAsync(eventName, eventDate);

            var categories = await _repository.GetCategoriesAsync(eventId);
            var categoryLookup = categories.ToDictionary(item => item.Name.Trim(), item => item.CategoryId, StringComparer.OrdinalIgnoreCase);
            var courses = await _repository.GetCoursesAsync(eventId);
            var courseLookup = courses.ToDictionary(item => item.Name.Trim(), item => item.CourseId, StringComparer.OrdinalIgnoreCase);

            var createdCount = 0;
            var updatedCount = 0;
            var competitorCount = 0;

            foreach (var importedTeam in importedTeams)
            {
                if (!categoryLookup.TryGetValue(importedTeam.CategoryName, out var categoryId))
                {
                    categoryId = await _repository.CreateCategoryAsync(eventId, importedTeam.CategoryName);
                    categoryLookup[importedTeam.CategoryName] = categoryId;
                }

                if (!courseLookup.TryGetValue(importedTeam.CourseName, out var courseId))
                {
                    courseId = await _repository.CreateCourseAsync(eventId, importedTeam.CourseName);
                    courseLookup[importedTeam.CourseName] = courseId;
                }

                var existingTeamId = await _repository.GetTeamIdByNumberAsync(eventId, importedTeam.TeamNumber);
                var registration = existingTeamId.HasValue
                    ? await _repository.GetTeamRegistrationWithTagsAsync(existingTeamId.Value)
                    : new TeamRegistration
                    {
                        EventId = eventId,
                        TeamNumber = importedTeam.TeamNumber,
                        Registered = false
                    };

                registration.TeamNumber = importedTeam.TeamNumber;
                registration.Name = importedTeam.TeamName;
                registration.CategoryId = categoryId;
                registration.CourseId = courseId;
                registration.Competitors = importedTeam.CompetitorNames
                    .Select(name => new CompetitorRecord { Name = name })
                    .ToList();

                await _repository.SaveAdminTeamAsync(registration);

                competitorCount += registration.Competitors.Count;
                if (existingTeamId.HasValue)
                {
                    updatedCount++;
                }
                else
                {
                    createdCount++;
                }
            }

            _summaryLabel.Text = $"Loaded {importedTeams.Count} team(s) into '{eventName}' on {eventDate:d} from {Path.GetFileName(filePath)}. Created: {createdCount}. Updated: {updatedCount}. Competitors: {competitorCount}.";
            _summaryLabel.ForeColor = SystemColors.ControlText;
            SetStatus("File loaded successfully.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            SetStatus(databaseUpdatesStarted
                ? $"{ex.Message}{Environment.NewLine}{Environment.NewLine}Some data may have been loaded. Check the team list before retrying."
                : ex.Message, true);
        }
        finally
        {
            ToggleBusyState(false);
        }
    }

    internal static List<ImportedTeam> ReadSpreadsheet(string filePath, bool hasHeaderRow)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet is null)
        {
            throw new InvalidOperationException("The workbook does not contain any worksheets.");
        }

        var range = worksheet.RangeUsed();
        if (range is null)
        {
            return [];
        }

        var rows = range.RowsUsed().ToList();
        if (rows.Count == 0)
        {
            return [];
        }

        var startIndex = hasHeaderRow ? 1 : 0;
        return GroupImportedRows(rows.Skip(startIndex).Select(row => new ImportedRow(
            row.RowNumber(),
            row.Cell(1).GetString(),
            row.Cell(2).GetString(),
            row.Cell(3).GetString(),
            row.Cell(4).GetString(),
            row.Cell(5).GetString())));
    }

    internal static List<ImportedTeam> ReadNavlightCsv(string filePath, bool hasHeaderRow)
    {
        using var parser = new TextFieldParser(filePath)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        var rows = new List<ImportedRow>();
        var isFirstRow = true;
        while (!parser.EndOfData)
        {
            var rowNumber = parser.LineNumber;
            string[] fields;
            try
            {
                fields = parser.ReadFields()
                    ?? throw new InvalidOperationException($"Could not read CSV row {rowNumber}.");
            }
            catch (MalformedLineException ex)
            {
                throw new InvalidOperationException($"CSV row {ex.LineNumber} is malformed.", ex);
            }

            if (isFirstRow && hasHeaderRow)
            {
                isFirstRow = false;
                continue;
            }

            isFirstRow = false;
            if (fields.Length != 6)
            {
                throw new InvalidOperationException($"CSV row {rowNumber} must contain exactly 6 columns.");
            }

            rows.Add(new ImportedRow(rowNumber, fields[0], fields[2], fields[3], fields[4], fields[5]));
        }

        return GroupImportedRows(rows);
    }

    private static List<ImportedTeam> GroupImportedRows(IEnumerable<ImportedRow> rows)
    {
        var teams = new Dictionary<string, ImportedTeam>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var teamNumber = row.TeamNumber.Trim();
            var categoryName = row.CategoryName.Trim();
            var competitorName = SanitizeImportedName(row.CompetitorName);
            var teamName = SanitizeImportedName(row.TeamName);
            var courseName = row.CourseName.Trim();

            if (string.IsNullOrWhiteSpace(teamNumber) &&
                string.IsNullOrWhiteSpace(categoryName) &&
                string.IsNullOrWhiteSpace(competitorName) &&
                string.IsNullOrWhiteSpace(teamName) &&
                string.IsNullOrWhiteSpace(courseName))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(teamNumber) ||
                string.IsNullOrWhiteSpace(categoryName) ||
                string.IsNullOrWhiteSpace(competitorName) ||
                string.IsNullOrWhiteSpace(teamName) ||
                string.IsNullOrWhiteSpace(courseName))
            {
                throw new InvalidOperationException($"Row {row.RowNumber} is missing one or more required values.");
            }

            if (!teams.TryGetValue(teamNumber, out var importedTeam))
            {
                importedTeam = new ImportedTeam(teamNumber, categoryName, teamName, courseName);
                teams.Add(teamNumber, importedTeam);
            }
            else if (!string.Equals(importedTeam.CategoryName, categoryName, StringComparison.OrdinalIgnoreCase) ||
                     !string.Equals(importedTeam.TeamName, teamName, StringComparison.OrdinalIgnoreCase) ||
                     !string.Equals(importedTeam.CourseName, courseName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Rows for team {teamNumber} contain inconsistent team, category, or course values.");
            }

            if (!importedTeam.CompetitorNames.Contains(competitorName, StringComparer.OrdinalIgnoreCase))
            {
                importedTeam.CompetitorNames.Add(competitorName);
            }
        }

        return teams.Values.OrderBy(team => team.TeamNumber, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string SanitizeImportedName(string value)
    {
        return value
            .Replace("&", "and", StringComparison.Ordinal)
            .Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private void ToggleBusyState(bool busy, string? status = null)
    {
        _eventNameTextBox.Enabled = !busy;
        _eventDatePicker.Enabled = !busy;
        _fileTypeComboBox.Enabled = !busy;
        _hasHeaderCheckBox.Enabled = !busy;
        _filePathTextBox.Enabled = !busy;
        _browseButton.Enabled = !busy;
        _importButton.Enabled = !busy;
        _cancelButton.Enabled = !busy;
        if (status is not null)
        {
            SetStatus(status);
        }
    }

    private void SetStatus(string message, bool isError = false)
    {
        if (isError)
        {
            _summaryLabel.Text = message;
            _summaryLabel.ForeColor = Color.Firebrick;
        }

        _statusLabel.Text = isError ? "\u26A0 Load failed." : message;
        _statusLabel.ForeColor = isError ? Color.Firebrick : Color.DimGray;
    }

    private sealed record ImportedRow(long RowNumber, string TeamNumber, string CategoryName, string CompetitorName, string TeamName, string CourseName);

    internal sealed class ImportedTeam(string teamNumber, string categoryName, string teamName, string courseName)
    {
        public string TeamNumber { get; } = teamNumber;
        public string CategoryName { get; } = categoryName;
        public string TeamName { get; } = teamName;
        public string CourseName { get; } = courseName;
        public List<string> CompetitorNames { get; } = [];
    }
}