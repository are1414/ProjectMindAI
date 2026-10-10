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

    [Fact]
    public async Task Demo_seeder_resolves_from_di()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<ProjectMind.Application.Demo.DemoProjectSeeder>();
        Assert.Null(await seeder.FindExistingAsync(CancellationToken.None));
        var options = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<ProjectMind.Application.Demo.DemoOptions>>().Value;
        Assert.Equal(9, options.WeeksBack);
    }

    [Fact]
    public async Task Evaluation_services_resolve_and_pages_are_served()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var evaluation = scope.ServiceProvider.GetRequiredService<ProjectMind.Application.Evaluation.EvaluationService>();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProjectMind.Application.Evaluation.SurveyService>());
        var summary = await evaluation.GetSummaryAsync(CancellationToken.None);
        Assert.Equal(0, summary.Survey.Sus.N);

        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/evaluation")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/evaluation/survey")).StatusCode);
    }
}
