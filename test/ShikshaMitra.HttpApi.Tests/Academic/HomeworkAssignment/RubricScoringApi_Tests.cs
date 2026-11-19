using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

[Collection(RubricScoringApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Academic)]
public class RubricScoringApi_Tests(RubricScoringApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string BaseUrl = "/api/academic/rubrics";

	[Theory]
	[InlineData(false, RubricScoringModel.PointsBased, 18, 25)]
	[InlineData(true, RubricScoringModel.PointsBased, 18, 25)]
	[InlineData(false, RubricScoringModel.WeightedPercentage, 82.5, 100)]
	[InlineData(true, RubricScoringModel.WeightedPercentage, 82.5, 100)]
	[InlineData(false, RubricScoringModel.Checklist, 4, 6)]
	[InlineData(true, RubricScoringModel.Checklist, 4, 6)]
	[InlineData(false, RubricScoringModel.PerformanceLevels, 7, 8)]
	[InlineData(true, RubricScoringModel.PerformanceLevels, 7, 8)]
	[InlineData(false, RubricScoringModel.RatingScale, 7, 10)]
	[InlineData(true, RubricScoringModel.RatingScale, 7, 10)]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Scores_Should_Round_Trip_And_Become_Official_Marks_On_Approval(
		bool assignment, RubricScoringModel model, double expected, double maximum)
	{
		var (rubric, submissionId, studentId) = await CreateTaskRubricAsync(assignment, model);
		var marks = model switch
		{
			RubricScoringModel.PointsBased => new decimal[] { 8, 10 },
			RubricScoringModel.WeightedPercentage => new decimal[] { 40, 42.5m },
			RubricScoringModel.Checklist => new decimal[] { 1, 1, 1, 1, 0, 0 },
			_ => new decimal[] { 4, 3 }
		};
		var input = EvaluationInput(rubric, submissionId, studentId, marks);
		var evaluation = await PostAndReadAsync<RubricEvaluationDto>($"{BaseUrl}/{rubric.Id}/evaluations", input);
		evaluation.TotalMarks.ShouldBe((decimal) expected);
		(await GetAndReadAsync<List<RubricEvaluationDto>>($"{BaseUrl}/{rubric.Id}/evaluations"))
			.Single().TotalMarks.ShouldBe(evaluation.TotalMarks);
		(await Client.PostAsync($"{BaseUrl}/evaluations/{evaluation.Id}/submit", null)).EnsureSuccessStatusCode();
		(await Client.PostAsync($"{BaseUrl}/evaluations/{evaluation.Id}/approve", null)).EnsureSuccessStatusCode();
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var stored = await scope.ServiceProvider.GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>().GetAsync(submissionId);
		stored.Marks.ShouldBe((decimal) expected);
		stored.TotalMarks.ShouldBe((decimal) maximum);
		stored.Status.ShouldBe(SubmissionStatus.Reviewed);
		await uow.CompleteAsync();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Rounding_Should_Apply_To_Each_Contribution_Before_Summing(bool assignment)
	{
		var (rubric, submissionId, studentId) = await CreateTaskRubricAsync(assignment, RubricScoringModel.WeightedPercentage);
		var evaluation = await PostAndReadAsync<RubricEvaluationDto>($"{BaseUrl}/{rubric.Id}/evaluations",
			EvaluationInput(rubric, submissionId, studentId, [1.005m, 1.005m]));
		evaluation.TotalMarks.ShouldBe(2.02m);
		evaluation.Scores.ShouldAllBe(score => score.Marks == 1.01m);
		var reloaded = (await GetAndReadAsync<List<RubricEvaluationDto>>($"{BaseUrl}/{rubric.Id}/evaluations")).Single();
		reloaded.TotalMarks.ShouldBe(2.02m);
		reloaded.Scores.Sum(score => score.Marks).ShouldBe(2.02m);
	}

	[Theory]
	[InlineData(RubricScoringModel.PointsBased, -1)]
	[InlineData(RubricScoringModel.PointsBased, 11)]
	[InlineData(RubricScoringModel.WeightedPercentage, 51)]
	[InlineData(RubricScoringModel.Checklist, 0.5)]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Invalid_Scores_Should_Be_Rejected(RubricScoringModel model, double invalid)
	{
		var (rubric, submissionId, studentId) = await CreateTaskRubricAsync(false, model);
		var input = EvaluationInput(rubric, submissionId, studentId, rubric.Criteria.Select(_ => 0m).ToArray());
		input.Scores[0].Marks = (decimal) invalid;
		var response = await Client.PostAsJsonAsync($"{BaseUrl}/{rubric.Id}/evaluations", input, JsonOptions);
		response.IsSuccessStatusCode.ShouldBeFalse();
		input.Scores.RemoveAt(0);
		response = await Client.PostAsJsonAsync($"{BaseUrl}/{rubric.Id}/evaluations", input, JsonOptions);
		response.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Weighted_Template_Save_Should_Require_Exactly_100_Percent()
	{
		var input = TemplateInput(RubricScoringModel.WeightedPercentage);
		input.Criteria[0].Weight = 40;
		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		response.IsSuccessStatusCode.ShouldBeFalse();
		(await response.Content.ReadAsStringAsync()).ShouldContain("WeightMustEqual100");
	}

	private async Task<(AssessmentRubricDto Rubric, Guid SubmissionId, Guid StudentId)> CreateTaskRubricAsync(bool assignment, RubricScoringModel model)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var taskId = Guid.NewGuid();
		var subjectId = Guid.NewGuid();
		var studentId = Guid.NewGuid();
		var submissionId = Guid.NewGuid();
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			if (assignment)
			{
				var type = await scope.ServiceProvider.GetRequiredService<AssignmentTypeManager>().CreateAsync(
					Factory.SeededMediumId, $"Scoring {Guid.NewGuid():N}"[..30], defaultEvaluationMethod: EvaluationMethod.Rubric);
				var task = new AssignmentEntity(taskId, Factory.SeededStandardId, subjectId, "Scoring task", "Test",
					type.Id, SubmissionType.OnlineText, 0, tenantId, Factory.SeededAcademicYearId,
					educationBoardId: Factory.SeededBoardId, educationMediumId: Factory.SeededMediumId,
					educationMediumStageId: Factory.SeededMediumStageId, evaluationRequired: true, evaluationMethod: EvaluationMethod.Rubric);
				await scope.ServiceProvider.GetRequiredService<IRepository<AssignmentEntity, Guid>>().InsertAsync(task, autoSave: true);
			}
			else
			{
				var type = await scope.ServiceProvider.GetRequiredService<HomeworkTypeManager>().CreateAsync(
					Factory.SeededMediumId, $"Scoring {Guid.NewGuid():N}"[..30]);
				await scope.ServiceProvider.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().InsertAsync(
					new HomeworkAssignmentEntity(taskId, Factory.SeededStandardId, subjectId, "Scoring task", "Test",
						type.Id, SubmissionType.OnlineText, tenantId, educationMediumId: Factory.SeededMediumId), autoSave: true);
			}
			var submission = assignment
				? new StudentSubmissionEntity(submissionId, taskId, studentId, Factory.SeededDivisionId, DateTime.UtcNow, tenantId, true)
				: new StudentSubmissionEntity(submissionId, taskId, studentId, Factory.SeededDivisionId, DateTime.UtcNow, tenantId);
			await scope.ServiceProvider.GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>().InsertAsync(submission, autoSave: true);
			await uow.CompleteAsync();
		}
		var template = await PostAndReadAsync<AssessmentRubricDto>(BaseUrl, TemplateInput(model));
		(await Client.PostAsync($"{BaseUrl}/{template.Id}/publish", null)).EnsureSuccessStatusCode();
		var rubric = await PostAndReadAsync<AssessmentRubricDto>($"{BaseUrl}/{template.Id}/copy-to-task", new CreateRubricFromTemplateDto
		{
			StandardId = Factory.SeededStandardId, SubjectId = subjectId,
			HomeworkId = assignment ? null : taskId, AssignmentId = assignment ? taskId : null
		});
		return (rubric, submissionId, studentId);
	}

	private static UpsertRubricEvaluationDto EvaluationInput(AssessmentRubricDto rubric, Guid submissionId, Guid studentId, decimal[] marks) => new()
	{
		StudentSubmissionId = submissionId, StudentId = studentId,
		Scores = rubric.Criteria.Select((criterion, index) => new RubricEvaluationScoreInputDto
		{
			CriterionId = criterion.Id, Marks = marks[index]
		}).ToList()
	};

	private static CreateUpdateRubricDto TemplateInput(RubricScoringModel model) => new()
	{
		Title = "Scoring template", IsTemplate = true, AppliesToWorkType = WorkType.Both, ScoringModel = model,
		PerformanceLevels = model is RubricScoringModel.PerformanceLevels or RubricScoringModel.RatingScale
			? [new() { Level = 2, Label = "Excellent", Score = 4 }, new() { Level = 1, Label = "Good", Score = 3 }] : [],
		Criteria = Enumerable.Range(0, model == RubricScoringModel.Checklist ? 6 : 2).Select(index => new RubricCriterionInputDto
		{
			Title = $"Criterion {index + 1}", IsRequired = true, DisplayOrder = index + 1,
			Points = model switch { RubricScoringModel.PerformanceLevels or RubricScoringModel.WeightedPercentage => null,
				RubricScoringModel.Checklist => 1, RubricScoringModel.RatingScale => 5, _ => index == 0 ? 10 : 15 },
			Weight = model == RubricScoringModel.WeightedPercentage ? 50 : null
		}).ToList()
	};
}
