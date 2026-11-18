using ClosedXML.Excel;
using Xunit;

namespace Navlight.Registration.App.Tests;

public sealed class TeamImportTests
{
    [Fact]
    public void NavlightCsv_MapsAndGroupsTeamRows()
    {
        const string csv = """
            "Team No"," ",Grade,Name,"Team Name","Event Code"
            101," ",XV,"Leon Perrie ",L2,"24 hour"
            101," ",XV,"Lara Shepherd ",L2,"24 hour"
            102," ",WO,"Anna Pellett ","Powered by Chocolate","24 hour"
            """;

        WithCsv(csv, path =>
        {
            var teams = AdminSpreadsheetImportDialog.ReadNavlightCsv(path, true);

            Assert.Equal(2, teams.Count);
            Assert.Equal("101", teams[0].TeamNumber);
            Assert.Equal("XV", teams[0].CategoryName);
            Assert.Equal("L2", teams[0].TeamName);
            Assert.Equal("24 hour", teams[0].CourseName);
            Assert.Equal(new[] { "Leon Perrie", "Lara Shepherd" }, teams[0].CompetitorNames);
            Assert.Equal("102", teams[1].TeamNumber);
            Assert.Equal("WO", teams[1].CategoryName);
            Assert.Equal("Powered by Chocolate", teams[1].TeamName);
            Assert.Equal(new[] { "Anna Pellett" }, teams[1].CompetitorNames);
        });
    }

    [Fact]
    public void NavlightCsv_HandlesQuotedFieldsAndNoHeader()
    {
        const string csv = """
            001, ,XV,"Alex ""Ace"" Example","Team, One","24, hour"
            001, ,xv,"alex ""ace"" example","team, one","24, hour"
            """;

        WithCsv(csv, path =>
        {
            var team = Assert.Single(AdminSpreadsheetImportDialog.ReadNavlightCsv(path, false));
            Assert.Equal("001", team.TeamNumber);
            Assert.Equal("Team One", team.TeamName);
            Assert.Equal("24, hour", team.CourseName);
            Assert.Equal(new[] { "Alex \"Ace\" Example" }, team.CompetitorNames);
        });
    }

    [Theory]
    [InlineData("101, ,XV,Alex,Team", "exactly 6 columns")]
    [InlineData("101, ,XV,,Team,24 hour", "missing one or more required values")]
    [InlineData("101, ,XV,\"unclosed,Team,24 hour", "malformed")]
    [InlineData("101, ,XV,Alex,Team,24 hour\n101, ,WO,Sam,Team,24 hour", "inconsistent")]
    public void NavlightCsv_RejectsInvalidRows(string csv, string expectedMessage)
    {
        WithCsv(csv, path =>
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => AdminSpreadsheetImportDialog.ReadNavlightCsv(path, false));
            Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Spreadsheet_StillGroupsAndSanitizesRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("Teams");
                sheet.Cell(1, 1).Value = "Team Number";
                sheet.Cell(2, 1).Value = "101";
                sheet.Cell(2, 2).Value = "XV";
                sheet.Cell(2, 3).Value = "Alex & Sam";
                sheet.Cell(2, 4).Value = "Team (One)";
                sheet.Cell(2, 5).Value = "24 hour";
                sheet.Cell(3, 1).Value = "101";
                sheet.Cell(3, 2).Value = "XV";
                sheet.Cell(3, 3).Value = "Jamie";
                sheet.Cell(3, 4).Value = "Team (One)";
                sheet.Cell(3, 5).Value = "24 hour";
                workbook.SaveAs(path);
            }

            var team = Assert.Single(AdminSpreadsheetImportDialog.ReadSpreadsheet(path, true));
            Assert.Equal("Team One", team.TeamName);
            Assert.Equal(new[] { "Alex and Sam", "Jamie" }, team.CompetitorNames);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WithCsv(string contents, Action<string> action)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, contents);
            action(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
