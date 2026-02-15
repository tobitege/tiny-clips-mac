using System.Drawing;
using System.Windows;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TinyClips.Windows.Models;

namespace TinyClips.Windows.Services;

public sealed class RecordingSession : IDisposable
{
    private readonly List<Bitmap> _frames = [];
    private readonly CaptureSettings _settings;
    private readonly Int32Rect _region;
    private readonly CaptureType _captureType;
    private readonly int _frameRate;
    private readonly object _sync = new();
    private System.Threading.Timer? _timer;

    public RecordingSession(CaptureType captureType, Int32Rect region, CaptureSettings settings)
    {
        _captureType = captureType;
        _region = region;
        _settings = settings;
        _frameRate = captureType == CaptureType.Video ? settings.VideoFrameRate : settings.GifFrameRate;
    }

    public void Start()
    {
        var frameDuration = TimeSpan.FromMilliseconds(1000d / _frameRate);
        _timer = new System.Threading.Timer(_ => CaptureFrame(), null, TimeSpan.Zero, frameDuration);
    }

    public async Task<string> StopAndSaveAsync()
    {
        _timer?.Dispose();
        _timer = null;

        List<Bitmap> frames;
        lock (_sync)
        {
            frames = [.. _frames];
            _frames.Clear();
        }

        if (frames.Count == 0)
        {
            throw new InvalidOperationException("No frames were captured.");
        }

        Directory.CreateDirectory(_settings.SaveDirectory);

        var output = _captureType == CaptureType.Video
            ? Path.Combine(_settings.SaveDirectory, $"tinyclip-{DateTime.Now:yyyyMMdd-HHmmss}.mp4")
            : Path.Combine(_settings.SaveDirectory, $"tinyclip-{DateTime.Now:yyyyMMdd-HHmmss}.gif");

        if (_captureType == CaptureType.Video)
        {
            await Task.Run(() => SaveVideo(frames, output));
        }
        else
        {
            await Task.Run(() => SaveGif(frames, output));
        }

        foreach (var frame in frames)
        {
            frame.Dispose();
        }

        return output;
    }

    private void CaptureFrame()
    {
        var bitmap = ScreenCaptureService.CaptureRegion(_region);
        lock (_sync)
        {
            _frames.Add(bitmap);
        }
    }

    private void SaveVideo(IReadOnlyList<Bitmap> frames, string outputPath)
    {
        using var writer = new VideoWriter(outputPath, FourCC.FromString("mp4v"), _frameRate, new OpenCvSharp.Size(_region.Width, _region.Height));
        foreach (var frame in frames)
        {
            using var mat = BitmapConverter.ToMat(frame);
            writer.Write(mat);
        }
    }

    private void SaveGif(IReadOnlyList<Bitmap> frames, string outputPath)
    {
        using var firstImage = ConvertFrame(frames[0]);
        var frameDelay = Math.Max(1, 100 / _settings.GifFrameRate);
        firstImage.Metadata.GetGifMetadata().RepeatCount = 0;
        firstImage.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = frameDelay;

        for (var i = 1; i < frames.Count; i++)
        {
            using var img = ConvertFrame(frames[i]);
            img.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = frameDelay;
            firstImage.Frames.AddFrame(img.Frames.RootFrame);
        }

        firstImage.Save(outputPath, new GifEncoder());
    }


    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;

        lock (_sync)
        {
            foreach (var frame in _frames)
            {
                frame.Dispose();
            }

            _frames.Clear();
        }
    }
    private Image<Rgba32> ConvertFrame(Bitmap frame)
    {
        using var stream = new MemoryStream();
        frame.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;
        var image = Image.Load<Rgba32>(stream);

        if (image.Width > _settings.GifMaxWidth)
        {
            var ratio = _settings.GifMaxWidth / (double)image.Width;
            image.Mutate(x => x.Resize(_settings.GifMaxWidth, (int)Math.Round(image.Height * ratio)));
        }

        return image;
    }
}
