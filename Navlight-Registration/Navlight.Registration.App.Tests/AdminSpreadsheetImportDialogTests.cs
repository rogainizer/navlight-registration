using System.Runtime.ExceptionServices;
using Navlight.Registration.App.Models;
using Xunit;

namespace Navlight.Registration.App.Tests;

public sealed class AdminSpreadsheetImportDialogTests
{
    [Fact]
    public void ImportFailure_RemainsVisibleAfterControlsAreReenabled()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "101, ,XV,Alex,Team");
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var dialog = new AdminSpreadsheetImportDialog(new DatabaseOptions());
                    var root = Assert.IsType<TableLayoutPanel>(Assert.Single(dialog.Controls.Cast<Control>()));
                    var fileLayout = Assert.IsType<TableLayoutPanel>(root.GetControlFromPosition(0, 1));
                    var eventName = Assert.IsType<TextBox>(fileLayout.GetControlFromPosition(1, 0));
                    var fileType = Assert.IsType<ComboBox>(fileLayout.GetControlFromPosition(1, 2));
                    var filePath = Assert.IsType<TextBox>(fileLayout.GetControlFromPosition(1, 3));
                    var hasHeader = Assert.IsType<CheckBox>(fileLayout.GetControlFromPosition(1, 4));
                    var summary = Assert.IsType<Panel>(root.GetControlFromPosition(0, 2));
                    var summaryLabel = Assert.IsType<Label>(Assert.Single(summary.Controls.Cast<Control>()));
                    var footer = Assert.IsType<FlowLayoutPanel>(root.GetControlFromPosition(0, 3));
                    var status = Assert.Single(footer.Controls.OfType<Label>());
                    var loadButton = Assert.Single(footer.Controls.OfType<Button>(), button => button.Text == "Load");

                    eventName.Text = "Event";
                    fileType.SelectedIndex = 1;
                    filePath.Text = path;
                    hasHeader.Checked = false;
                    dialog.ImportButton_ClickAsync().GetAwaiter().GetResult();

                    Assert.Contains("exactly 6 columns", summaryLabel.Text);
                    Assert.Contains("Load failed", status.Text);
                    Assert.Equal(Color.Firebrick, summaryLabel.ForeColor);
                    Assert.True(loadButton.Enabled);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure is not null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(820, 650)]
    [InlineData(780, 600)]
    public void FileSelector_IsVisibleWithinDialogLayout(int width, int height)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var dialog = new AdminSpreadsheetImportDialog(new DatabaseOptions());
                using var font = new Font("Segoe UI", 15f);
                dialog.Font = font;
                dialog.Size = new Size(width, height);
                dialog.CreateControl();
                dialog.PerformLayout();

                var root = Assert.IsType<TableLayoutPanel>(Assert.Single(dialog.Controls.Cast<Control>()));
                var fileLayout = Assert.IsType<TableLayoutPanel>(root.GetControlFromPosition(0, 1));
                var fileType = Assert.IsType<ComboBox>(fileLayout.GetControlFromPosition(1, 2));
                var filePath = Assert.IsType<TextBox>(fileLayout.GetControlFromPosition(1, 3));
                var browse = Assert.IsType<Button>(fileLayout.GetControlFromPosition(2, 3));
                var instructions = Assert.IsType<Label>(fileLayout.GetControlFromPosition(0, 5));
                var summary = Assert.IsType<Panel>(root.GetControlFromPosition(0, 2));
                var footer = Assert.IsType<FlowLayoutPanel>(root.GetControlFromPosition(0, 3));

                Assert.Equal("Spreadsheet", fileType.SelectedItem);
                filePath.Text = "old.xlsx";

                foreach (var index in new[] { 0, 1 })
                {
                    fileType.SelectedIndex = index;
                    if (index == 1)
                    {
                        Assert.Empty(filePath.Text);
                        Assert.Contains("CSV", filePath.PlaceholderText);
                    }

                    Assert.True(fileLayout.ClientRectangle.Contains(fileType.Bounds), "The file type selector is clipped.");
                    Assert.True(fileLayout.ClientRectangle.Contains(filePath.Bounds), "The file path field is clipped.");
                    Assert.True(fileLayout.ClientRectangle.Contains(browse.Bounds), "The browse button is clipped.");
                    Assert.True(fileLayout.ClientRectangle.Contains(instructions.Bounds), "The instructions are clipped.");
                    Assert.True(root.ClientRectangle.Contains(fileLayout.Bounds), "The file selector layout is clipped.");
                    Assert.True(root.ClientRectangle.Contains(summary.Bounds), "The summary is clipped.");
                    Assert.True(root.ClientRectangle.Contains(footer.Bounds), "The action buttons are clipped.");
                    Assert.True(summary.Height >= 30 && footer.Top >= summary.Bottom, "The summary and action buttons overlap.");
                    Assert.True(filePath.Width >= 100, "The file path field is too narrow.");
                    Assert.True(filePath.Height > 0 && browse.Height > 0, "The file selector has no height.");
                }

                filePath.Text = "new.csv";
                fileType.SelectedIndex = 0;
                Assert.Empty(filePath.Text);
                Assert.Contains("Excel", filePath.PlaceholderText);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
