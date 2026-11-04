using SkiaSharp;
using Pactogg.Api.Common;

namespace Pactogg.Api.Modules.Evidence;

public static class ImageProcessor
{
    private const int MaxDimension = 8000;

    public static byte[] Normalize(byte[] content)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(content);
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                throw ApiException.Unprocessable(ErrorCodes.UnsupportedMediaType, "La imagen no es válida.");
            }

            if (bitmap.Width > MaxDimension || bitmap.Height > MaxDimension)
            {
                throw ApiException.Unprocessable(ErrorCodes.ImageTooLarge, "La imagen supera las dimensiones permitidas.");
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            if (data is null || data.Size == 0)
            {
                throw ApiException.Unprocessable(ErrorCodes.UnsupportedMediaType, "No se pudo procesar la imagen.");
            }

            return data.ToArray();
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception)
        {
            throw ApiException.Unprocessable(ErrorCodes.UnsupportedMediaType, "La imagen no es válida.");
        }
    }
}
