using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Ai;
using ProjectMind.Infrastructure.Ai;
using ProjectMind.Infrastructure.Persistence;

namespace ProjectMind.Infrastructure;

public static class DependencyInjection
{
    private const string ClaudeProvider = "Claude";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<ClaudeChatModel>();
        services.AddScoped<NotConfiguredChatModel>();
        services.AddScoped<IChatModel>(sp =>
        {
            var ai = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            var useClaude = string.Equals(ai.Provider, ClaudeProvider, StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(ai.ApiKey);
            return useClaude
                ? sp.GetRequiredService<ClaudeChatModel>()
                : sp.GetRequiredService<NotConfiguredChatModel>();
        });
        return services;
    }
}
