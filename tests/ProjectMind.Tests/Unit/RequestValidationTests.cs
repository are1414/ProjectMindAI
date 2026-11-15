using ProjectMind.Application.Common;
using ProjectMind.Application.People;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WorkItems;

namespace ProjectMind.Tests.Unit;

/// <summary>Elle yönetim formlarının doğrulaması (D33): DTO kuralları Türkçe mesaj verir; gizli alanlar atlanabilir.</summary>
public class RequestValidationTests
{
    [Fact]
    public void Work_item_rules_give_turkish_messages()
    {
        var errors = RequestValidation.Errors(new WorkItemRequest { Name = "", EstimatedHours = 0, PercentComplete = 120 });

        Assert.Equal(3, errors.Count);
        Assert.Contains("İş adı zorunludur.", errors);
        Assert.Contains("Tahmini efor 0,5 ile 10.000 saat arasında olmalı.", errors);
        Assert.Contains("Tamamlanma 0 ile 100 arasında olmalı.", errors);
    }

    [Fact]
    public void Hidden_fields_of_a_parent_item_are_skipped()
    {
        var parent = new WorkItemRequest { Name = "Backend", EstimatedHours = 0 };

        Assert.NotEmpty(RequestValidation.Errors(parent));
        Assert.Empty(RequestValidation.Errors(parent, nameof(WorkItemRequest.EstimatedHours)));
    }

    [Fact]
    public void Project_currency_and_person_capacity_are_checked()
    {
        var project = new ProjectRequest { Name = "Proje", Currency = "TL" };
        Assert.Equal(["Para birimi 3 harfli kod olmalı (ör. TRY)."], RequestValidation.Errors(project));
        project.Currency = "TRY";
        Assert.Empty(RequestValidation.Errors(project));

        Assert.Equal(["Haftalık kapasite 0 ile 168 saat arasında olmalı."],
            RequestValidation.Errors(new PersonRequest { Name = "Ali", WeeklyCapacityHours = 200 }));
    }
}
