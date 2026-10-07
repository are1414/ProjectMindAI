using ProjectMind.Application.Common;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Unit;

public class SkillRulesTests
{
    [Fact]
    public void None_is_not_valid() => Assert.False(SkillRules.IsValidSet(Skill.None));

    [Fact]
    public void Combination_is_valid_set_but_not_single()
    {
        var skills = Skill.Backend | Skill.Database;
        Assert.True(SkillRules.IsValidSet(skills));
        Assert.False(SkillRules.IsSingle(skills));
    }

    [Fact]
    public void Single_skill_is_single() => Assert.True(SkillRules.IsSingle(Skill.Test));

    [Fact]
    public void Undefined_bits_are_rejected() => Assert.False(SkillRules.IsValidSet((Skill)4096));
}
