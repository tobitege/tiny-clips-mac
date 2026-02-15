using System.ComponentModel;
using System.Diagnostics;
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

            using var bitmap = ScreenCaptureService.CaptureRegion(region.Value);
            var output = ScreenCaptureService.SaveBitmap(bitmap, Settings.SaveDirectory);

            if (Settings.CopyToClipboard)
            {
                Clipboard.SetImage(ScreenCaptureService.ToBitmapSource(bitmap));
            }

            PostSave(output);
            StatusMessage = $"Screenshot saved: {output}";
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
            _activeRecording = null;

            StatusMessage = "Finalizing recording...";
            var output = await recording.StopAndSaveAsync();
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
