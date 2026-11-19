using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(AdmissionWorkflowStageType.Eligibility)]
	[InlineData(AdmissionWorkflowStageType.Review)]
	[InlineData(AdmissionWorkflowStageType.Offer)]
	public Task Retired_stage_cannot_be_authored_and_does_not_create_a_definition(AdmissionWorkflowStageType type) => RunCertificationScenarioAsync(async () =>
	{
		var repository = GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>();
		var before = await WithUnitOfWorkAsync(() => repository.CountAsync());
		var metadata = AdmissionWorkflowStageTypeRegistry.Find(type)!;
		metadata.IsSelectable.ShouldBeFalse(); metadata.IsRuntimeReady.ShouldBeFalse(); metadata.IsDeprecated.ShouldBeTrue();
		var error = await Should.ThrowAsync<BusinessException>(() => _workflowAppService.CreateAsync(new()
		{
			Name = "Retired stage rejection", Code = $"RETIRED_{Guid.NewGuid():N}"[..24],
			Stages = [new() { Id = Guid.NewGuid(), Code = "RETIRED", Name = "Retired stage", StageType = type, IsEntryStage = true, IsTerminalStage = true }]
		}));
		error.Code.ShouldBe(AdmissionWorkflowErrorCodes.RuntimeHandlerUnavailable);
		(await WithUnitOfWorkAsync(() => repository.CountAsync())).ShouldBe(before);
	});

	[Theory]
	[InlineData(AdmissionWorkflowStageType.Eligibility)]
	[InlineData(AdmissionWorkflowStageType.Review)]
	[InlineData(AdmissionWorkflowStageType.Offer)]
	public Task Retired_stage_update_does_not_change_the_existing_draft(AdmissionWorkflowStageType type) => RunCertificationScenarioAsync(async () =>
	{
		var draft = await CreateDirectWorkflowDraftAsync();
		var input = ToUpdateInput(draft);
		input.Name = "Rejected change";
		input.Stages[0].StageType = type;
		(await Should.ThrowAsync<BusinessException>(() => _workflowAppService.UpdateAsync(draft.Id, input)))
			.Code.ShouldBe(AdmissionWorkflowErrorCodes.RuntimeHandlerUnavailable);
		var unchanged = await _workflowAppService.GetAsync(draft.Id);
		unchanged.Name.ShouldBe(draft.Name);
		unchanged.Status.ShouldBe(draft.Status);
		unchanged.ConcurrencyStamp.ShouldBe(draft.ConcurrencyStamp);
		unchanged.Stages[0].StageType.ShouldBe(draft.Stages[0].StageType);
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Unsupported_historical_graph_cannot_leave_an_orphan_clone_or_version(bool version) => RunCertificationScenarioAsync(async () =>
	{
		var historical = await CreateAndPublishWorkflowAsync();
		var repository = GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>();
		var before = await WithUnitOfWorkAsync(() => repository.CountAsync());
		var error = await Should.ThrowAsync<BusinessException>(async () =>
		{
			if (version) await _workflowAppService.CreateNewVersionAsync(historical.Id);
			else await _workflowAppService.CloneAsync(historical.Id, new() { Name = "Rejected historical clone" });
		});
		error.Code.ShouldBe(AdmissionWorkflowErrorCodes.RuntimeHandlerUnavailable);
		(await WithUnitOfWorkAsync(() => repository.CountAsync())).ShouldBe(before);
		(await _workflowAppService.GetAsync(historical.Id)).Status.ShouldBe(historical.Status);
	});

	[Theory]
	[InlineData("Eligibility", AdmissionEligibilityErrorCodes.InvalidState)]
	[InlineData("Review", AdmissionReviewErrorCodes.InvalidState)]
	[InlineData("Offer", AdmissionOfferStageErrorCodes.InvalidState)]
	public Task Retired_access_rejects_scoped_applications_without_mutation(string feature, string expected) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("RetiredAccess");
		var before = await _applicationAppService.GetAsync(fixture.Application.Id);
		var error = await Should.ThrowAsync<BusinessException>(() => ReadRetiredContextAsync(feature, before.Id));
		error.Code.ShouldBe(expected);
		var after = await _applicationAppService.GetAsync(before.Id);
		after.Status.ShouldBe(before.Status); after.ConcurrencyStamp.ShouldBe(before.ConcurrencyStamp);
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<EntityNotFoundException>(() => ReadRetiredContextAsync(feature, before.Id));
	});

	private Task ReadRetiredContextAsync(string feature, Guid applicationId) => WithUnitOfWorkAsync(async () =>
	{
		if (feature == "Eligibility") await GetRequiredService<AdmissionEligibilityAccessService>().GetContextAsync(applicationId, Guid.NewGuid(), true);
		else if (feature == "Review") await GetRequiredService<AdmissionReviewAccessService>().GetContextAsync(applicationId, Guid.NewGuid(), true);
		else await GetRequiredService<AdmissionOfferAccessService>().GetContextAsync(applicationId, Guid.NewGuid());
	});

	[Theory]
	[InlineData("Review", AdmissionReviewErrorCodes.InvalidState)]
	[InlineData("Issue", AdmissionOfferStageErrorCodes.InvalidState)]
	[InlineData("Accept", AdmissionOfferStageErrorCodes.InvalidState)]
	[InlineData("Decline", AdmissionOfferStageErrorCodes.InvalidState)]
	public Task Retired_application_commands_reject_without_changing_current_decision(string command, string expected) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("RetiredCommand");
		var before = await _applicationAppService.GetAsync(fixture.Application.Id);
		var input = Decision(before, "Verify retired operation does not change current decision");
		var error = await Should.ThrowAsync<BusinessException>(async () =>
		{
			if (command == "Review") await _applicationAppService.MarkInReviewAsync(before.Id, input);
			else if (command == "Issue") await _applicationAppService.IssueOfferAsync(before.Id, input);
			else if (command == "Accept") await _applicationAppService.AcceptOfferAsync(before.Id, input);
			else await _applicationAppService.DeclineOfferAsync(before.Id, input);
		});
		error.Code.ShouldBe(expected);
		var after = await _applicationAppService.GetAsync(before.Id);
		after.Status.ShouldBe(before.Status); after.ConcurrencyStamp.ShouldBe(before.ConcurrencyStamp);
	});
}
