using System.Runtime.ExceptionServices;
using Navlight.Registration.App.Models;
using Xunit;

namespace Navlight.Registration.App.Tests;

public sealed class AdminSpreadsheetImportDialogTests
{
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
