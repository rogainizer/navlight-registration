using Navlight.Registration.App.Services;
using Xunit;

namespace Navlight.Registration.App.Tests;

public sealed class NavLightTagReaderTests
{
    [Fact]
    public void ParseTagIdResponse_ReturnsTagDetailsForValidReply()
    {
        var result = NavLightTagReader.ParseTagIdResponse("5A 00 01 02 03 60");

        Assert.Equal("000102", result.TagIdHex);
        Assert.Equal("AJAL", result.TagIdAlpha);
        Assert.Equal("03", result.CourseHex);
        Assert.Equal("5A 00 01 02 03 60", result.RawReply);
    }

    [Theory]
    [InlineData("5A 00 01 02 03", "format")]
    [InlineData("00 00 01 02 03 06", "prefix")]
    [InlineData("5A 00 01 02 03 00", "checksum")]
    public void ParseTagIdResponse_RejectsMalformedReply(string reply, string expectedMessage)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => NavLightTagReader.ParseTagIdResponse(reply));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateErasePreparationResponse_AcceptsMatchingChecksumIgnoringCase()
    {
        NavLightTagReader.ValidateErasePreparationResponse("5a 03 additional data", "03");
    }

    [Theory]
    [InlineData("", "format")]
    [InlineData("00 03", "prefix")]
    [InlineData("5A 04", "checksum")]
    public void ValidateErasePreparationResponse_RejectsInvalidReply(string reply, string expectedMessage)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => NavLightTagReader.ValidateErasePreparationResponse(reply, "03"));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAckResponse_AcceptsPositiveAcknowledgementIgnoringCase()
    {
        NavLightTagReader.ValidateAckResponse("5a OK");
    }

    [Fact]
    public void ValidateAckResponse_RejectsNegativeAcknowledgement()
    {
        Assert.Throws<InvalidOperationException>(
            () => NavLightTagReader.ValidateAckResponse("00 failed"));
    }

    [Theory]
    [InlineData("000102", "03")]
    [InlineData("80FF01", "80")]
    public void CalculateBaseIdSumHex_ReturnsByteSumModulo256(string tagIdHex, string expected)
    {
        Assert.Equal(expected, NavLightTagReader.CalculateBaseIdSumHex(tagIdHex));
    }

    [Fact]
    public void CalculateBaseIdSumHex_RejectsTagIdWithIncorrectLength()
    {
        Assert.Throws<ArgumentException>(() => NavLightTagReader.CalculateBaseIdSumHex("1234"));
    }
}
