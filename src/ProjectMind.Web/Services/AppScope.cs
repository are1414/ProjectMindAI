namespace ProjectMind.Web.Services;

/// <summary>
/// Blazor Server'da bir bileşenin ömrü boyunca tek DbContext kullanmak eski veri ve eşzamanlılık hatası üretir.
/// Her işlem kendi DI kapsamında (dolayısıyla kendi DbContext'iyle) çalıştırılır.
/// </summary>
public sealed class AppScope(IServiceScopeFactory scopeFactory)
{
    public async Task<T> RunAsync<TService, T>(Func<TService, Task<T>> operation) where TService : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await operation(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task RunAsync<TService>(Func<TService, Task> operation) where TService : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await operation(scope.ServiceProvider.GetRequiredService<TService>());
    }
}
