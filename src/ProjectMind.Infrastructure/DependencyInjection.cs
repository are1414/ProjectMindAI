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
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddHttpClient<GeminiChatModel>(c => c.Timeout = TimeSpan.FromMinutes(2));
        services.AddScoped<ClaudeChatModel>();
        services.AddScoped<NotConfiguredChatModel>();
        services.AddScoped<IChatModel>(sp =>
        {
            var ai = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            bool Is(string provider) => string.Equals(ai.Provider, provider, StringComparison.OrdinalIgnoreCase);

            if (Is(AiOptions.GeminiProvider) && !string.IsNullOrWhiteSpace(ai.Gemini.ApiKey))
                return sp.GetRequiredService<GeminiChatModel>();
            if (Is(AiOptions.ClaudeProvider) && !string.IsNullOrWhiteSpace(ai.Claude.ApiKey))
                return sp.GetRequiredService<ClaudeChatModel>();
            return sp.GetRequiredService<NotConfiguredChatModel>();
        });
        return services;
    }
}
