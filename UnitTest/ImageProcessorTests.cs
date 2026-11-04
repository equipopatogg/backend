using FluentAssertions;
using Pactogg.Api.Common;
using Pactogg.Api.Modules.Evidence;
using SkiaSharp;

namespace UnitTest;

public sealed class ImageProcessorTests
{
    [Fact]
    public void Normalize_ConvertsValidImageToJpeg()
    {
        using var bitmap = new SKBitmap(4, 4);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);

        var result = ImageProcessor.Normalize(png.ToArray());

        result.Should().NotBeEmpty();
        result[0].Should().Be(0xFF);
        result[1].Should().Be(0xD8);
    }

    [Fact]
    public void Normalize_RejectsGarbage()
    {
        var action = () => ImageProcessor.Normalize([1, 2, 3, 4]);

        action.Should().Throw<ApiException>().Which.Code.Should().Be(ErrorCodes.UnsupportedMediaType);
    }
}
