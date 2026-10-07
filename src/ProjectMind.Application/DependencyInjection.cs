using Microsoft.Extensions.DependencyInjection;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Chat;
using ProjectMind.Application.Dependencies;
using ProjectMind.Application.MissingWork;
using ProjectMind.Application.Overview;
using ProjectMind.Application.People;
using ProjectMind.Application.Planning;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WorkItems;

namespace ProjectMind.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ProjectService>();
        services.AddScoped<PersonService>();
        services.AddScoped<WorkItemService>();
        services.AddScoped<DependencyService>();
        services.AddScoped<ProjectOverviewService>();
        services.AddScoped<AiActionService>();
        services.AddScoped<ProjectContextBuilder>();
        services.AddScoped<ChatService>();
        services.AddScoped<MissingWorkService>();
        services.AddScoped<ScheduleService>();
        services.AddScoped<ReadOnlyToolHandler>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
