using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using TinyClips.Windows.Models;
using TinyClips.Windows.Services;
using TinyClips.Windows.Views;

namespace TinyClips.Windows;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private string _statusMessage = "Ready.";
    private RecordingSession? _activeRecording;

    public CaptureSettings Settings { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        Settings = CaptureSettings.LoadDefault();
        DataContext = this;
    }

    private async void OnScreenshotClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var region = await SelectRegionAsync();
            if (region is null)
            {
                StatusMessage = "Capture canceled.";
                return;
            }

            var output = ResolveOutputPath(CaptureType.Screenshot);
            if (output is null)
            {
                StatusMessage = "Capture canceled.";
                return;
            }

            using var bitmap = ScreenCaptureService.CaptureRegion(region.Value);
            while (true)
            {
                try
                {
                    ScreenCaptureService.SaveBitmap(bitmap, output);
                    break;
                }
                catch (IOException)
                {
                    output = PromptForUniqueOutputPath(CaptureType.Screenshot, output);
                    if (output is null)
                    {
                        StatusMessage = "Capture canceled.";
                        return;
                    }
                }
            }
            string? clipboardFailureMessage = null;

            if (Settings.CopyToClipboard)
            {
                try
                {
                    System.Windows.Clipboard.SetImage(ScreenCaptureService.ToBitmapSource(bitmap));
                }
                catch (Exception ex)
                {
                    clipboardFailureMessage = ex.Message;
                }
            }

            PostSave(output);
            StatusMessage = clipboardFailureMessage is null
                ? $"Screenshot saved: {output}"
                : $"Screenshot saved: {output}. Clipboard failed: {clipboardFailureMessage}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Screenshot failed: {ex.Message}";
        }
    }

    private async void OnRecordVideoClicked(object sender, RoutedEventArgs e) =>
        await StartRecordingAsync(CaptureType.Video);

    private async void OnRecordGifClicked(object sender, RoutedEventArgs e) =>
        await StartRecordingAsync(CaptureType.Gif);

    private async Task StartRecordingAsync(CaptureType type)
    {
        if (_activeRecording is not null)
        {
            StatusMessage = "A recording is already active.";
            return;
        }

        try
        {
            var region = await SelectRegionAsync();
            if (region is null)
            {
                StatusMessage = "Selection canceled.";
                return;
            }

            var countdownEnabled = type == CaptureType.Video ? Settings.VideoCountdownEnabled : Settings.GifCountdownEnabled;
            var countdownDuration = type == CaptureType.Video ? Settings.VideoCountdownDuration : Settings.GifCountdownDuration;

            if (countdownEnabled)
            {
                await CountdownService.RunAsync(countdownDuration, remaining => Dispatcher.Invoke(() =>
                    StatusMessage = $"{type} starting in {remaining}..."));
            }

            _activeRecording = new RecordingSession(type, region.Value, Settings);
            _activeRecording.Start();
            StatusMessage = $"{type} recording... click 'Stop Active Recording' to finish.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to start recording: {ex.Message}";
        }
    }

    private async void OnStopRecordingClicked(object sender, RoutedEventArgs e)
    {
        if (_activeRecording is null)
        {
            StatusMessage = "No active recording.";
            return;
        }

        try
        {
            var recording = _activeRecording;
            var output = ResolveOutputPath(recording.CaptureType);
            if (output is null)
            {
                StatusMessage = "Finalize canceled. Recording continues.";
                return;
            }

            while (true)
            {
                try
                {
                    StatusMessage = "Finalizing recording...";
                    output = await recording.StopAndSaveAsync(output);
                    break;
                }
                catch (IOException)
                {
                    output = PromptForUniqueOutputPath(recording.CaptureType, output);
                    if (output is null)
                    {
                        StatusMessage = "Finalize canceled. Recording data is still available.";
                        return;
                    }
                }
            }

            _activeRecording = null;
            PostSave(output);
            StatusMessage = $"Recording saved: {output}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Finalize failed: {ex.Message}";
        }
    }

    private void OnOpenFolderClicked(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Settings.SaveDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Settings.SaveDirectory}\"") { UseShellExecute = true });
    }

    private void OnBrowseFolderClicked(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Choose where captures are saved",
            InitialDirectory = Settings.SaveDirectory,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            Settings.SaveDirectory = dialog.SelectedPath;
            Settings.Save();
            OnPropertyChanged(nameof(Settings));
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _activeRecording?.Dispose();
        _activeRecording = null;
        Settings.Save();
        base.OnClosing(e);
    }

    private async Task<Int32Rect?> SelectRegionAsync()
    {
        StatusMessage = "Select a region...";
        return await RegionSelectionWindow.SelectRegionAsync(this);
    }

    private void PostSave(string output)
    {
        if (Settings.OpenFolderAfterSave)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{output}\"") { UseShellExecute = true });
        }
    }

    private string? ResolveOutputPath(CaptureType captureType)
    {
        Directory.CreateDirectory(Settings.SaveDirectory);
        var extension = GetExtension(captureType);
        var defaultPath = Path.Combine(Settings.SaveDirectory, $"tinyclip-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}");
        if (!File.Exists(defaultPath))
        {
            return defaultPath;
        }

        return PromptForUniqueOutputPath(captureType, defaultPath);
    }

    private string? PromptForUniqueOutputPath(CaptureType captureType, string initialPath)
    {
        var extension = GetExtension(captureType);
        var currentDirectory = Path.GetDirectoryName(initialPath) ?? Settings.SaveDirectory;
        var currentFileName = Path.GetFileName(initialPath);

        while (true)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Select a new filename",
                InitialDirectory = currentDirectory,
                FileName = currentFileName,
                DefaultExt = extension,
                AddExtension = true,
                Filter = GetFilter(captureType),
                OverwritePrompt = false,
                CheckFileExists = false
            };

            if (dialog.ShowDialog(this) != true)
            {
                return null;
            }

            var selectedPath = EnsureExtension(dialog.FileName, extension);
            if (!File.Exists(selectedPath))
            {
                return selectedPath;
            }

            System.Windows.MessageBox.Show(this,
                "That filename already exists. Please choose a different one.",
                "File already exists",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);

            currentDirectory = Path.GetDirectoryName(selectedPath) ?? currentDirectory;
            currentFileName = Path.GetFileName(selectedPath);
        }
    }

    private static string GetExtension(CaptureType captureType) =>
        captureType switch
        {
            CaptureType.Screenshot => "png",
            CaptureType.Video => "mp4",
            CaptureType.Gif => "gif",
            _ => throw new ArgumentOutOfRangeException(nameof(captureType), captureType, "Unsupported capture type.")
        };

    private static string GetFilter(CaptureType captureType) =>
        captureType switch
        {
            CaptureType.Screenshot => "PNG Image (*.png)|*.png",
            CaptureType.Video => "MP4 Video (*.mp4)|*.mp4",
            CaptureType.Gif => "GIF Animation (*.gif)|*.gif",
            _ => throw new ArgumentOutOfRangeException(nameof(captureType), captureType, "Unsupported capture type.")
        };

    private static string EnsureExtension(string path, string extension)
    {
        var normalizedExtension = extension.StartsWith(".") ? extension : $".{extension}";
        return string.Equals(Path.GetExtension(path), normalizedExtension, StringComparison.OrdinalIgnoreCase)
            ? path
            : Path.ChangeExtension(path, normalizedExtension);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
