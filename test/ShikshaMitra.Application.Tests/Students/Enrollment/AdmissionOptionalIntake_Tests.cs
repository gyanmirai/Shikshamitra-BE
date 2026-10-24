using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Optional_intake_offering_should_create_activate_and_read_ready_without_profiles() =>
		RunCertificationScenarioAsync(async () =>
		{
			var source = await CreateOptionalIntakeOfferingAsync();
			var campus = new AdmissionCampus(Guid.NewGuid(), TestTenantId,
				$"OPT_{Guid.NewGuid():N}"[..24], "Optional intake campus");
			await _campusRepository.InsertAsync(campus, true);
			var offerings = GetRequiredService<IAdmissionOfferingAppService>();
			var created = await offerings.CreateAsync(source.AdmissionCycleId, new CreateAdmissionOfferingDto
			{
				CampusId = campus.Id, BoardId = source.BoardId, MediumId = source.MediumId,
				StageId = source.StageId, StandardId = source.StandardId, Code = $"OPT_{Guid.NewGuid():N}"[..24],
				UseCycleDates = true, WorkflowDefinitionId = source.WorkflowDefinitionId,
				InquiryFormDefinitionId = source.InquiryFormDefinitionId,
				ApplicationFormDefinitionId = source.ApplicationFormDefinitionId, IsActive = false
			});
			created.Configuration.IsValid.ShouldBeTrue();
			created.CanActivate.ShouldBeTrue();
			var active = await offerings.ActivateAsync(created.Id, created.ConcurrencyStamp);
			active.IsActive.ShouldBeTrue();
			active.InquiryIntakeProfileId.ShouldBeNull();
			active.CounsellorAssignmentProfileId.ShouldBeNull();
			(await offerings.GetListAsync(new() { ConfigurationStatus = AdmissionOfferingConfigurationStatus.Ready,
				MaxResultCount = 100 })).Items.ShouldContain(item => item.Id == active.Id);
			(await _offeringRepository.GetAsync(active.Id)).IntakeConfigurationRequired.ShouldBeFalse();
		});

	[Fact]
	public Task Optional_intake_offering_should_clear_existing_setup_and_ignore_stale_required_marker() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				typeof(AdmissionOffering).GetProperty(nameof(AdmissionOffering.IntakeConfigurationRequired))!.SetValue(stored, true);
				await _offeringRepository.UpdateAsync(stored, true);
			});
			var service = GetRequiredService<IAdmissionOfferingAppService>();
			(await service.GetAsync(offering.Id)).Configuration.IsValid.ShouldBeTrue();
			var setup = await CreateHistoricalIntakeSetupAsync(ValidSetupInput("Optional setup"));
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				stored.ConfigureIntake(setup.InquiryIntakeProfileId, setup.CounsellorAssignmentProfileId);
				await _offeringRepository.UpdateAsync(stored, true);
			});
			var current = await service.GetAsync(offering.Id);
			var cleared = await service.UpdateAsync(offering.Id, new UpdateAdmissionOfferingDto
			{
				CampusId = current.CampusId, BoardId = current.BoardId, MediumId = current.MediumId,
				StageId = current.StageId, StandardId = current.StandardId, Code = current.Code,
				Track = current.Track, EntryMode = current.EntryMode, Name = current.Name,
				UseCycleDates = current.UseCycleDates, OpenDate = current.OpenDate, CloseDate = current.CloseDate,
				WorkflowDefinitionId = current.WorkflowDefinitionId, InquiryFormDefinitionId = current.InquiryFormDefinitionId,
				ApplicationFormDefinitionId = current.ApplicationFormDefinitionId,
				IsActive = current.IsActive, ConcurrencyStamp = current.ConcurrencyStamp
			});
			cleared.Configuration.IsValid.ShouldBeTrue();
			cleared.CounsellorAssignmentProfileId.ShouldBeNull();
			(await _offeringRepository.GetAsync(offering.Id)).IntakeConfigurationRequired.ShouldBeFalse();
		});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Optional_intake_public_capture_should_remain_unassigned_despite_automatic_routing(bool usingSetup) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync(legacyPolicy: !usingSetup);
			await WithUnitOfWorkAsync(async () =>
			{
				if (usingSetup)
				{
					var setup = await CreateHistoricalIntakeSetupAsync(ValidSetupInput("Automatic setup"));
					var stored = await _offeringRepository.GetAsync(offering.Id);
					stored.ConfigureIntake(setup.InquiryIntakeProfileId, setup.CounsellorAssignmentProfileId);
					await _offeringRepository.UpdateAsync(stored, true);
				}
			});
			var input = ManagedInquiryInput(offering);
			var inquiry = await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionInquiryAppService>().CreateFromPublicLinkAsync(input, null,
				offering.InquiryFormDefinitionId));
			inquiry.AssignedCounsellorId.ShouldBeNull();
			(await GetRequiredService<IAdmissionInquiryAppService>().GetListAsync(new()
			{ IsAssigned = false, MaxResultCount = 100 })).Items.ShouldContain(item => item.Id == inquiry.Id);
		});



	private async Task<AdmissionOffering> CreateOptionalIntakeOfferingAsync(bool legacyPolicy = false)
	{
		var formStageId = Guid.NewGuid();
		var decisionStageId = Guid.NewGuid();
		var initial = await _workflowAppService.CreateAsync(new()
		{
			Name = "Optional intake fixture", Code = $"OPT_DRAFT_{Guid.NewGuid():N}"[..24],
			Stages =
			[
				new() { Id = formStageId, Code = "FORM", Name = "Application form", StageType = AdmissionWorkflowStageType.Form,
					DisplayOrder = 0, IsEntryStage = true, IsRequired = true },
				new() { Id = decisionStageId, Code = "DECISION", Name = "Admission decision", StageType = AdmissionWorkflowStageType.Approval,
					DisplayOrder = 1, IsTerminalStage = true, IsRequired = true }
			],
			Transitions = [new() { FromStageId = formStageId, ToStageId = decisionStageId, IsDefault = true }]
		});
		var offering = await CreateAvailableOfferingAsync(initial.Id, assertValid: false);
		var workflow = await _workflowAppService.CreateAsync(new()
		{
			Name = "Optional intake current workflow", Code = $"OPT_FLOW_{Guid.NewGuid():N}"[..24],
			Stages =
			[
				new() { Id = formStageId, Code = "FORM", Name = "Application form", StageType = AdmissionWorkflowStageType.Form,
					DisplayOrder = 0, IsEntryStage = true, IsRequired = true,
					ConfigurationJson = JsonSerializer.Serialize(new { formDefinitionId = offering.ApplicationFormDefinitionId,
						form = new AdmissionFormStagePolicy { Version = 1, Enabled = true, Responsibility = "shared",
							AllowSaveAndResume = true, AllowLateSubmission = true, MaxSubmissionAttempts = 3 } }) },
				new() { Id = decisionStageId, Code = "DECISION", Name = "Admission decision", StageType = AdmissionWorkflowStageType.Approval,
					DisplayOrder = 1, IsTerminalStage = true, IsRequired = true }
			],
			Transitions = [new() { FromStageId = formStageId, ToStageId = decisionStageId, IsDefault = true }]
		});
		workflow = await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await _offeringRepository.GetAsync(offering.Id);
			stored.ConfigureReferences(workflow.Id, stored.InquiryFormDefinitionId, stored.ApplicationFormDefinitionId);
			await _offeringRepository.UpdateAsync(stored, true);
			if (legacyPolicy)
			{
				// Historical published Inquiry policies remain supported; fresh editors cannot create them.
				var stages = GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>();
				var form = await stages.GetAsync(item => item.WorkflowDefinitionId == workflow.Id && item.IsEntryStage);
				form.Update(form.Code, form.Name, form.StageType, 1, form.IsRequired, form.AllowManualSkip,
					form.Description, form.ConfigurationJson, false, false);
				await stages.UpdateAsync(form, true);
				await stages.InsertAsync(new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.Id,
					"INQUIRY", "Historical inquiry", AdmissionWorkflowStageType.Inquiry, 0, true, false, null,
					JsonSerializer.Serialize(new { inquiry = new AdmissionInquiryPolicy { Version = 1, Enabled = true,
						DefaultCounsellorId = TestUserId, FirstContactDueInHours = 12, QualificationDueInDays = 3,
						QualificationChecklist = [new() { Code = "AGE", Name = "Age confirmed", Required = true }] } }),
					isEntryStage: true), true);
			}
		});
		offering.ConfigureReferences(workflow.Id, offering.InquiryFormDefinitionId, offering.ApplicationFormDefinitionId);
		return offering;
	}
}
