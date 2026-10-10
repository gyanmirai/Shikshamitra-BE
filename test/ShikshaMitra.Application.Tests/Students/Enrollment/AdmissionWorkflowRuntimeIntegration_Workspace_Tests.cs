using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Decision_history_should_resolve_the_recorded_actor_and_preserve_unknown_actor_records() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateDecisionWorkspaceOfferingAsync();
			var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("History summary")).Id, offering.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var users = GetRequiredService<IRepository<Volo.Abp.Identity.IdentityUser, Guid>>();
				var actor = await users.GetAsync(TestUserId);
				actor.Name = "Finalizing";
				actor.Surname = "Staff";
				await users.UpdateAsync(actor, true);
				var history = GetRequiredService<IRepository<AdmissionApplicationDecisionHistory, Guid>>();
				await history.InsertAsync(new AdmissionApplicationDecisionHistory(Guid.NewGuid(), TestTenantId,
					application.Id, AdmissionApplicationStatus.Approved, "Approved", TestUserId), true);
				await history.InsertAsync(new AdmissionApplicationDecisionHistory(Guid.NewGuid(), TestTenantId,
					application.Id, AdmissionApplicationStatus.AdmissionFinalized, "Finalized", Guid.NewGuid()), true);
			});
			var rows = await _applicationAppService.GetDecisionHistoryAsync(application.Id);
			rows.Count.ShouldBe(2);
			rows.Single(item => item.ActorId == TestUserId).ActorName.ShouldBe("Finalizing Staff");
			rows.Single(item => item.Status == AdmissionApplicationStatus.AdmissionFinalized).ActorName.ShouldBeNull();
		});

	[Fact]
	public Task Decision_workspace_should_transfer_on_stage_entry_and_keep_corrections_in_applications() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateDecisionWorkspaceOfferingAsync();
			var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("Riya")).Id, offering.Id);
			(await _applicationAppService.GetAsync(application.Id)).HasReachedDecision.ShouldBe(false);
			(await _applicationAppService.GetListAsync(new() { AdmissionOfferingId = offering.Id, DecisionWorkspaceOnly = true })).TotalCount.ShouldBe(0);
			(await _applicationAppService.GetListAsync(new() { AdmissionOfferingId = offering.Id })).Items.Single().Id.ShouldBe(application.Id);
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id })).Items.Single().Id.ShouldBe(application.Id);
			(await _applicationAppService.GetPipelineOptionsAsync(new() { AdmissionOfferingId = offering.Id })).Stages.ShouldNotContain(item => item.Code == "DECISION");

			(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
			var entered = await _applicationAppService.GetAsync(application.Id);
			entered.HasReachedDecision.ShouldBe(true);
			entered.Status.ShouldBe(AdmissionApplicationStatus.Submitted); // Visibility changes before approval, not after it.
			entered.DecisionEnteredAt.ShouldNotBeNull();
			entered.DecisionEnteredAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
			(await _applicationAppService.GetListAsync(new() { AdmissionOfferingId = offering.Id, DecisionWorkspaceOnly = true })).Items.Single().Id.ShouldBe(application.Id);
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id })).TotalCount.ShouldBe(0);
			(await _applicationAppService.GetPipelineOptionsAsync(new() { AdmissionOfferingId = offering.Id })).Stages.ShouldBeEmpty();

			// A correction can restore an earlier current stage/status; persisted entry owns the workspace permanently.
			await WithUnitOfWorkAsync(async () =>
			{
				var entity = await _applicationRepository.GetAsync(application.Id);
				entity.ReopenForManagedReview();
				var runtime = await GetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>().GetAsync(item => item.ApplicationId == entity.Id);
				var form = await GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>().GetAsync(item => item.WorkflowInstanceId == runtime.Id && item.StageType == AdmissionWorkflowStageType.Form);
				entity.SetCurrentStage(form.Id);
				await _applicationRepository.UpdateAsync(entity, true);
			});
			var corrected = await _applicationAppService.GetAsync(application.Id);
			corrected.Status.ShouldBe(AdmissionApplicationStatus.Draft);
			corrected.HasReachedDecision.ShouldBe(true);
			corrected.DecisionEnteredAt.ShouldBe(entered.DecisionEnteredAt);
			(await _applicationAppService.GetListAsync(new() { AdmissionOfferingId = offering.Id, DecisionWorkspaceOnly = true })).Items.Single().Id.ShouldBe(application.Id);
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id })).TotalCount.ShouldBe(0);
			// Older imported runtime snapshots may lack EnteredAt; actual transition history still proves the handoff.
			await WithUnitOfWorkAsync(async () =>
			{
				var repository = GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>();
				var decision = await repository.GetAsync(entered.CurrentStageInstanceId!.Value);
				typeof(AdmissionApplicationStageInstance).GetProperty(nameof(AdmissionApplicationStageInstance.EnteredAt))!.SetValue(decision, null);
				await repository.UpdateAsync(decision, true);
			});
			var recovered = await _applicationAppService.GetAsync(application.Id);
			recovered.HasReachedDecision.ShouldBe(true);
			recovered.DecisionEnteredAt.ShouldNotBeNull();
			recovered.DecisionEnteredAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id })).TotalCount.ShouldBe(0);
		});

	[Fact]
	public Task Decision_workspace_should_page_sort_search_and_preserve_scoped_counts() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateDecisionWorkspaceOfferingAsync();
			var first = await StartFixtureApplicationAsync((await CreateApplicantAsync("Aarav")).Id, offering.Id);
			var secondApplicant = await CreateApplicantAsync("Zoya");
			var second = await StartFixtureApplicationAsync(secondApplicant.Id, offering.Id);
			await StartFixtureApplicationAsync((await CreateApplicantAsync("Pending")).Id, offering.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var guardian = new AdmissionGuardian(Guid.NewGuid(), TestTenantId, secondApplicant.FamilyId, "Parent", "Zoya", "9123456789", "zoya.parent@example.test", AdmissionGuardianRelationship.Parent, true);
				await _guardianRepository.InsertAsync(guardian, true);
				var family = await _familyRepository.GetAsync(secondApplicant.FamilyId);
				family.SetPrimaryGuardian(guardian.Id);
				await _familyRepository.UpdateAsync(family, true);
			});
			await _applicationAppService.SubmitAsync(first.Id);
			await _applicationAppService.SubmitAsync(second.Id);
			var page = await _applicationAppService.GetListAsync(new() { DecisionWorkspaceOnly = true, AdmissionOfferingId = offering.Id, MaxResultCount = 1, Sorting = "applicantName asc" });
			page.TotalCount.ShouldBe(2);
			page.Items.Single().Id.ShouldBe(first.Id);
			var next = await _applicationAppService.GetListAsync(new() { DecisionWorkspaceOnly = true, AdmissionOfferingId = offering.Id, SkipCount = 1, MaxResultCount = 1, Sorting = "applicantName asc" });
			next.TotalCount.ShouldBe(2);
			next.Items.Single().Id.ShouldBe(second.Id);
			next.Items.Single().ContactPhone.ShouldBe("9123456789");
			next.Items.Single().ContactEmail.ShouldBe("zoya.parent@example.test");
			next.Items.Single().StandardName.ShouldNotBeNullOrWhiteSpace();
			foreach (var search in new[] { "ZOYA", "9123456789", "ZOYA.PARENT@EXAMPLE.TEST", second.ApplicationNumber })
				(await _applicationAppService.GetListAsync(new() { DecisionWorkspaceOnly = true, AdmissionOfferingId = offering.Id, Search = search })).Items.Single().Id.ShouldBe(second.Id);
			(await _applicationAppService.GetListAsync(new() { DecisionWorkspaceOnly = true, AdmissionOfferingId = offering.Id, Sorting = "decisionEnteredAt desc" })).TotalCount.ShouldBe(2);
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id })).TotalCount.ShouldBe(1);
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()), new Claim(AbpClaimTypes.UserName, "workspace-no-scope") }, "Test"))))
			{
				(await _applicationAppService.GetListAsync(new() { DecisionWorkspaceOnly = true })).TotalCount.ShouldBe(0);
				(await _applicationAppService.GetPipelineAsync(new() { MyCases = false })).TotalCount.ShouldBe(0);
			}
		});

	[Theory]
	[InlineData(AdmissionApplicationStatus.Approved, false)]
	[InlineData(AdmissionApplicationStatus.Waitlisted, false)]
	[InlineData(AdmissionApplicationStatus.Rejected, false)]
	[InlineData(AdmissionApplicationStatus.Withdrawn, false)]
	[InlineData(AdmissionApplicationStatus.InReview, false)]
	[InlineData(AdmissionApplicationStatus.AdmissionFinalized, true)]
	[InlineData(AdmissionApplicationStatus.Enrolled, true)]
	public Task Decision_workspace_should_not_treat_pending_decision_stage_as_entered(AdmissionApplicationStatus status, bool expected) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateDecisionWorkspaceOfferingAsync();
			var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("Pending decision")).Id, offering.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var entity = await _applicationRepository.GetAsync(application.Id);
				typeof(AdmissionApplication).GetProperty(nameof(AdmissionApplication.Status))!.SetValue(entity, status);
				await _applicationRepository.UpdateAsync(entity, true);
			});
			(await _applicationAppService.GetAsync(application.Id)).HasReachedDecision.ShouldBe(expected);
			(await _applicationAppService.GetListAsync(new() { DecisionWorkspaceOnly = true, AdmissionOfferingId = offering.Id })).TotalCount.ShouldBe(expected || status == AdmissionApplicationStatus.Withdrawn ? 1 : 0);
		});

	[Theory]
	[InlineData(AdmissionApplicationStatus.Approved)]
	[InlineData(AdmissionApplicationStatus.Waitlisted)]
	[InlineData(AdmissionApplicationStatus.Rejected)]
	[InlineData(AdmissionApplicationStatus.Offered)]
	[InlineData(AdmissionApplicationStatus.OfferAccepted)]
	[InlineData(AdmissionApplicationStatus.OfferDeclined)]
	[InlineData(AdmissionApplicationStatus.AdmissionFinalized)]
	[InlineData(AdmissionApplicationStatus.Enrolled)]
	public Task Decision_workspace_should_keep_historical_decisions_without_approval_stage(AdmissionApplicationStatus status) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateDecisionWorkspaceOfferingAsync();
			var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("Historical decision")).Id, offering.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var entity = await _applicationRepository.GetAsync(application.Id);
				// Simulate a pinned legacy runtime snapshot, without publishing retired stage types today.
				var stages = GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>();
				var runtime = await GetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>().GetAsync(item => item.ApplicationId == entity.Id);
				var decision = await stages.GetAsync(item => item.WorkflowInstanceId == runtime.Id && item.StageType == AdmissionWorkflowStageType.Approval);
				typeof(AdmissionApplicationStageInstance).GetProperty(nameof(AdmissionApplicationStageInstance.StageType))!.SetValue(decision, AdmissionWorkflowStageType.Custom);
				await stages.UpdateAsync(decision, true);
				typeof(AdmissionApplication).GetProperty(nameof(AdmissionApplication.Status))!.SetValue(entity, status);
				await _applicationRepository.UpdateAsync(entity, true);
			});
			(await _applicationAppService.GetListAsync(new() { DecisionWorkspaceOnly = true, AdmissionOfferingId = offering.Id })).Items.Single().Id.ShouldBe(application.Id);
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id })).TotalCount.ShouldBe(0);
		});

	private async Task<AdmissionOffering> CreateDecisionWorkspaceOfferingAsync()
	{
		var formId = Guid.NewGuid(); var decisionId = Guid.NewGuid();
		var workflow = await _workflowAppService.CreateAsync(new()
		{
			Name = "Application workspace handoff", Code = $"WORKSPACE_{Guid.NewGuid():N}"[..24],
			Stages = [new() { Id = formId, Code = "FORM", Name = "Application form", StageType = AdmissionWorkflowStageType.Form, IsEntryStage = true, IsRequired = true },
				new() { Id = decisionId, Code = "DECISION", Name = "Admission decision", StageType = AdmissionWorkflowStageType.Approval, DisplayOrder = 1, IsTerminalStage = true, IsRequired = true }],
			Transitions = [new() { FromStageId = formId, ToStageId = decisionId, IsDefault = true, Priority = 1 }]
		});
		var offering = await CreateAvailableOfferingAsync(workflow.Id, assertValid: false);
		var input = ToUpdateInput(workflow);
		input.Stages.Single(item => item.Code == "FORM").ConfigurationJson = JsonSerializer.Serialize(new
		{
			formDefinitionId = offering.ApplicationFormDefinitionId,
			form = new AdmissionFormStagePolicy { Enabled = true, Version = 1, AutoAdvanceOnSubmit = true }
		});
		workflow = await _workflowAppService.UpdateAsync(workflow.Id, input);
		await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
		return offering;
	}
}
