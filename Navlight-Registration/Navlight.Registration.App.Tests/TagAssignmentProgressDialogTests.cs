using System.Runtime.ExceptionServices;
using Xunit;

namespace Navlight.Registration.App.Tests;

public sealed class TagAssignmentProgressDialogTests
{
    [Fact]
    public void StatusAndAllAssignedTags_AreVisibleAtAppFontSize()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var dialog = new TagAssignmentProgressDialog([], 3);
                using var font = new Font("Segoe UI", 15f);
                dialog.Font = font;
                dialog.CreateControl();
                dialog.PerformLayout();

                var layout = Assert.IsType<TableLayoutPanel>(Assert.Single(dialog.Controls.Cast<Control>()));
                var status = Assert.IsType<TextBox>(layout.GetControlFromPosition(0, 1));
                var heading = Assert.IsType<Label>(layout.GetControlFromPosition(0, 0));
                var tags = Assert.IsType<ListBox>(layout.GetControlFromPosition(0, 2));
                var close = Assert.IsType<Button>(layout.GetControlFromPosition(0, 3));
                using var headingFont = new Font(font, FontStyle.Bold);
                heading.Font = headingFont;

                dialog.AddTag("ABCD");
                dialog.SetStatus("Waiting up to 10 seconds for another tag...");
                dialog.AddTag("EFGH");
                Assert.Equal(new[] { "ABCD", "EFGH" }, tags.Items.Cast<string>());
                Assert.True(tags.ClientRectangle.Contains(tags.GetItemRectangle(1)), "The second tag is not visible.");
                dialog.AddTag("JKLM");
                dialog.AddTag("EFGH");

                Assert.Equal(new[] { "ABCD", "EFGH", "JKLM" }, tags.Items.Cast<string>());
                Assert.True(status.Multiline && status.ReadOnly && status.ScrollBars == ScrollBars.Vertical);
                foreach (var message in new[]
                {
                    "Waiting up to 10 seconds for another tag...",
                    "Read tag ABCD again. Waiting for a different tag...",
                    "Tag ABCD is already assigned to team 101 - The Long Team Name."
                })
                {
                    dialog.SetStatus(message);
                    Assert.Equal(message, status.Text);
                    Assert.True(layout.ClientRectangle.Contains(status.Bounds), $"The status message is clipped: {message}");
                    var end = status.GetPositionFromCharIndex(message.Length - 1);
                    Assert.True(end.Y + status.Font.Height <= status.ClientSize.Height, $"The status message is not fully visible: {message}");
                    Assert.True(layout.ClientRectangle.Contains(tags.Bounds), "The tag list is clipped.");
                    Assert.True(layout.ClientRectangle.Contains(close.Bounds), "The close button is clipped.");
                    for (var index = 0; index < tags.Items.Count; index++)
                    {
                        Assert.True(tags.ClientRectangle.Contains(tags.GetItemRectangle(index)), $"Tag {index + 1} is not visible.");
                    }
                }
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
