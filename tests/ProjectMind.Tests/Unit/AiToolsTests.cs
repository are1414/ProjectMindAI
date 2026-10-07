using System.Text.Json;
using ProjectMind.Application.Ai;

namespace ProjectMind.Tests.Unit;

public class AiToolsTests
{
    [Fact]
    public void Every_tool_has_object_schema_and_required_fields_exist_in_properties()
    {
        foreach (var tool in AiTools.All)
        {
            Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());
            var properties = tool.InputSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToHashSet();
            foreach (var required in tool.InputSchema.GetProperty("required").EnumerateArray())
                Assert.Contains(required.GetString()!, properties);
        }
    }

    [Fact]
    public void Tool_names_are_unique_and_have_an_apply_order() =>
        Assert.All(AiTools.All, t => Assert.NotEqual(99, AiTools.ApplyOrder(t.Name)));

    [Fact]
    public void Skill_enum_in_schema_excludes_none()
    {
        var addPerson = AiTools.All.Single(t => t.Name == AiTools.AddPerson);
        var values = addPerson.InputSchema.GetProperty("properties").GetProperty("skills")
            .GetProperty("items").GetProperty("enum").EnumerateArray().Select(v => v.GetString());
        Assert.DoesNotContain("None", values);
        Assert.Contains("Backend", values);
    }
}
