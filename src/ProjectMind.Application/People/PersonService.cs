using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Common;
using ProjectMind.Domain.Entities;

namespace ProjectMind.Application.People;

public sealed class PersonService(IAppDbContext db)
{
    public async Task<IReadOnlyList<PersonResponse>> ListAsync(int projectId, CancellationToken ct)
    {
        await EnsureProjectExistsAsync(projectId, ct);
        var people = await db.People.AsNoTracking()
            .Where(p => p.ProjectId == projectId).OrderBy(p => p.Id).ToListAsync(ct);
        return people.Select(PersonResponse.From).ToList();
    }

    public async Task<PersonResponse> GetAsync(int projectId, int id, CancellationToken ct) =>
        PersonResponse.From(await FindAsync(projectId, id, ct));

    public async Task<PersonResponse> CreateAsync(int projectId, PersonRequest request, CancellationToken ct)
    {
        await EnsureProjectExistsAsync(projectId, ct);
        Validate(request);
        var person = new Person { ProjectId = projectId, Name = request.Name };
        Apply(person, request);
        db.People.Add(person);
        await db.SaveChangesAsync(ct);
        return PersonResponse.From(person);
    }

    public async Task<PersonResponse> UpdateAsync(int projectId, int id, PersonRequest request, CancellationToken ct)
    {
        Validate(request);
        var person = await FindAsync(projectId, id, ct);
        Apply(person, request);
        await db.SaveChangesAsync(ct);
        return PersonResponse.From(person);
    }

    public async Task DeleteAsync(int projectId, int id, CancellationToken ct)
    {
        var person = await FindAsync(projectId, id, ct);
        // Kişi silinince atandığı işler boşa çıkar (iş silinmez).
        await db.WorkItems.Where(w => w.AssigneeId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.AssigneeId, (int?)null), ct);
        db.People.Remove(person);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Person> FindAsync(int projectId, int id, CancellationToken ct) =>
        await db.People.FirstOrDefaultAsync(p => p.Id == id && p.ProjectId == projectId, ct)
        ?? throw new NotFoundException("Kişi", id);

    private async Task EnsureProjectExistsAsync(int projectId, CancellationToken ct)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct))
            throw new NotFoundException("Proje", projectId);
    }

    private static void Validate(PersonRequest request)
    {
        if (!SkillRules.IsValidSet(request.CombinedSkills))
            throw new BusinessRuleException("Kişinin en az bir geçerli becerisi olmalıdır.");
    }

    private static void Apply(Person person, PersonRequest request)
    {
        person.Name = request.Name.Trim();
        person.Skills = request.CombinedSkills;
        person.WeeklyCapacityHours = request.WeeklyCapacityHours;
        person.HourlyCost = request.HourlyCost;
    }
}
