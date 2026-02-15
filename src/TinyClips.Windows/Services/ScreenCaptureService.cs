using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Media.Imaging;

namespace TinyClips.Windows.Services;

public static class ScreenCaptureService
{
    public static Bitmap CaptureRegion(Int32Rect rect)
    {
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

    public static string SaveBitmap(Bitmap source, string directory, ImageFormat? format = null, long jpegQuality = 90)
    {
        Directory.CreateDirectory(directory);
        var extension = format == ImageFormat.Jpeg ? "jpg" : "png";
        var fileName = $"tinyclip-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}";
        var path = Path.Combine(directory, fileName);

        if (format == ImageFormat.Jpeg)
        {
            var codec = ImageCodecInfo.GetImageDecoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
            var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(Encoder.Quality, jpegQuality);
            source.Save(path, codec, parameters);
        }
        else
        {
            source.Save(path, ImageFormat.Png);
        }

        return path;
    }
}
