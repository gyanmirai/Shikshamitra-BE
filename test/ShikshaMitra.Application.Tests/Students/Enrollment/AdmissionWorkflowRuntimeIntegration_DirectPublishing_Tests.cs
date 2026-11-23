using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Direct_publication_ignores_retired_maker_checker_but_preserves_validation_concurrency_and_audit()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var settingRepository = GetRequiredService<IRepository<AdmissionWorkflowGovernanceSetting, Guid>>();
		var settings = new AdmissionWorkflowGovernanceSetting(Guid.NewGuid(), TestTenantId);
		settings.Update(true, true, true);
		await settingRepository.InsertAsync(settings, autoSave: true);
		var draft = await CreateDirectPublicationDraftAsync();
		(await Should.ThrowAsync<BusinessException>(() =>
			_workflowAppService.PublishAsync(draft.Id, "stale-stamp")))
			.Code.ShouldBe(AdmissionWorkflowErrorCodes.ConcurrencyConflict);
		(await _workflowAppService.GetAsync(draft.Id)).Status.ShouldBe(AdmissionWorkflowStatus.Draft);
		var published = await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
		published.Status.ShouldBe(AdmissionWorkflowStatus.Published);
		published.ScheduledPublishAt.ShouldBeNull();
		(await _workflowAppService.GetGovernanceHistoryAsync(draft.Id))
			.ShouldContain(item => item.Action == AdmissionWorkflowGovernanceAction.Published);
		(await _workflowAppService.GetNotificationsAsync()).ShouldBeEmpty();
		(await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp)).Id.ShouldBe(draft.Id);

		var invalid = await _workflowAppService.CreateAsync(new()
		{
			Name = "Invalid publication", Code = $"INVALID_{Guid.NewGuid():N}"[..24],
			Stages = [], Transitions = []
		});
		(await Should.ThrowAsync<BusinessException>(() =>
			_workflowAppService.PublishAsync(invalid.Id, invalid.ConcurrencyStamp)))
			.Code.ShouldBe(AdmissionWorkflowErrorCodes.InvalidFinalStage);
		(await _workflowAppService.GetAsync(invalid.Id)).Status.ShouldBe(AdmissionWorkflowStatus.Draft);
	}

	[Fact]
	public async Task Compatibility_publication_is_immediate_and_historical_schedule_tick_cannot_publish()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var draft = await CreateDirectPublicationDraftAsync();
		var published = await _workflowAppService.PublishWithGovernanceAsync(draft.Id, new()
		{
			ConcurrencyStamp = draft.ConcurrencyStamp, ChangeReason = "Legacy client",
			EffectiveAt = DateTime.UtcNow.AddDays(10)
		});
		published.Status.ShouldBe(AdmissionWorkflowStatus.Published);
		published.ScheduledPublishAt.ShouldBeNull();

		var historical = await CreateDirectPublicationDraftAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>();
			var definition = await repository.GetAsync(historical.Id);
			definition.MarkValidated(TestUserId, DateTime.UtcNow);
			definition.Schedule(DateTime.UtcNow.AddDays(-1));
			await repository.UpdateAsync(definition, autoSave: true);
		});
		await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionWorkflowCommandService>().PublishScheduledAsync(historical.Id));
		var unchanged = await _workflowAppService.GetAsync(historical.Id);
		unchanged.Status.ShouldBe(AdmissionWorkflowStatus.Scheduled);
		unchanged.ScheduledPublishAt.ShouldNotBeNull();
		(await _workflowAppService.PublishAsync(historical.Id, unchanged.ConcurrencyStamp)).Status
			.ShouldBe(AdmissionWorkflowStatus.Published);
	}

	[Fact]
	public async Task Workflow_save_requires_an_ending_decision_but_can_bootstrap_an_empty_draft()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var invalid = new CreateUpdateAdmissionWorkflowDto
		{
			Name = "Missing ending decision",
			Stages = [new() { Code = "ASSESSMENT", Name = "Assessment", StageType = AdmissionWorkflowStageType.Assessment,
				IsEntryStage = true, IsTerminalStage = true }]
		};
		(await Should.ThrowAsync<BusinessException>(() => _workflowAppService.CreateAsync(invalid)))
			.Code.ShouldBe(AdmissionWorkflowErrorCodes.InvalidFinalStage);

		var blank = await _workflowAppService.CreateAsync(new() { Name = invalid.Name });
		blank.Stages.ShouldBeEmpty();
		(await Should.ThrowAsync<BusinessException>(() => _workflowAppService.UpdateAsync(blank.Id, ToUpdateInput(blank))))
			.Code.ShouldBe(AdmissionWorkflowErrorCodes.InvalidFinalStage);
		(await _workflowAppService.ValidatePreviewAsync(blank.Id, new() { Stages = invalid.Stages })).Issues
			.ShouldContain(issue => issue.Code == AdmissionWorkflowErrorCodes.InvalidFinalStage);
		(await _workflowAppService.GetAsync(blank.Id)).Stages.ShouldBeEmpty();

		var completed = ToUpdateInput(blank);
		completed.Stages.Add(new() { Code = "DECISION", Name = "Admission decision",
			StageType = AdmissionWorkflowStageType.Approval, IsEntryStage = true, IsTerminalStage = true });
		var draft = await _workflowAppService.UpdateAsync(blank.Id, completed);
		(await _workflowAppService.ValidateAsync(draft.Id)).IsValid.ShouldBeTrue();
		draft = await _workflowAppService.GetAsync(draft.Id);
		var input = ToUpdateInput(draft);
		input.Stages[0].IsTerminalStage = false;
		(await Should.ThrowAsync<BusinessException>(() => _workflowAppService.UpdateAsync(draft.Id, input)))
			.Code.ShouldBe(AdmissionWorkflowErrorCodes.InvalidFinalStage);
		input.Stages[0].IsTerminalStage = true;
		var finalId = input.Stages[0].Id!.Value;
		input.Transitions.Add(new() { FromStageId = finalId, ToStageId = finalId, IsDefault = true });
		(await Should.ThrowAsync<BusinessException>(() => _workflowAppService.UpdateAsync(draft.Id, input)))
			.Code.ShouldBe(AdmissionWorkflowErrorCodes.InvalidFinalStage);
		var unchanged = await _workflowAppService.GetAsync(draft.Id);
		unchanged.Status.ShouldBe(AdmissionWorkflowStatus.Validated);
		unchanged.Stages.ShouldHaveSingleItem().IsTerminalStage.ShouldBeTrue();
	}

	[Fact]
	public async Task Validation_and_publication_refuse_a_saved_legacy_draft_ending_with_assessment()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var draft = await _workflowAppService.CreateAsync(new() { Name = "Legacy assessment draft" });
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>()
			.InsertAsync(new AdmissionWorkflowStageDefinition(Guid.NewGuid(), draft.Id, "ASSESSMENT", "Assessment",
				AdmissionWorkflowStageType.Assessment, 0, true, false, null, null, true, true), autoSave: true));

		(await _workflowAppService.ValidateAsync(draft.Id)).Issues.ShouldContain(issue =>
			issue.Code == AdmissionWorkflowErrorCodes.InvalidFinalStage);
		(await Should.ThrowAsync<BusinessException>(() => _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp)))
			.Code.ShouldBe(AdmissionWorkflowErrorCodes.InvalidFinalStage);
		(await _workflowAppService.GetAsync(draft.Id)).Status.ShouldBe(AdmissionWorkflowStatus.Draft);
	}

	private Task<AdmissionWorkflowDto> CreateDirectPublicationDraftAsync(string? name = null) => _workflowAppService.CreateAsync(new()
	{
		Name = name ?? $"Direct publication {Guid.NewGuid():N}",
		Stages = [new() { Code = "DECISION", Name = "Admission decision", StageType = AdmissionWorkflowStageType.Approval,
			IsEntryStage = true, IsTerminalStage = true, IsRequired = true }],
		Transitions = []
	});
}
