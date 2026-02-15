namespace TinyClips.Windows.Services;

public static class CountdownService
{
    public static async Task RunAsync(int seconds, Action<int> onTick, CancellationToken cancellationToken = default)
    {
        for (var remaining = seconds; remaining > 0; remaining--)
        {
            onTick(remaining);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }
}
