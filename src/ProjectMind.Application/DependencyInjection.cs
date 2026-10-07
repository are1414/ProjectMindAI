using Microsoft.Extensions.DependencyInjection;
using ProjectMind.Application.Dependencies;
using ProjectMind.Application.People;
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
        return services;
    }
}
