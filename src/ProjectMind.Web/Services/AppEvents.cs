namespace ProjectMind.Web.Services;

/// <summary>Devre (circuit) içi basit olay: proje/sohbet listesi değişince kenar çubuğu yenilenir.</summary>
public sealed class AppEvents
{
    public event Func<Task>? SessionsChanged;

    public Task NotifySessionsChangedAsync() => SessionsChanged?.Invoke() ?? Task.CompletedTask;
}
