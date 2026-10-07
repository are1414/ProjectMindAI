using System.Net;

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
}
