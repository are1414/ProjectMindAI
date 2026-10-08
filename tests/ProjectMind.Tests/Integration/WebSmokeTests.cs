using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectMind.Tests.Integration;

public class WebSmokeTests(WebFactory factory) : IClassFixture<WebFactory>
{
    [Fact]
    public async Task Home_page_serves_blazor_app()
    {
        var response = await factory.CreateClient().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("blazor.web.js", html);
        Assert.Contains("<title>ProjectMind AI</title>", html);
    }

    [Fact]
    public async Task Comment_service_resolves_and_falls_back_to_mock_without_api_key()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<ProjectMind.Application.Ai.IChatModel>();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProjectMind.Application.Ai.ProjectCommentService>());
        if (model is ProjectMind.Infrastructure.Ai.NotConfiguredChatModel)
            Assert.False(model.IsConfigured);   // anahtar yoksa yorum kartı yalnız uyarı gösterir
    }

    [Fact]
    public async Task Hybrid_missing_work_services_resolve_with_configured_size_hours()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProjectMind.Application.Chat.ChatService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProjectMind.Application.MissingWork.MissingWorkService>());
        var options = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<ProjectMind.Application.MissingWork.MissingWorkOptions>>().Value;
        Assert.Equal(80m, options.HoursFor(ProjectMind.Application.MissingWork.WorkSize.L));   // appsettings.json "MissingWork:SizeHours"
    }
}
