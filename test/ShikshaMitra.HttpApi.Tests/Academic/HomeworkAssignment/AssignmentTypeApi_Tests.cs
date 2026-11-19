using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Academic)]
public class AssignmentTypeApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private string BaseUrl => $"/api/assignment-type?academicMediumId={Factory.SeededMediumId}";

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Marks_Type_Should_Persist_Null_Default_Marks_And_Allow_Clearing_Them()
	{
		var input = CreateInput();
		var created = await PostAndReadAsync<AssignmentTypeCreateResponseDto>(BaseUrl, input);
		created.Data.DefaultMarks.ShouldBeNull();
		created.Data.DefaultEvaluationMethod.ShouldBe(EvaluationMethod.Marks);
		created.Data.EvaluationRequired.ShouldBeTrue();

		var recordUrl = $"/api/assignment-type/{created.Data.Id}?academicMediumId={Factory.SeededMediumId}";
		var persisted = await GetAndReadAsync<AssignmentTypeGetResponseDto>(recordUrl);
		persisted.Data.DefaultMarks.ShouldBeNull();
		persisted.Data.DefaultSubmissionType.ShouldBe(SubmissionType.InPersonSubmission);
		persisted.Data.DefaultDueDays.ShouldBe(1);

		var update = new UpdateAssignmentTypeDto
		{
			AssignmentTypeName = input.AssignmentTypeName,
			DefaultSubmissionType = input.DefaultSubmissionType,
			DefaultDueDays = input.DefaultDueDays,
			EvaluationRequired = true,
			DefaultEvaluationMethod = EvaluationMethod.Marks,
			DefaultMarks = 25,
		};
		var setMarks = await Client.PutAsJsonAsync(recordUrl, update, JsonOptions);
		setMarks.EnsureSuccessStatusCode();
		persisted = await GetAndReadAsync<AssignmentTypeGetResponseDto>(recordUrl);
		persisted.Data.DefaultMarks.ShouldBe(25m);

		update.DefaultMarks = null;
		var clearMarks = await Client.PutAsJsonAsync(recordUrl, update, JsonOptions);
		clearMarks.EnsureSuccessStatusCode();
		persisted = await GetAndReadAsync<AssignmentTypeGetResponseDto>(recordUrl);
		persisted.Data.DefaultMarks.ShouldBeNull();
		persisted.Data.DefaultEvaluationMethod.ShouldBe(EvaluationMethod.Marks);
		persisted.Data.EvaluationRequired.ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Marks_Type_Should_Still_Reject_Negative_Default_Marks()
	{
		var input = CreateInput();
		input.DefaultMarks = -1;
		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private static CreateAssignmentTypeDto CreateInput() => new()
	{
		AssignmentTypeName = $"Optional marks {Guid.NewGuid():N}",
		DefaultSubmissionType = SubmissionType.InPersonSubmission,
		DefaultMarks = null,
		DefaultDueDays = 1,
		EvaluationRequired = true,
		DefaultEvaluationMethod = EvaluationMethod.Marks,
		Status = TaskTypeStatus.Active,
	};
}
