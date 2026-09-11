using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private async Task<ReviewFixture> CreateReviewFixtureAsync(string? configurationJson = null,
		IReadOnlyList<CreateUpdateAdmissionWorkflowStageDto>? priorStages = null,
		Guid? applicationFormDefinitionId = null, string? negativeRoute = null,
		Dictionary<string, string>? priorExitConditions = null)
	{
		var legacy = await CreateAndPublishWorkflowAsync();
		var draft = await _workflowAppService.CreateNewVersionAsync(legacy.Id);
		var input = ToUpdateInput(draft);
		var entry = input.Stages.Single(item => item.IsEntryStage);
		var terminal = input.Stages.Single(item => item.IsTerminalStage);
		input.Transitions = [];
		var previousId = entry.Id!.Value;
		var order = 1;
		foreach (var stage in priorStages ?? [])
		{
			stage.Id ??= Guid.NewGuid();
			stage.DisplayOrder = order++;
			input.Stages.Add(stage);
			input.Transitions.Add(new() { FromStageId = previousId, ToStageId = stage.Id.Value, IsDefault = true });
			previousId = stage.Id.Value;
		}
		var reviewId = Guid.NewGuid();
		input.Stages.Add(new()
		{
			Id = reviewId, Code = "REVIEW", Name = "Review admission evidence",
			StageType = AdmissionWorkflowStageType.Review, DisplayOrder = order++, IsRequired = true,
			ConfigurationJson = configurationJson ?? ReviewPolicyJson()
		});
		terminal.DisplayOrder = order;
		input.Transitions.Add(new() { FromStageId = previousId, ToStageId = reviewId, IsDefault = true });
		input.Transitions.Add(new() { FromStageId = reviewId, ToStageId = terminal.Id!.Value, IsDefault = true });
		if (negativeRoute != null)
		{
			var outcomeId = Guid.NewGuid();
			input.Stages.Add(new() { Id = outcomeId, Code = "REVIEW_OUTCOME", Name = "Review outcome follow-up",
				StageType = AdmissionWorkflowStageType.Custom, DisplayOrder = order++, IsRequired = true });
			terminal.DisplayOrder = order;
			input.Transitions.Add(new()
			{
				FromStageId = reviewId, ToStageId = outcomeId, Priority = 1,
				ConditionRuleJson = JsonSerializer.Serialize(new { field = "review.decision", @operator = "Equals", value = negativeRoute })
			});
			input.Transitions.Add(new() { FromStageId = outcomeId, ToStageId = terminal.Id.Value, IsDefault = true });
		}
		foreach (var (code, condition) in priorExitConditions ?? [])
		{
			var sourceId = input.Stages.Single(item => item.Code == code).Id!.Value;
			var transition = input.Transitions.Single(item => item.FromStageId == sourceId);
			transition.IsDefault = false;
			transition.ConditionRuleJson = condition;
		}
		draft = await _workflowAppService.UpdateAsync(draft.Id, input);
		var graphValidation = await _workflowAppService.ValidateAsync(draft.Id);
		graphValidation.IsValid.ShouldBeTrue(string.Join(" | ", graphValidation.Issues.Select(item => item.Message)));
		var published = await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
		var offering = await CreateAvailableOfferingAsync(legacy.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await _offeringRepository.GetAsync(offering.Id);
			stored.ConfigureReferences(published.Id, stored.InquiryFormDefinitionId,
				applicationFormDefinitionId ?? stored.ApplicationFormDefinitionId);
			await _offeringRepository.UpdateAsync(stored, autoSave: true);
			var validation = await _offeringValidator.ValidateStoredAsync(stored);
			validation.Errors.ShouldBeEmpty(string.Join(" | ", validation.Errors.Select(item => item.Message)));
		});
		var applicant = await CreateCertificationApplicantAsync("Review", true, true, null);
		var application = await _applicationAppService.StartAsync(applicant.Id, offering.Id);
		(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
		var workflow = await _applicationAppService.GetWorkflowAsync(application.Id);
		return new(applicant.Id, application.Id, workflow.Stages.Single(item => item.Code == "REVIEW").Id,
			offering, published.Id, workflow.Stages.ToDictionary(item => item.Code, item => item.Id));
	}

	private static string ReviewPolicyJson(bool enabled = true, bool independent = false,
		string? reviewerRole = null, string? approverRole = null, Guid? assignedReviewerId = null,
		int? dueInDays = null, object[]? prerequisites = null, object[]? checklist = null) =>
		JsonSerializer.Serialize(new { review = new { version = 1, enabled, reviewerRole, approverRole,
			assignedReviewerId, requireIndependentApprover = independent, dueInDays,
			prerequisites = prerequisites ?? [], checklist = checklist ?? [] } });

	private async Task<Guid> CreateReviewUserAsync(string? role = null, bool grantScope = true, bool active = true)
	{
		var id = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var user = new IdentityUser(id, $"reviewer-{id:N}", $"{id:N}@example.test", TestTenantId);
			user.SetIsActive(active);
			await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(user, autoSave: true);
			if (role != null)
			{
				var roles = GetRequiredService<IRepository<IdentityRole, Guid>>();
				if (!await roles.AnyAsync(item => item.Name == role))
					await roles.InsertAsync(new IdentityRole(Guid.NewGuid(), role, TestTenantId), autoSave: true);
				var result = await GetRequiredService<IdentityUserManager>().AddToRoleAsync(user, role);
				result.Succeeded.ShouldBeTrue(string.Join(" | ", result.Errors.Select(item => item.Description)));
			}
			if (grantScope)
				await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
					userId: id, description: "Managed Review integration scope"), autoSave: true);
		});
		return id;
	}

	private sealed record ReviewFixture(Guid ApplicantId, Guid ApplicationId, Guid StageId,
		AdmissionOffering Offering, Guid WorkflowDefinitionId, Dictionary<string, Guid> StageIds);

	private async Task<AdmissionReviewDto> SubmitReviewAsync(ReviewFixture fixture, string recommendation = "approve")
	{
		var service = GetRequiredService<IAdmissionReviewAppService>();
		var state = await service.GetAsync(fixture.ApplicationId, fixture.StageId);
		return await service.SubmitAsync(fixture.ApplicationId, fixture.StageId, new()
		{
			ConcurrencyStamp = state.ConcurrencyStamp, Recommendation = recommendation,
			Reason = "Recorded after inspecting current application evidence.",
			Checklist = state.Policy!.Checklist.Select(item => new AdmissionReviewChecklistDto
				{ Code = item.Code, Checked = true, Notes = "Checked against the recorded source." }).ToList()
		});
	}
}
