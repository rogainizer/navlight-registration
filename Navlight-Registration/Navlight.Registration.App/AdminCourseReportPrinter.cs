using System.Drawing.Printing;
using Navlight.Registration.App.Models;

namespace Navlight.Registration.App;

internal sealed class AdminCourseReportPrinter
{
    private readonly IReadOnlyList<CourseReportSection> _sections;
    private readonly Font _titleFont = new(Control.DefaultFont.FontFamily, 14, FontStyle.Bold);
    private readonly Font _headerFont = new(Control.DefaultFont.FontFamily, 9, FontStyle.Bold);
    private readonly Font _bodyFont = new(Control.DefaultFont.FontFamily, 9);
    private readonly float[] _columnFractions = [0.16f, 0.11f, 0.2f, 0.28f, 0.11f, 0.14f];
    private readonly string[] _columnHeaders = ["Category", "Team #", "Team", "Competitors", "Flight Plan", "Tags"];

    private int _sectionIndex;
    private int _rowIndex;

    public AdminCourseReportPrinter(IReadOnlyList<CourseReportSection> sections)
    {
        _sections = sections;
    }

    public PrintDocument CreateDocument()
    {
        var document = new PrintDocument
        {
            DocumentName = "Navlight Course Report",
            DefaultPageSettings =
            {
                Landscape = true
            }
        };

        document.BeginPrint += (_, _) =>
        {
            _sectionIndex = 0;
            _rowIndex = 0;
        };
        document.PrintPage += PrintDocument_PrintPage;
        return document;
    }

    private void PrintDocument_PrintPage(object? sender, PrintPageEventArgs e)
    {
        if (_sectionIndex >= _sections.Count)
        {
            e.HasMorePages = false;
            return;
        }

        if (e.Graphics is null)
        {
            e.HasMorePages = false;
            return;
        }

        var section = _sections[_sectionIndex];
        var graphics = e.Graphics;
        var bounds = e.MarginBounds;
        var left = bounds.Left;
        var top = bounds.Top;
        var availableWidth = bounds.Width;

        var titleHeight = _titleFont.GetHeight(graphics);
        graphics.DrawString($"Course: {section.CourseName}", _titleFont, Brushes.Black, left, top);

        var y = top + titleHeight + 12;
        var columnWidths = CalculateColumnWidths(availableWidth);
        DrawColumnHeaders(graphics, left, y, columnWidths);
        y += _headerFont.GetHeight(graphics) + 8;

        if (section.Rows.Count == 0)
        {
            graphics.DrawString("No teams for this course.", _bodyFont, Brushes.Black, left, y);
            _sectionIndex++;
            _rowIndex = 0;
            e.HasMorePages = _sectionIndex < _sections.Count;
            return;
        }

        while (_rowIndex < section.Rows.Count)
        {
            var row = section.Rows[_rowIndex];
            var values = new[]
            {
                row.CategoryName,
                row.TeamNumber,
                row.TeamName,
                row.Competitors,
                row.FlightPlan ? "Returned" : "Not returned",
                row.Tags
            };

            var rowHeight = MeasureRowHeight(graphics, values, columnWidths);
            if (y + rowHeight > bounds.Bottom)
            {
                e.HasMorePages = true;
                return;
            }

            DrawRow(graphics, left, y, values, columnWidths, rowHeight);
            y += rowHeight;
            _rowIndex++;
        }

        _sectionIndex++;
        _rowIndex = 0;
        e.HasMorePages = _sectionIndex < _sections.Count;
    }

    private float[] CalculateColumnWidths(int availableWidth)
    {
        return _columnFractions.Select(fraction => availableWidth * fraction).ToArray();
    }

    private void DrawColumnHeaders(Graphics graphics, float left, float top, IReadOnlyList<float> columnWidths)
    {
        var x = left;
        for (var index = 0; index < _columnHeaders.Length; index++)
        {
            var rectangle = new RectangleF(x, top, columnWidths[index], _headerFont.GetHeight(graphics) + 6);
            graphics.FillRectangle(Brushes.Gainsboro, rectangle);
            graphics.DrawRectangle(Pens.Black, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
            graphics.DrawString(_columnHeaders[index], _headerFont, Brushes.Black, rectangle, StringFormat.GenericDefault);
            x += columnWidths[index];
        }
    }

    private float MeasureRowHeight(Graphics graphics, IReadOnlyList<string> values, IReadOnlyList<float> columnWidths)
    {
        var measuredHeight = _bodyFont.GetHeight(graphics) + 8;
        for (var index = 0; index < values.Count; index++)
        {
            var size = graphics.MeasureString(values[index], _bodyFont, (int)Math.Max(1, columnWidths[index] - 8));
            measuredHeight = Math.Max(measuredHeight, size.Height + 8);
        }

        return measuredHeight;
    }

    private void DrawRow(Graphics graphics, float left, float top, IReadOnlyList<string> values, IReadOnlyList<float> columnWidths, float rowHeight)
    {
        var x = left;
        for (var index = 0; index < values.Count; index++)
        {
            var rectangle = new RectangleF(x, top, columnWidths[index], rowHeight);
            graphics.DrawRectangle(Pens.Black, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
            var textRectangle = RectangleF.Inflate(rectangle, -4, -4);
            graphics.DrawString(values[index], _bodyFont, Brushes.Black, textRectangle, StringFormat.GenericDefault);
            x += columnWidths[index];
        }
    }

    internal sealed class CourseReportSection
    {
        public required string CourseName { get; init; }
        public required IReadOnlyList<AdminTeamOverviewRow> Rows { get; init; }
    }
}