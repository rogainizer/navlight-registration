using ClosedXML.Excel;
using Navlight.Registration.App.Models;
using Navlight.Registration.App.Services;

namespace Navlight.Registration.App;

public sealed class AdminSpreadsheetImportDialog : Form
{
    private readonly RegistrationRepository _repository;
    private readonly TextBox _eventNameTextBox;
    private readonly DateTimePicker _eventDatePicker;
    private readonly CheckBox _hasHeaderCheckBox;
    private readonly TextBox _filePathTextBox;
    private readonly Button _browseButton;
    private readonly Button _importButton;
    private readonly Button _cancelButton;
    private readonly Label _statusLabel;
    private readonly Label _summaryLabel;

    public AdminSpreadsheetImportDialog()
    {
        _repository = new RegistrationRepository(DatabaseOptions.Load());

        Text = "Load Teams from Spreadsheet";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(720, 360);
        Width = 780;
        Height = 420;

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
            ColumnCount = 3,
            RowCount = 5,
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

        var fileLabel = new Label
        {
            Text = "Spreadsheet",
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

        var instructionsLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(680, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 8, 0, 0),
            Text = "Use the first worksheet. Expected columns are: Team Number, Category, Competitor Name, Team Name, Course. Multiple rows for the same team are grouped together. Use the header checkbox to skip the first row when the spreadsheet includes headings. The selected event name and date are used to create or reuse the target event. Missing categories and courses are created automatically for that event."
        };

        _hasHeaderCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "Spreadsheet includes a header row",
            Checked = true,
            Margin = new Padding(0, 8, 0, 0)
        };

        fileLayout.Controls.Add(eventNameLabel, 0, 0);
        fileLayout.Controls.Add(_eventNameTextBox, 1, 0);
        fileLayout.SetColumnSpan(_eventNameTextBox, 2);
        fileLayout.Controls.Add(eventDateLabel, 0, 1);
        fileLayout.Controls.Add(_eventDatePicker, 1, 1);
        fileLayout.Controls.Add(_filePathTextBox, 1, 2);
        fileLayout.Controls.Add(_browseButton, 2, 2);
        fileLayout.Controls.Add(fileLabel, 0, 2);
        fileLayout.Controls.Add(_hasHeaderCheckBox, 1, 3);
        fileLayout.SetColumnSpan(_hasHeaderCheckBox, 2);
        fileLayout.Controls.Add(instructionsLabel, 0, 4);
        fileLayout.SetColumnSpan(instructionsLabel, 3);

        var summaryPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(12)
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
        AcceptButton = _importButton;
        CancelButton = _cancelButton;
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Excel Workbook (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All Files (*.*)|*.*",
            Title = "Select team import spreadsheet",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _filePathTextBox.Text = dialog.FileName;
        _summaryLabel.Text = $"Selected file: {Path.GetFileName(dialog.FileName)}";
        SetStatus("Ready");
    }

    private async Task ImportButton_ClickAsync()
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
            SetStatus("Select a spreadsheet before loading.", true);
            return;
        }

        if (!File.Exists(filePath))
        {
            SetStatus("The selected spreadsheet does not exist.", true);
            return;
        }

        ToggleBusyState(true, "Loading spreadsheet...");

        try
        {
            var eventDate = _eventDatePicker.Value.Date;
            var importedTeams = ReadSpreadsheet(filePath, _hasHeaderCheckBox.Checked);
            if (importedTeams.Count == 0)
            {
                throw new InvalidOperationException("The spreadsheet does not contain any team rows.");
            }

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
            SetStatus("Spreadsheet loaded successfully.");
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

    private static List<ImportedTeam> ReadSpreadsheet(string filePath, bool hasHeaderRow)
    {
        using var workbook = new XLWorkbook(filePath);
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
        var teams = new Dictionary<string, ImportedTeam>(StringComparer.OrdinalIgnoreCase);

        for (var rowIndex = startIndex; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var teamNumber = row.Cell(1).GetString().Trim();
            var categoryName = row.Cell(2).GetString().Trim();
            var competitorName = SanitizeImportedName(row.Cell(3).GetString());
            var teamName = SanitizeImportedName(row.Cell(4).GetString());
            var courseName = row.Cell(5).GetString().Trim();

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
                throw new InvalidOperationException($"Row {row.RowNumber()} is missing one or more required values.");
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

    private void ToggleBusyState(bool busy, string status = "Ready")
    {
        _eventNameTextBox.Enabled = !busy;
        _eventDatePicker.Enabled = !busy;
        _hasHeaderCheckBox.Enabled = !busy;
        _filePathTextBox.Enabled = !busy;
        _browseButton.Enabled = !busy;
        _importButton.Enabled = !busy;
        _cancelButton.Enabled = !busy;
        SetStatus(status);
    }

    private void SetStatus(string message, bool isError = false)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = isError ? Color.Firebrick : Color.DimGray;
    }

    private sealed class ImportedTeam(string teamNumber, string categoryName, string teamName, string courseName)
    {
        public string TeamNumber { get; } = teamNumber;
        public string CategoryName { get; } = categoryName;
        public string TeamName { get; } = teamName;
        public string CourseName { get; } = courseName;
        public List<string> CompetitorNames { get; } = [];
    }
}