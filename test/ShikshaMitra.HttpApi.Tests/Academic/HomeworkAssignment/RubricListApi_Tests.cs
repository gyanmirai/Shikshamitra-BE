using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

[Collection(RubricScoringApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Academic)]
public class RubricListApi_Tests(RubricScoringApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData("")]
	[InlineData("&isTemplate=true")]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task List_With_Medium_And_Omitted_Optional_Filters_Should_Return_Template_With_Criteria(string templateFilter)
	{
		var mediumId = Guid.NewGuid();
		var response = await Client.PostAsJsonAsync("/api/academic/rubrics", new CreateUpdateRubricDto
		{
			AcademicMediumId = mediumId,
			IsTemplate = true,
			AppliesToWorkType = WorkType.Both,
			Title = $"List regression {Guid.NewGuid():N}",
			Criteria = [new() { Title = "Accuracy", Points = 10, IsRequired = true, DisplayOrder = 1 }]
		}, JsonOptions);
		response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
		var created = (await response.Content.ReadFromJsonAsync<AssessmentRubricDto>(JsonOptions))!;
		var list = await GetAndReadAsync<List<AssessmentRubricDto>>($"/api/academic/rubrics?academicMediumId={mediumId}{templateFilter}");
		var template = list.Single(rubric => rubric.Id == created.Id);
		template.Criteria.Count.ShouldBe(1);
		template.Criteria[0].Points.ShouldBe(10);
		template.TotalMarks.ShouldBe(10);
		var otherMedium = await GetAndReadAsync<List<AssessmentRubricDto>>($"/api/academic/rubrics?academicMediumId={Guid.NewGuid()}{templateFilter}");
		otherMedium.ShouldNotContain(rubric => rubric.Id == created.Id);
	}
}
