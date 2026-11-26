using System.Runtime.ExceptionServices;
using Xunit;

namespace Navlight.Registration.App.Tests;

public sealed class NavlightBrandingTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(48)]
    public void WindowIcon_HasReadableTransparentArtworkAtEachSize(int size)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form();
                NavlightBranding.ApplyWindowIcon(form);
                var windowIcon = form.Icon;
                Assert.NotNull(windowIcon);
                using var icon = new Icon(windowIcon, new Size(size, size));
                using var bitmap = icon.ToBitmap();

                Assert.Equal(new Size(size, size), icon.Size);
                var redPixels = 0;
                var transparentPixels = 0;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        if (pixel.A >= 200 && pixel.R > 180 && pixel.G < 130 && pixel.B < 130)
                        {
                            redPixels++;
                        }

                        if (pixel.A < 32)
                        {
                            transparentPixels++;
                        }
                    }
                }

                Assert.True(redPixels > size * size / 5, "The icon should contain the filled logo, not just an outline.");
                Assert.True(transparentPixels > size * size / 10, "The icon background should be transparent.");
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
