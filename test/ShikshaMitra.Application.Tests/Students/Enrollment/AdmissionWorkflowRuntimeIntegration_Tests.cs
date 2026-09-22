using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid TestUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

	private readonly IAdmissionWorkflowAppService _workflowAppService;
	private readonly IAdmissionApplicationAppService _applicationAppService;
	private readonly IAdmissionFormAppService _formAppService;
	private readonly IRepository<AdmissionCycle, Guid> _cycleRepository;
	private readonly IRepository<AdmissionOffering, Guid> _offeringRepository;
	private readonly IRepository<AdmissionCampus, Guid> _campusRepository;
	private readonly IRepository<AdmissionFormDefinition, Guid> _formRepository;
	private readonly IRepository<AdmissionFormSectionDefinition, Guid> _formSectionRepository;
	private readonly IRepository<AdmissionFormFieldDefinition, Guid> _formFieldRepository;
	private readonly IRepository<AdmissionFamily, Guid> _familyRepository;
	private readonly IRepository<AdmissionApplicant, Guid> _applicantRepository;
	private readonly IRepository<AdmissionGuardian, Guid> _guardianRepository;
	private readonly IRepository<AdmissionApplication, Guid> _applicationRepository;
	private readonly IRepository<StudentOnboardingCase, Guid> _onboardingCaseRepository;
	private readonly IRepository<StudentOnboardingStep, Guid> _onboardingStepRepository;
	private readonly IStudentRepository _studentRepository;
	private readonly IRepository<StudentEnrollment, Guid> _studentEnrollmentRepository;
	private readonly IRepository<AdmissionWorkflowTransitionHistory, Guid> _historyRepository;
	private readonly IRepository<UserScopeAssignment, Guid> _scopeRepository;
	private readonly IEducationMediumStageRepository _stageRepository;
	private readonly IEducationMediumRepository _mediumRepository;
	private readonly IEducationBoardRepository _boardRepository;
	private readonly IStandardRepository _standardRepository;
	private readonly StandardManager _standardManager;
	private readonly AdmissionOfferingConfigurationValidator _offeringValidator;
	private readonly ICurrentTenant _currentTenant;
	private readonly ICurrentPrincipalAccessor _principalAccessor;

	public AdmissionWorkflowRuntimeIntegration_Tests()
	{
		_workflowAppService = GetRequiredService<IAdmissionWorkflowAppService>();
		_applicationAppService = GetRequiredService<IAdmissionApplicationAppService>();
		_formAppService = GetRequiredService<IAdmissionFormAppService>();
		_cycleRepository = GetRequiredService<IRepository<AdmissionCycle, Guid>>();
		_offeringRepository = GetRequiredService<IRepository<AdmissionOffering, Guid>>();
		_campusRepository = GetRequiredService<IRepository<AdmissionCampus, Guid>>();
		_formRepository = GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>();
		_formSectionRepository = GetRequiredService<IRepository<AdmissionFormSectionDefinition, Guid>>();
		_formFieldRepository = GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>();
		_familyRepository = GetRequiredService<IRepository<AdmissionFamily, Guid>>();
		_applicantRepository = GetRequiredService<IRepository<AdmissionApplicant, Guid>>();
		_guardianRepository = GetRequiredService<IRepository<AdmissionGuardian, Guid>>();
		_applicationRepository = GetRequiredService<IRepository<AdmissionApplication, Guid>>();
		_onboardingCaseRepository = GetRequiredService<IRepository<StudentOnboardingCase, Guid>>();
		_onboardingStepRepository = GetRequiredService<IRepository<StudentOnboardingStep, Guid>>();
		_studentRepository = GetRequiredService<IStudentRepository>();
		_studentEnrollmentRepository = GetRequiredService<IRepository<StudentEnrollment, Guid>>();
		_historyRepository = GetRequiredService<IRepository<AdmissionWorkflowTransitionHistory, Guid>>();
		_scopeRepository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		_stageRepository = GetRequiredService<IEducationMediumStageRepository>();
		_mediumRepository = GetRequiredService<IEducationMediumRepository>();
		_boardRepository = GetRequiredService<IEducationBoardRepository>();
		_standardRepository = GetRequiredService<IStandardRepository>();
		_standardManager = GetRequiredService<StandardManager>();
		_offeringValidator = GetRequiredService<AdmissionOfferingConfigurationValidator>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
	}

	[Fact]
	public async Task Published_Version_Should_Remain_Frozen_For_Existing_Application()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Workflow integration test"),
				autoSave: true);
			var workflowV1 = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflowV1.Id);
			var aarav = await CreateApplicantAsync("Aarav");

			var aaravApplication = await _applicationAppService.StartAsync(aarav.Id, offering.Id);
			var aaravRuntime = await _applicationAppService.GetWorkflowAsync(aaravApplication.Id);
			aaravRuntime.WorkflowDefinitionId.ShouldBe(workflowV1.Id);
			aaravRuntime.NextRequiredAction.ShouldBe("Review and qualify the inquiry");
			aaravRuntime.Stages.Single(stage => stage.Code == "INQUIRY").Status
				.ShouldBe(AdmissionRuntimeStageStatus.Active);

			var firstStageId = aaravRuntime.CurrentStageInstanceId.ShouldNotBeNull();
			aaravRuntime = await _applicationAppService.AdvanceWorkflowAsync(
				aaravApplication.Id,
				new AdvanceAdmissionWorkflowDto
				{
					ExpectedCurrentStageInstanceId = firstStageId,
					IdempotencyKey = "aarav-inquiry-complete",
					ConcurrencyStamp = aaravApplication.ConcurrencyStamp
				});
			aaravRuntime.Stages.Single(stage => stage.Code == "INQUIRY").Status
				.ShouldBe(AdmissionRuntimeStageStatus.Completed);
			aaravRuntime.Stages.Single(stage => stage.Code == "ADMISSION").Status
				.ShouldBe(AdmissionRuntimeStageStatus.Active);

			var terminalStageId = aaravRuntime.CurrentStageInstanceId.ShouldNotBeNull();
			aaravRuntime = await _applicationAppService.AdvanceWorkflowAsync(
				aaravApplication.Id,
				new AdvanceAdmissionWorkflowDto
				{
					ExpectedCurrentStageInstanceId = terminalStageId,
					IdempotencyKey = "aarav-admission-complete",
					ConcurrencyStamp = (await _applicationAppService.GetAsync(aaravApplication.Id)).ConcurrencyStamp
				});
			aaravRuntime.Status.ShouldBe(AdmissionWorkflowInstanceStatus.Completed);
			aaravRuntime.CompletedAt.ShouldNotBeNull();
			aaravRuntime.NextRequiredAction.ShouldBeNull();

			var historyCount = await CountHistoryAsync(aaravApplication.Id);
			await _applicationAppService.AdvanceWorkflowAsync(
				aaravApplication.Id,
				new AdvanceAdmissionWorkflowDto
				{
					ExpectedCurrentStageInstanceId = terminalStageId,
					IdempotencyKey = "aarav-admission-complete",
					ConcurrencyStamp = (await _applicationAppService.GetAsync(aaravApplication.Id)).ConcurrencyStamp
				});
			(await CountHistoryAsync(aaravApplication.Id)).ShouldBe(historyCount);
			await CreateCampusScopedImpactOfferingAsync(offering, workflowV1.Id, "NORTH");
			await CreateCampusScopedImpactOfferingAsync(offering, workflowV1.Id, "SOUTH");

			var workflowV2 = await _workflowAppService.CreateNewVersionAsync(workflowV1.Id);
			var v2Input = ToUpdateInput(workflowV2);
			v2Input.Stages.Single(stage => stage.Code == "ADMISSION").Name = "Enrollment Decision";
			workflowV2 = await _workflowAppService.UpdateAsync(workflowV2.Id, v2Input);
			workflowV2 = await _workflowAppService.PublishAsync(workflowV2.Id, workflowV2.ConcurrencyStamp);
			var versionHistory = await _workflowAppService.GetVersionHistoryAsync(workflowV2.Id);
			versionHistory.Count.ShouldBe(2);
			var versionOneHistory = versionHistory.Single(version => version.Version == 1);
			versionOneHistory.Stages.Count.ShouldBe(2);
			versionOneHistory.Transitions.Count.ShouldBe(1);
			versionOneHistory.OfferingCount.ShouldBe(3);
			versionOneHistory.CampusCount.ShouldBe(2);
			versionHistory.Single(version => version.Version == 2).Stages
				.Single(stage => stage.Code == "ADMISSION").Name.ShouldBe("Enrollment Decision");

			await WithUnitOfWorkAsync(async () =>
			{
				var currentOffering = await _offeringRepository.GetAsync(offering.Id);
				currentOffering.ConfigureReferences(
					workflowV2.Id,
					currentOffering.InquiryFormDefinitionId,
					currentOffering.ApplicationFormDefinitionId);
				await _offeringRepository.UpdateAsync(currentOffering, autoSave: true);
			});

			var diya = await CreateApplicantAsync("Diya");
			var diyaApplication = await _applicationAppService.StartAsync(diya.Id, offering.Id);
			var diyaRuntime = await _applicationAppService.GetWorkflowAsync(diyaApplication.Id);

			diyaRuntime.WorkflowDefinitionId.ShouldBe(workflowV2.Id);
			diyaRuntime.Stages.Single(stage => stage.Code == "ADMISSION").Name
				.ShouldBe("Enrollment Decision");
			(await _applicationAppService.GetWorkflowAsync(aaravApplication.Id)).WorkflowDefinitionId
				.ShouldBe(workflowV1.Id);

			var v1Usage = await _workflowAppService.GetUsageAsync(workflowV1.Id);
			v1Usage.OfferingCount.ShouldBe(2);
			v1Usage.CampusCount.ShouldBe(2);
			v1Usage.Offerings.Select(item => item.CampusId).ShouldAllBe(campusId => campusId.HasValue);
			v1Usage.RuntimeAnalytics.StartedCount.ShouldBe(1);
			v1Usage.RuntimeAnalytics.CompletedCount.ShouldBe(1);
			v1Usage.RuntimeAnalytics.CompletionRatePercent.ShouldBe(100m);
			v1Usage.RuntimeAnalytics.Stages.Count.ShouldBe(2);
			v1Usage.RuntimeAnalytics.Stages.ShouldAllBe(stage => stage.CompletedCount == 1);
			var v2Usage = await _workflowAppService.GetUsageAsync(workflowV2.Id);
			v2Usage.OfferingCount.ShouldBe(1);
			v2Usage.CampusCount.ShouldBe(0);
			v2Usage.RuntimeAnalytics.StartedCount.ShouldBe(1);
			v2Usage.RuntimeAnalytics.ActiveCount.ShouldBe(1);
			v2Usage.RuntimeAnalytics.Stages.Single().Code.ShouldBe("INQUIRY");
		}
	}

	[Fact]
	public async Task Inquiry_Form_Values_Should_Update_Identity_And_Prefill_Application()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Inquiry carryover test"),
				autoSave: true);
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var inquiryService = GetRequiredService<IAdmissionInquiryAppService>();
			var inquiry = await inquiryService.CreateAsync(new CreateAdmissionInquiryDto
			{
				AdmissionCycleId = offering.AdmissionCycleId,
				AdmissionOfferingId = offering.Id,
				BoardId = offering.BoardId,
				MediumId = offering.MediumId,
				StandardId = offering.StandardId,
				ApplicantName = "Fallback Applicant",
				Phone = "9000000000",
				Source = AdmissionInquirySource.Website,
				GuardianFirstName = "Meera",
				GuardianPhone = "9000000000",
				ApplicantFirstName = "Fallback",
				FormValues =
				[
					new() { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"Aanya\"" },
					new() { FieldCode = "APPLICANT_DOB", ValueJson = "\"2016-04-12T00:00:00+05:30\"" },
					new() { FieldCode = "APPLICANT_GENDER", ValueJson = "\"Female\"" },
					new() { FieldCode = "GUARDIAN_MOBILE", ValueJson = "\"9876543210\"" },
					new() { FieldCode = "LEARNING_NOTES", ValueJson = "\"Benefits from visual learning prompts\"" }
				]
			});

			var applicant = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(inquiry.ApplicantId!.Value));
			var guardian = (await WithUnitOfWorkAsync(() => _guardianRepository.GetListAsync(x => x.FamilyId == applicant.FamilyId))).Single();
			applicant.FirstName.ShouldBe("Aanya");
			applicant.DateOfBirth.ShouldBe(new DateTime(2016, 4, 12));
			applicant.Gender.ShouldBe("Female");
			guardian.Mobile.ShouldBe("9876543210");

			var application = await _applicationAppService.StartAsync(applicant.Id, offering.Id, inquiry.Id);
			var form = await _applicationAppService.GetFormAsync(application.Id);
			form.Values.Single(value => value.FieldCode == "LEARNING_NOTES").ValueJson
				.ShouldBe("\"Benefits from visual learning prompts\"");

			var exactInquiry = await inquiryService.GetListAsync(new AdmissionInquiryListInput
			{
				InquiryNumber = inquiry.InquiryNumber,
				InquiryNumberFilterType = "equals",
				MaxResultCount = 10
			});
			exactInquiry.Items.ShouldContain(item => item.Id == inquiry.Id);

			var mainInquiryQueue = await inquiryService.GetListAsync(new AdmissionInquiryListInput
			{
				InquiryNumber = inquiry.InquiryNumber,
				InquiryNumberFilterType = "equals",
				ExcludePostApplication = true,
				MaxResultCount = 10
			});
			mainInquiryQueue.Items.ShouldNotContain(item => item.Id == inquiry.Id);

			var activePipeline = await inquiryService.GetListAsync(new AdmissionInquiryListInput
			{
				InquiryNumber = inquiry.InquiryNumber,
				InquiryNumberFilterType = "equals",
				PipelineGroup = "active",
				MaxResultCount = 10
			});
			activePipeline.Items.ShouldNotContain(item => item.Id == inquiry.Id);
			var handoffPipeline = await inquiryService.GetListAsync(new AdmissionInquiryListInput
			{
				InquiryNumber = inquiry.InquiryNumber,
				InquiryNumberFilterType = "equals",
				PipelineGroup = "handoff",
				MaxResultCount = 10
			});
			handoffPipeline.Items.ShouldContain(item => item.Id == inquiry.Id);
		}
	}

	[Fact]
	public async Task Inquiry_Should_Report_Incomplete_Identity_As_A_Business_Error()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Inquiry identity validation test"),
				autoSave: true);
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var inquiryService = GetRequiredService<IAdmissionInquiryAppService>();

			var error = await Should.ThrowAsync<BusinessException>(() => inquiryService.CreateAsync(
				new CreateAdmissionInquiryDto
				{
					AdmissionCycleId = offering.AdmissionCycleId,
					AdmissionOfferingId = offering.Id,
					BoardId = offering.BoardId,
					MediumId = offering.MediumId,
					StandardId = offering.StandardId,
					ApplicantName = "Riya Mehta",
					ApplicantFirstName = "Riya",
					Phone = "9876501011",
					GuardianPhone = "9876501011",
					Source = AdmissionInquirySource.Website,
					FormValues =
					[
						new() { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"Riya\"" },
						new() { FieldCode = "APPLICANT_DOB", ValueJson = "\"2019-11-22T00:00:00+05:30\"" },
						new() { FieldCode = "APPLICANT_GENDER", ValueJson = "\"Female\"" },
						new() { FieldCode = "GUARDIAN_MOBILE", ValueJson = "\"9876501011\"" },
						new() { FieldCode = "LEARNING_NOTES", ValueJson = "\"Enjoys reading\"" }
					]
				}));

			error.Code.ShouldBe(AdmissionErrorCodes.InquiryIdentityIncomplete);
			error.Data["Fields"].ShouldBe("Guardian.FirstName");
		}
	}

	[Fact]
	public async Task Applicant_Profile_Should_Be_Repairable_Before_Finalization()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			var applicant = await WithUnitOfWorkAsync(async () =>
			{
				var family = new AdmissionFamily(Guid.NewGuid(), TestTenantId, $"REPAIR-{Guid.NewGuid():N}"[..20]);
				await _familyRepository.InsertAsync(family, autoSave: true);
				var incomplete = new AdmissionApplicant(
					Guid.NewGuid(), TestTenantId, family.Id, "Aanya", null, null, null, null, null);
				await _applicantRepository.InsertAsync(incomplete, autoSave: true);
				return incomplete;
			});

			var applicantService = GetRequiredService<IAdmissionApplicantAppService>();
			var updated = await applicantService.UpdateProfileAsync(applicant.Id,
				new UpdateAdmissionApplicantProfileDto
				{
					FirstName = "Aanya",
					LastName = "Shah",
					DateOfBirth = new DateTime(2016, 4, 12),
					Gender = "Female"
				});

			updated.LastName.ShouldBe("Shah");
			updated.DateOfBirth.ShouldBe(new DateTime(2016, 4, 12));
			updated.Gender.ShouldBe("Female");
		}
	}

	[Fact]
	public async Task Offering_Should_Not_Require_Application_Identity_Fields_For_Readiness()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Offering identity test"),
				autoSave: true);
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id, assertValid: false);
			var incompleteForm = await WithUnitOfWorkAsync(async () =>
			{
				var form = new AdmissionFormDefinition(Guid.NewGuid(), TestTenantId, "Incomplete form",
					$"INCOMPLETE_{Guid.NewGuid():N}"[..24], AdmissionFormLayoutType.SinglePage);
				form.Publish(Guid.NewGuid());
				await _formRepository.InsertAsync(form, autoSave: true);
				return form;
			});
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				stored.ConfigureReferences(workflow.Id, incompleteForm.Id, incompleteForm.Id);
				await _offeringRepository.UpdateAsync(stored, autoSave: true);
			});

			var validation = await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				return await _offeringValidator.ValidateStoredAsync(stored);
			});
			validation.Errors.ShouldNotContain(error =>
				error.Code == AdmissionOfferingValidationCodes.ApplicationFormIdentityIncomplete);
		}
	}

	[Fact]
	public async Task Published_Form_Version_Should_Remain_Frozen_For_Existing_Application()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Form version integration test"), autoSave: true);
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var aarav = await CreateApplicantAsync("Aarav");
			var aaravApplication = await _applicationAppService.StartAsync(aarav.Id, offering.Id);
			var aaravForm = await _applicationAppService.GetFormAsync(aaravApplication.Id);
			aaravForm.FormVersion.ShouldBe(1);

			var formV2 = await _formAppService.CreateNewVersionAsync(aaravForm.FormDefinitionId);
			formV2 = await _formAppService.PublishAsync(formV2.Id, formV2.ConcurrencyStamp);
			await WithUnitOfWorkAsync(async () =>
			{
				var currentOffering = await _offeringRepository.GetAsync(offering.Id);
				currentOffering.ConfigureReferences(currentOffering.WorkflowDefinitionId, currentOffering.InquiryFormDefinitionId, formV2.Id);
				await _offeringRepository.UpdateAsync(currentOffering, autoSave: true);
			});

			var diya = await CreateApplicantAsync("Diya");
			var diyaApplication = await _applicationAppService.StartAsync(diya.Id, offering.Id);
			(await _applicationAppService.GetFormAsync(aaravApplication.Id)).FormDefinitionId.ShouldBe(aaravForm.FormDefinitionId);
			(await _applicationAppService.GetFormAsync(aaravApplication.Id)).FormVersion.ShouldBe(1);
			(await _applicationAppService.GetFormAsync(diyaApplication.Id)).FormDefinitionId.ShouldBe(formV2.Id);
			(await _applicationAppService.GetFormAsync(diyaApplication.Id)).FormVersion.ShouldBe(2);
		}
	}

	[Fact]
	public async Task Form_Runtime_Should_Save_Submit_Audit_And_Reopen()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Form runtime integration test"), autoSave: true);
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var applicant = await CreateApplicantAsync("Aarav");
			var application = await _applicationAppService.StartAsync(applicant.Id, offering.Id);
			var form = await _applicationAppService.GetFormAsync(application.Id);

			var saved = await _applicationAppService.UpdateFormValuesAsync(application.Id, new UpdateAdmissionApplicationFormValuesDto
			{
				ConcurrencyStamp = application.ConcurrencyStamp,
				Values =
				[
					new AdmissionFormValueDto { FieldCode = "APPLICANT_NAME", ValueJson = "\"Aarav updated\"" },
					new AdmissionFormValueDto { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"Aarav writeback\"" }
				]
			});
			saved.Values.Single(value => value.FieldCode == "APPLICANT_NAME").ValueJson.ShouldBe("\"Aarav updated\"");
			(await _applicantRepository.GetAsync(applicant.Id)).FirstName.ShouldBe("Aarav writeback");

			var submitted = await _applicationAppService.SubmitAsync(application.Id);
			submitted.ValidationErrors.ShouldBeEmpty();
			(await _applicationAppService.GetFormAsync(application.Id)).Status.ShouldBe(AdmissionFormInstanceStatus.Submitted);
			var history = await _applicationAppService.GetFormHistoryAsync(application.Id);
			history.Select(item => item.ChangeType).ShouldContain("Created");
			history.Select(item => item.ChangeType).ShouldContain("DraftSaved");
			history.Select(item => item.ChangeType).ShouldContain("Submitted");

			var reopened = await _applicationAppService.ReopenFormAsync(application.Id);
			reopened.Status.ShouldBe(AdmissionFormInstanceStatus.Reopened);
		}
	}

	[Fact]
	public async Task Sunrise_Grade5_Workflow_Should_Create_The_Frozen_Eight_Stage_Journey()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Real-world workflow test"),
				autoSave: true);
			var workflow = await CreateAndPublishRealisticWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var aarav = await CreateApplicantAsync("Aarav");

			var application = await _applicationAppService.StartAsync(aarav.Id, offering.Id);
			var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);

			runtime.WorkflowDefinitionId.ShouldBe(workflow.Id);
			runtime.Stages.OrderBy(stage => stage.DisplayOrder).Select(stage => stage.Code).ShouldBe(
			[
				"INQUIRY", "ENTRANCE_EXAM", "REGISTRATION_FEE", "REGISTRATION_FORM",
				"DOCUMENT_VERIFICATION", "ADMISSION_ELIGIBILITY", "PRINCIPAL_APPROVAL", "ADMISSION"
			]);
			runtime.Stages.Single(stage => stage.Code == "INQUIRY").Status
				.ShouldBe(AdmissionRuntimeStageStatus.Active);
			runtime.Stages.Where(stage => stage.Code != "INQUIRY").ShouldAllBe(stage =>
				stage.Status == AdmissionRuntimeStageStatus.Pending);
		}
	}

	private async Task<AdmissionWorkflowDto> CreateAndPublishWorkflowAsync()
	{
		var entryId = Guid.NewGuid();
		var terminalId = Guid.NewGuid();
		var workflow = await _workflowAppService.CreateAsync(new CreateUpdateAdmissionWorkflowDto
		{
			Name = "Grade 5 Admission",
			Code = $"GRADE5_{Guid.NewGuid():N}"[..24],
			Description = "Sunrise Public School admission workflow",
			Stages =
			[
				new CreateUpdateAdmissionWorkflowStageDto
				{
					Id = entryId,
					Code = "INQUIRY",
					Name = "Inquiry",
					StageType = AdmissionWorkflowStageType.Inquiry,
					DisplayOrder = 0,
					IsEntryStage = true,
					IsRequired = true
				},
				new CreateUpdateAdmissionWorkflowStageDto
				{
					Id = terminalId,
					Code = "ADMISSION",
					Name = "Admission",
					StageType = AdmissionWorkflowStageType.Custom,
					DisplayOrder = 1,
					IsTerminalStage = true,
					IsRequired = true
				}
			],
			Transitions =
			[
				new CreateUpdateAdmissionWorkflowTransitionDto
				{
					FromStageId = entryId,
					ToStageId = terminalId,
					Priority = 1,
					IsDefault = true
				}
			]
		});

		return await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
	}

	private async Task<AdmissionWorkflowDto> CreateAndPublishRealisticWorkflowAsync()
	{
		var stageBlueprints = new[]
		{
			("INQUIRY", "Inquiry", AdmissionWorkflowStageType.Inquiry),
			("ENTRANCE_EXAM", "Entrance Exam", AdmissionWorkflowStageType.Assessment),
			("REGISTRATION_FEE", "Registration Form Fee", AdmissionWorkflowStageType.Payment),
			("REGISTRATION_FORM", "Physical Registration Form", AdmissionWorkflowStageType.Custom),
			("DOCUMENT_VERIFICATION", "Document Verification", AdmissionWorkflowStageType.DocumentVerification),
			("ADMISSION_ELIGIBILITY", "Admission Eligibility", AdmissionWorkflowStageType.Eligibility),
			("PRINCIPAL_APPROVAL", "Principal Approval", AdmissionWorkflowStageType.Approval),
			("ADMISSION", "Admission", AdmissionWorkflowStageType.Enrollment)
		};
		var stages = stageBlueprints.Select((stage, index) => new CreateUpdateAdmissionWorkflowStageDto
		{
			Id = Guid.NewGuid(),
			Code = stage.Item1,
			Name = stage.Item2,
			StageType = stage.Item3,
			DisplayOrder = index,
			IsRequired = true,
			IsEntryStage = index == 0,
			IsTerminalStage = index == stageBlueprints.Length - 1
		}).ToList();
		var transitions = stages.Zip(stages.Skip(1), (from, to) =>
			new CreateUpdateAdmissionWorkflowTransitionDto
			{
				FromStageId = from.Id!.Value,
				ToStageId = to.Id!.Value,
				Priority = 1,
				IsDefault = true
			}).ToList();
		var workflow = await _workflowAppService.CreateAsync(new CreateUpdateAdmissionWorkflowDto
		{
			Name = "Grade 5 Admission",
			Code = $"GRADE5_REAL_{Guid.NewGuid():N}"[..24],
			Description = "Sunrise Public School Admissions 2027-28 journey",
			Stages = stages,
			Transitions = transitions
		});

		return await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
	}

	private Task<AdmissionOffering> CreateAvailableOfferingAsync(Guid workflowId, bool assertValid = true) =>
		WithUnitOfWorkAsync(async () =>
	{
		var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var stage = await _stageRepository.GetAsync(stageId);
		var medium = await _mediumRepository.GetAsync(stage.EducationMediumId);
		var board = await _boardRepository.GetAsync(medium.EducationBoardId);
		var standard = (await _standardRepository.GetListAsync(item =>
			item.EducationMediumStageId == stage.Id)).FirstOrDefault();
		if (standard == null)
		{
			standard = await _standardManager.CreateAsync(stage.Id, $"Grade 5 {Guid.NewGuid():N}"[..30], 10, null);
			await _standardRepository.InsertAsync(standard, autoSave: true);
		}

		var cycle = new AdmissionCycle(
			Guid.NewGuid(),
			TestTenantId,
			"Admissions 2027-28",
			$"CYCLE_{Guid.NewGuid():N}"[..24],
			board.AcademicYearId,
			new DateTime(2026, 1, 1),
			new DateTime(2027, 12, 31));
		cycle.Publish(Guid.NewGuid(), DateTime.UtcNow);
		await _cycleRepository.InsertAsync(cycle, autoSave: true);

		var form = new AdmissionFormDefinition(
			Guid.NewGuid(),
			TestTenantId,
			"Grade 5 Application Form",
			$"FORM_{Guid.NewGuid():N}"[..24],
			AdmissionFormLayoutType.SinglePage);
		form.Publish(Guid.NewGuid());
		await _formRepository.InsertAsync(form, autoSave: true);
		var section = new AdmissionFormSectionDefinition(Guid.NewGuid(), form.Id, "STUDENT", "Student details", 0, null);
		await _formSectionRepository.InsertAsync(section, autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(
			Guid.NewGuid(), form.Id, section.Id, "APPLICANT_NAME", "Applicant name", AdmissionFormFieldType.Text,
			AdmissionFormBindingType.Custom, "ApplicantName", false, false, 0, null, null, null, null, null), autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(
			Guid.NewGuid(), form.Id, section.Id, "APPLICANT_FIRST_NAME", "Applicant first name", AdmissionFormFieldType.Text,
			AdmissionFormBindingType.System, "Applicant.FirstName", true, false, 1, null, null, null, null, null), autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(
			Guid.NewGuid(), form.Id, section.Id, "APPLICANT_DOB", "Applicant date of birth", AdmissionFormFieldType.Date,
			AdmissionFormBindingType.System, "Applicant.DateOfBirth", true, false, 2, null, null, null, null, null), autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(
			Guid.NewGuid(), form.Id, section.Id, "APPLICANT_GENDER", "Applicant gender", AdmissionFormFieldType.Select,
			AdmissionFormBindingType.System, "Applicant.Gender", true, false, 3, null, null, null, null, null,
			"[{\"value\":\"Male\",\"label\":\"Male\"},{\"value\":\"Female\",\"label\":\"Female\"}]"), autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(
			Guid.NewGuid(), form.Id, section.Id, "GUARDIAN_MOBILE", "Guardian mobile", AdmissionFormFieldType.Phone,
			AdmissionFormBindingType.System, "Guardian.Mobile", false, false, 4, null, null, null, null, null), autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(
			Guid.NewGuid(), form.Id, section.Id, "LEARNING_NOTES", "Learning notes", AdmissionFormFieldType.Textarea,
			AdmissionFormBindingType.Custom, "LearningNotes", false, false, 5, null, null, null, null, null), autoSave: true);

		var offering = new AdmissionOffering(
			Guid.NewGuid(),
			TestTenantId,
			cycle.Id,
			null,
			board.Id,
			medium.Id,
			stage.Id,
			standard.Id,
			$"G5_{Guid.NewGuid():N}"[..20],
			useCycleDates: true,
			null,
			null);
		offering.ConfigureReferences(workflowId, form.Id, form.Id);
		await _offeringRepository.InsertAsync(offering, autoSave: true);
		if (assertValid)
		{
			var validation = await _offeringValidator.ValidateStoredAsync(offering);
			validation.Errors.ShouldBeEmpty(string.Join(" | ", validation.Errors.Select(error =>
				$"{error.Code}: {error.Message}")));
		}
		return offering;
	});

	private Task CreateCampusScopedImpactOfferingAsync(
		AdmissionOffering source,
		Guid workflowId,
		string campusCode) => WithUnitOfWorkAsync(async () =>
	{
		var campus = new AdmissionCampus(
			Guid.NewGuid(), TestTenantId, $"{campusCode}_{Guid.NewGuid():N}"[..24], $"{campusCode} Campus");
		await _campusRepository.InsertAsync(campus, autoSave: true);
		var offering = new AdmissionOffering(
			Guid.NewGuid(), TestTenantId, source.AdmissionCycleId, campus.Id, source.BoardId, source.MediumId,
			source.StageId, source.StandardId, $"{campusCode}_{Guid.NewGuid():N}"[..20], true, null, null);
		offering.ConfigureReferences(
			workflowId, source.InquiryFormDefinitionId, source.ApplicationFormDefinitionId);
		await _offeringRepository.InsertAsync(offering, autoSave: true);
	});

	private async Task<AdmissionApplicant> CreateApplicantAsync(string firstName)
	{
		var family = new AdmissionFamily(Guid.NewGuid(), TestTenantId, $"FAM-{Guid.NewGuid():N}");
		await _familyRepository.InsertAsync(family, autoSave: true);
		var applicant = new AdmissionApplicant(
			Guid.NewGuid(), TestTenantId, family.Id, firstName, null, "Patel", new DateTime(2016, 5, 14), "Male", null);
		await _applicantRepository.InsertAsync(applicant, autoSave: true);
		return applicant;
	}

	private Task<int> CountHistoryAsync(Guid applicationId) =>
		WithUnitOfWorkAsync(() => _historyRepository.CountAsync(history =>
			history.ApplicationId == applicationId));

	private static CreateUpdateAdmissionWorkflowDto ToUpdateInput(AdmissionWorkflowDto workflow) => new()
	{
		Name = workflow.Name,
		Code = workflow.Code,
		Description = workflow.Description,
		ConcurrencyStamp = workflow.ConcurrencyStamp,
		Stages = workflow.Stages.Select(stage => new CreateUpdateAdmissionWorkflowStageDto
		{
			Id = stage.Id,
			Code = stage.Code,
			Name = stage.Name,
			Description = stage.Description,
			StageType = stage.StageType,
			DisplayOrder = stage.DisplayOrder,
			IsRequired = stage.IsRequired,
			AllowManualSkip = stage.AllowManualSkip,
			IsEntryStage = stage.IsEntryStage,
			IsTerminalStage = stage.IsTerminalStage,
			ConfigurationJson = stage.ConfigurationJson
		}).ToList(),
		Transitions = workflow.Transitions.Select(transition => new CreateUpdateAdmissionWorkflowTransitionDto
		{
			Id = transition.Id,
			FromStageId = transition.FromStageId,
			ToStageId = transition.ToStageId,
			Priority = transition.Priority,
			ConditionRuleJson = transition.ConditionRuleJson,
			IsDefault = transition.IsDefault
		}).ToList()
	};

	private static ClaimsPrincipal BuildAdminPrincipal()
	{
		return new ClaimsPrincipal(new ClaimsIdentity(
		[
			new Claim(AbpClaimTypes.UserId, TestUserId.ToString()),
			new Claim(AbpClaimTypes.UserName, $"workflow-admin-{TestUserId:N}"),
			new Claim(AbpClaimTypes.Role, "admin")
		], "Test"));
	}
}
