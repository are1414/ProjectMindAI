using System.Numerics;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Common;

public static class SkillRules
{
    private static readonly int AllSkillsMask = Enum.GetValues<Skill>().Aggregate(0, (mask, s) => mask | (int)s);

    public static bool IsValidSet(Skill skills) => skills != Skill.None && ((int)skills & ~AllSkillsMask) == 0;

    public static bool IsSingle(Skill skill) => IsValidSet(skill) && BitOperations.PopCount((uint)skill) == 1;
}
