using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class RubricVisibilityDefault_Tests
{
	[Fact]
	public void Omitted_Visibility_Should_Default_To_Institution()
	{
		var input = JsonSerializer.Deserialize<CreateUpdateRubricDto>(
			"""{"Title":"Writing rubric","IsTemplate":true}""");

		input.ShouldNotBeNull();
		input.Visibility.ShouldBe(RubricVisibility.Institution);
	}

	[Theory]
	[InlineData(RubricVisibility.Private)]
	[InlineData(RubricVisibility.SelectedTeachers)]
	[InlineData(RubricVisibility.Department)]
	[InlineData(RubricVisibility.Institution)]
	public void Explicit_Visibility_Should_Be_Preserved(RubricVisibility visibility)
	{
		var input = JsonSerializer.Deserialize<CreateUpdateRubricDto>(
			$"{{\"Visibility\":{(int)visibility}}}");

		input.ShouldNotBeNull();
		input.Visibility.ShouldBe(visibility);
	}
}
