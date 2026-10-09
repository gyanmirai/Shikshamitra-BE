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
			.Code.ShouldBe(AdmissionWorkflowErrorCodes.InvalidTransition);
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

	private Task<AdmissionWorkflowDto> CreateDirectPublicationDraftAsync() => _workflowAppService.CreateAsync(new()
	{
		Name = "Direct publication", Code = $"DIRECT_{Guid.NewGuid():N}"[..24],
		Stages = [new() { Code = "DECISION", Name = "Admission decision", StageType = AdmissionWorkflowStageType.Approval,
			IsEntryStage = true, IsTerminalStage = true, IsRequired = true }],
		Transitions = []
	});
}
