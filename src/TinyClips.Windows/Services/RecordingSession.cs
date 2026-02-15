using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TinyClips.Windows.Models;
using TinyCaptureType = TinyClips.Windows.Models.CaptureType;

namespace TinyClips.Windows.Services;

public sealed class RecordingSession : IDisposable
{
    private readonly List<string> _framePaths = [];
    private readonly CaptureSettings _settings;
    private readonly Int32Rect _region;
    private readonly TinyCaptureType _captureType;
    private readonly int _frameRate;
    private readonly object _sync = new();
    private CancellationTokenSource? _captureCts;
    private Task? _captureLoopTask;
    private string? _frameDirectory;
    private int _nextFrameIndex;
    private bool _isStarted;
    private bool _isCaptureStopped;

    public RecordingSession(TinyCaptureType captureType, Int32Rect region, CaptureSettings settings)
    {
        _captureType = captureType;
        _region = region;
        _settings = settings;
        _frameRate = captureType == TinyCaptureType.Video ? settings.VideoFrameRate : settings.GifFrameRate;
    }

    public TinyCaptureType CaptureType => _captureType;

    public void Start()
    {
        if (_isStarted)
        {
            throw new InvalidOperationException("Recording has already started.");
        }

        _frameDirectory = Path.Combine(Path.GetTempPath(), "TinyClips.Windows", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_frameDirectory);
        _nextFrameIndex = 0;
        _captureCts = new CancellationTokenSource();
        _captureLoopTask = Task.Run(() => CaptureLoopAsync(_captureCts.Token));
        _isStarted = true;
        _isCaptureStopped = false;
    }

    public async Task<string> StopAndSaveAsync(string outputPath)
    {
        ValidateOutputPath(outputPath);
        if (_isStarted)
        {
            await StopCaptureAsync();
        }

        if (!_isCaptureStopped)
        {
            throw new InvalidOperationException("Recording has not been started.");
        }

        List<string> framePaths;
        lock (_sync)
        {
            framePaths = [.. _framePaths];
        }

        if (framePaths.Count == 0)
        {
            throw new InvalidOperationException("No frames were captured.");
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output path must include a directory.", nameof(outputPath));
        }

        Directory.CreateDirectory(outputDirectory);
        if (File.Exists(outputPath))
        {
            throw new IOException($"Output file already exists: {outputPath}");
        }

        if (_captureType == TinyCaptureType.Video)
        {
            await Task.Run(() => SaveVideo(framePaths, outputPath));
        }
        else
        {
            await Task.Run(() => SaveGif(framePaths, outputPath));
        }

        CleanupTemporaryFrames();
        return outputPath;
    }

    private async Task StopCaptureAsync()
    {
        _isStarted = false;
        _isCaptureStopped = true;
        if (_captureCts is null || _captureLoopTask is null)
        {
            return;
        }

        _captureCts.Cancel();
        try
        {
            await _captureLoopTask;
        }
        catch (OperationCanceledException)
        {
        }

        _captureCts.Dispose();
        _captureCts = null;
        _captureLoopTask = null;
    }

    private async Task CaptureLoopAsync(CancellationToken cancellationToken)
    {
        CaptureFrameToTemporaryFile();

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000d / _frameRate));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            CaptureFrameToTemporaryFile();
        }
    }

    private void CaptureFrameToTemporaryFile()
    {
        if (string.IsNullOrWhiteSpace(_frameDirectory))
        {
            throw new InvalidOperationException("Capture session is not initialized.");
        }

        var framePath = Path.Combine(_frameDirectory, $"{Interlocked.Increment(ref _nextFrameIndex):D8}.png");
        using var bitmap = ScreenCaptureService.CaptureRegion(_region);
        bitmap.Save(framePath, System.Drawing.Imaging.ImageFormat.Png);
        lock (_sync)
        {
            _framePaths.Add(framePath);
        }
    }

    private void SaveVideo(IReadOnlyList<string> framePaths, string outputPath)
    {
        using var writer = new VideoWriter(outputPath, FourCC.FromString("mp4v"), _frameRate, new OpenCvSharp.Size(_region.Width, _region.Height));
        if (!writer.IsOpened())
        {
            throw new InvalidOperationException($"Unable to create video file: {outputPath}");
        }

        foreach (var framePath in framePaths)
        {
            using var mat = Cv2.ImRead(framePath, ImreadModes.Color);
            if (mat.Empty())
            {
                throw new InvalidOperationException($"Captured frame could not be read: {framePath}");
            }

            writer.Write(mat);
        }
    }

    private void SaveGif(IReadOnlyList<string> framePaths, string outputPath)
    {
        using var firstImage = ConvertFrame(framePaths[0]);
        var frameDelay = Math.Max(1, 100 / _settings.GifFrameRate);
        firstImage.Metadata.GetGifMetadata().RepeatCount = 0;
        firstImage.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = frameDelay;

        for (var i = 1; i < framePaths.Count; i++)
        {
            using var img = ConvertFrame(framePaths[i]);
            img.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = frameDelay;
            firstImage.Frames.AddFrame(img.Frames.RootFrame);
        }

        firstImage.Save(outputPath, new GifEncoder());
    }

    private void ValidateOutputPath(string outputPath)
    {
        var extension = Path.GetExtension(outputPath);
        if (_captureType == TinyCaptureType.Video && !string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Video output path must end with .mp4", nameof(outputPath));
        }

        if (_captureType == TinyCaptureType.Gif && !string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("GIF output path must end with .gif", nameof(outputPath));
        }
    }

    public void Dispose()
    {
        _isStarted = false;
        _isCaptureStopped = false;
        _captureCts?.Cancel();
        try
        {
            _captureLoopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }
        finally
        {
            _captureCts?.Dispose();
            _captureCts = null;
            _captureLoopTask = null;
            CleanupTemporaryFrames();
        }
    }

    private void CleanupTemporaryFrames()
    {
        var frameDirectory = _frameDirectory;
        _frameDirectory = null;
        _isCaptureStopped = false;
        lock (_sync)
        {
            _framePaths.Clear();
        }

        if (!string.IsNullOrWhiteSpace(frameDirectory))
        {
            try
            {
                if (Directory.Exists(frameDirectory))
                {
                    Directory.Delete(frameDirectory, true);
                }
            }
            catch
            {
            }
        }
    }

    private Image<Rgba32> ConvertFrame(string framePath)
    {
        var image = SixLabors.ImageSharp.Image.Load<Rgba32>(framePath);

        if (image.Width > _settings.GifMaxWidth)
        {
            var ratio = _settings.GifMaxWidth / (double)image.Width;
            image.Mutate(x => x.Resize(_settings.GifMaxWidth, (int)Math.Round(image.Height * ratio)));
        }

        return image;
    }
}
