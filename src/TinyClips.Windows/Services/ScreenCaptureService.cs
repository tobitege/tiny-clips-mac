using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace TinyClips.Windows.Services;

public static class ScreenCaptureService
{
    public static Bitmap CaptureRegion(Int32Rect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rect), "Capture region must have a positive width and height.");
        }

        var bitmap = new Bitmap(rect.Width, rect.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(rect.X, rect.Y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var handle = bitmap.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            Vanara.PInvoke.Gdi32.DeleteObject(handle);
        }
    }

    public static string SaveBitmap(Bitmap source, string outputPath, ImageFormat? format = null, long jpegQuality = 90)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output path must include a directory.", nameof(outputPath));
        }

        Directory.CreateDirectory(outputDirectory);

        using var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        if (format == ImageFormat.Jpeg)
        {
            var codec = ImageCodecInfo.GetImageDecoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
            var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(Encoder.Quality, jpegQuality);
            source.Save(stream, codec, parameters);
        }
        else
        {
            source.Save(stream, ImageFormat.Png);
        }

        return outputPath;
    }
}
