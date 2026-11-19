using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

[Collection(RubricScoringApiTestCollection.Name)]
public class RubricReadPerformanceApi_Tests(RubricScoringApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Fact]
	public async Task Template_Page_Should_Return_Summary_Counts_Without_Criteria_And_Retain_Later_Matches()
	{
		var mediumId = Guid.NewGuid();
		var prefix = $"Paged {Guid.NewGuid():N}";
		var ids = new List<Guid>();
		for (var index = 0; index < 7; index++)
		{
			var createdResponse = await Client.PostAsJsonAsync("/api/academic/rubrics", new CreateUpdateRubricDto
			{
				AcademicMediumId = mediumId, IsTemplate = true, AppliesToWorkType = WorkType.Both, Title = $"{prefix} {index:00}",
				Description = "Full description must be fetched only on opening this template.",
				Criteria = [new() { Title = "Accuracy", Points = 10, TeacherGuidance = new string('g', 900) }]
			}, JsonOptions);
			createdResponse.IsSuccessStatusCode.ShouldBeTrue(await createdResponse.Content.ReadAsStringAsync());
			var created = (await createdResponse.Content.ReadFromJsonAsync<AssessmentRubricDto>(JsonOptions))!;
			ids.Add(created.Id);
			if (index % 2 == 0) (await Client.PostAsync($"/api/academic/rubrics/{created.Id}/publish", null)).EnsureSuccessStatusCode();
		}

		var response = await Client.GetAsync($"/api/academic/rubrics/templates/paged?academicMediumId={mediumId}&page=2&pageSize=2");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadAsStringAsync();
		using var json = JsonDocument.Parse(body);
		var page = json.RootElement;
		page.GetProperty("totalCount").GetInt32().ShouldBe(7);
		page.GetProperty("summary").GetProperty("published").GetInt32().ShouldBe(4);
		page.GetProperty("summary").GetProperty("draft").GetInt32().ShouldBe(3);
		var items = page.GetProperty("items").EnumerateArray().ToArray();
		items.Length.ShouldBe(2);
		items.Select(item => item.GetProperty("id").GetGuid()).ShouldBe(ids.Skip(2).Take(2));
		items.ShouldAllBe(item => item.GetProperty("criteriaCount").GetInt32() == 1);
		items.Select(item => !item.TryGetProperty("criteria", out _)).ShouldAllBe(hasNoCriteria => hasNoCriteria);
		body.ShouldNotContain("teacherGuidance");
		body.ShouldNotContain("evaluationInstructions");

		var matched = await Client.GetAsync($"/api/academic/rubrics/templates/paged?academicMediumId={mediumId}&search={Uri.EscapeDataString(prefix + " 06")}&page=1&pageSize=2");
		matched.EnsureSuccessStatusCode();
		using var match = JsonDocument.Parse(await matched.Content.ReadAsStringAsync());
		match.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(1);
		match.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(ids[6]);
		var detail = await GetAndReadAsync<AssessmentRubricDto>($"/api/academic/rubrics/{ids[6]}");
		detail.Criteria.Single().TeacherGuidance!.Length.ShouldBe(900);
	}

	[Fact]
	public async Task Submission_Evaluation_Should_Return_Only_The_Selected_Student_And_Reject_Mismatched_Student()
	{
		var (rubricId, submissionId, studentId, evaluationId) = await SeedHomeworkEvaluationsAsync();
		var url = $"/api/academic/rubrics/{rubricId}/evaluations/submission/{submissionId}";
		var response = await Client.GetAsync($"{url}?studentId={studentId}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var evaluation = await response.Content.ReadFromJsonAsync<RubricEvaluationDto>(JsonOptions);
		evaluation.ShouldNotBeNull();
		evaluation.Id.ShouldBe(evaluationId);
		evaluation.StudentId.ShouldBe(studentId);
		evaluation.Scores.Count.ShouldBe(1);
		var mismatch = await Client.GetAsync($"{url}?studentId={Guid.NewGuid()}");
		mismatch.IsSuccessStatusCode.ShouldBeFalse();
		(await mismatch.Content.ReadAsStringAsync()).ShouldContain("SubmissionStudentMismatch");
		var missing = await Client.GetAsync($"/api/academic/rubrics/{rubricId}/evaluations/submission/{Guid.NewGuid()}?studentId={studentId}");
		missing.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	private async Task<(Guid RubricId, Guid SubmissionId, Guid StudentId, Guid EvaluationId)> SeedHomeworkEvaluationsAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var type = await scope.ServiceProvider.GetRequiredService<HomeworkTypeManager>().CreateAsync(Factory.SeededMediumId, $"Read {Guid.NewGuid():N}"[..30]);
		var homework = new HomeworkAssignmentEntity(Guid.NewGuid(), Factory.SeededStandardId, Guid.NewGuid(), "Evaluation page", "Test", type.Id, SubmissionType.OnlineText, tenantId, educationMediumId: Factory.SeededMediumId);
		await scope.ServiceProvider.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().InsertAsync(homework, autoSave: true);
		var rubric = new AssessmentRubric(Guid.NewGuid(), tenantId, homework.StandardId, homework.SubjectId, homework.Id, null, "Evaluation rubric", null);
		var criterion = new AssessmentRubricCriterion(Guid.NewGuid(), tenantId, rubric.Id, "Accuracy", null, null, null, true, 10, 1);
		rubric.ReplaceCriteria([criterion]);
		await scope.ServiceProvider.GetRequiredService<IRepository<AssessmentRubric, Guid>>().InsertAsync(rubric, autoSave: true);
		var submissionRepository = scope.ServiceProvider.GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>();
		var evaluationRepository = scope.ServiceProvider.GetRequiredService<IRepository<RubricEvaluation, Guid>>();
		(Guid RubricId, Guid SubmissionId, Guid StudentId, Guid EvaluationId) result = default;
		for (var index = 0; index < 35; index++)
		{
			var studentId = Guid.NewGuid();
			var submission = new StudentSubmissionEntity(Guid.NewGuid(), homework.Id, studentId, Factory.SeededDivisionId, DateTime.UtcNow, tenantId) { SubmittedContent = new string('b', 12000) };
			await submissionRepository.InsertAsync(submission, autoSave: true);
			var evaluation = new RubricEvaluation(Guid.NewGuid(), tenantId, rubric.Id, submission.Id, studentId, Guid.Parse(TestAuthHandler.TestUserId), $"Student {index}");
			evaluation.ReplaceScores([new RubricCriterionScore(Guid.NewGuid(), tenantId, evaluation.Id, criterion.Id, index % 10, null)]);
			await evaluationRepository.InsertAsync(evaluation, autoSave: true);
			if (index == 31) result = (rubric.Id, submission.Id, studentId, evaluation.Id);
		}
		await uow.CompleteAsync();
		return result;
	}
}
