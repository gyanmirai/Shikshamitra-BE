using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
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
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
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

			var aaravApplication = await StartFixtureApplicationAsync(aarav.Id, offering.Id);
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
			var diyaApplication = await StartFixtureApplicationAsync(diya.Id, offering.Id);
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
			var studentCountBeforeInquiry = await WithUnitOfWorkAsync(() => _studentRepository.CountAsync());
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
			applicant.ReservedStudentId.ShouldNotBe(Guid.Empty);
			applicant.ReservedStudentId.ShouldNotBe(applicant.Id);
			(await WithUnitOfWorkAsync(() => _studentRepository.FindAsync(applicant.ReservedStudentId)))
				.ShouldBeNull();
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(studentCountBeforeInquiry);
			guardian.Mobile.ShouldBe("9876543210");

			inquiry = await inquiryService.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
			var application = await _applicationAppService.StartAsync(applicant.Id, offering.Id, inquiry.Id);
			application.StudentId.ShouldBeNull();
			(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(applicant.Id)))
				.ReservedStudentId.ShouldBe(applicant.ReservedStudentId);
			(await WithUnitOfWorkAsync(() => _studentRepository.FindAsync(applicant.ReservedStudentId)))
				.ShouldBeNull();
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(studentCountBeforeInquiry);
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
					Guid.NewGuid(), TestTenantId, family.Id, "Aanya", null, null, null, null, null, Guid.NewGuid());
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
			(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(applicant.Id)))
				.ReservedStudentId.ShouldBe(applicant.ReservedStudentId);
			(await WithUnitOfWorkAsync(() => _studentRepository.FindAsync(applicant.ReservedStudentId)))
				.ShouldBeNull();
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
	public async Task Inquiry_And_Registration_Drafts_Should_Publish_Without_Approval()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
				userId: TestUserId, description: "Direct form publication integration test"), autoSave: true);
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);

			foreach (var formId in new[] { offering.InquiryFormDefinitionId, offering.ApplicationFormDefinitionId })
			{
				var draft = await _formAppService.CreateNewVersionAsync(formId!.Value);
				draft.Status.ShouldBe(AdmissionFormStatus.Draft);
				var published = await _formAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
				published.Status.ShouldBe(AdmissionFormStatus.Published);
			}
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
			var aaravApplication = await StartFixtureApplicationAsync(aarav.Id, offering.Id);
			var aaravForm = await _applicationAppService.GetFormAsync(aaravApplication.Id);
			aaravForm.FormVersion.ShouldBe(1);

			var formV2 = await _formAppService.CreateNewVersionAsync(aaravForm.FormDefinitionId);
			formV2.Status.ShouldBe(AdmissionFormStatus.Draft);
			formV2 = await _formAppService.PublishAsync(formV2.Id, formV2.ConcurrencyStamp);
			formV2.Status.ShouldBe(AdmissionFormStatus.Published);
			await WithUnitOfWorkAsync(async () =>
			{
				var currentOffering = await _offeringRepository.GetAsync(offering.Id);
				currentOffering.ConfigureReferences(currentOffering.WorkflowDefinitionId, currentOffering.InquiryFormDefinitionId, formV2.Id);
				await _offeringRepository.UpdateAsync(currentOffering, autoSave: true);
			});

			var diya = await CreateApplicantAsync("Diya");
			var diyaApplication = await StartFixtureApplicationAsync(diya.Id, offering.Id);
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
			var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
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

			var application = await StartFixtureApplicationAsync(aarav.Id, offering.Id);
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

	[Fact]
	public async Task Inquiry_existing_family_requires_matching_primary_guardian_and_accessible_admission_record()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Inquiry family access test"),
				autoSave: true);
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var inquiryService = GetRequiredService<IAdmissionInquiryAppService>();
			CreateAdmissionInquiryDto Input(string applicantName, string mobile, Guid? familyId = null) => new()
			{
				AdmissionCycleId = offering.AdmissionCycleId,
				AdmissionOfferingId = offering.Id,
				BoardId = offering.BoardId,
				MediumId = offering.MediumId,
				StandardId = offering.StandardId,
				ApplicantName = applicantName,
				ApplicantFirstName = applicantName,
				GuardianFirstName = "Meera",
				GuardianPhone = mobile,
				Phone = mobile,
				Source = AdmissionInquirySource.Manual,
				ExistingFamilyId = familyId,
				FormValues =
				[
					new() { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = JsonSerializer.Serialize(applicantName) },
					new() { FieldCode = "APPLICANT_DOB", ValueJson = "\"2017-05-15\"" },
					new() { FieldCode = "APPLICANT_GENDER", ValueJson = "\"Female\"" },
					new() { FieldCode = "GUARDIAN_MOBILE", ValueJson = JsonSerializer.Serialize(mobile) }
				]
			};

			var first = await inquiryService.CreateAsync(Input("Asha", "9876543210"));
			var matches = await inquiryService.DuplicateCheckAsync(new AdmissionInquiryDuplicateCheckInput
			{
				AdmissionCycleId = offering.AdmissionCycleId,
				GuardianPhone = "9876543210",
				ApplicantName = "Asha"
			});
			matches.ShouldContain(match => match.FamilyId == first.FamilyId);
			matches.Single(match => match.FamilyId == first.FamilyId).CanLinkFamily.ShouldBeTrue();
			var nameOnlyMatches = await inquiryService.DuplicateCheckAsync(new AdmissionInquiryDuplicateCheckInput
			{
				AdmissionCycleId = offering.AdmissionCycleId,
				GuardianPhone = "9876543211",
				ApplicantName = "Asha"
			});
			nameOnlyMatches.Single(match => match.FamilyId == first.FamilyId).CanLinkFamily.ShouldBeFalse();
			var sibling = await inquiryService.CreateAsync(Input("Anya", "9876543210", first.FamilyId));
			sibling.FamilyId.ShouldBe(first.FamilyId);

			var wrongPhone = await Should.ThrowAsync<BusinessException>(async () =>
				await inquiryService.CreateAsync(Input("Wrong", "9876543211", first.FamilyId)));
			wrongPhone.Code.ShouldBe(AdmissionErrorCodes.InquiryFamilyMismatch);

			var unlinkedFamily = new AdmissionFamily(Guid.NewGuid(), TestTenantId, $"FAM-{Guid.NewGuid():N}");
			var unlinkedGuardian = new AdmissionGuardian(Guid.NewGuid(), TestTenantId, unlinkedFamily.Id,
				"Other", "Parent", "9123456789", null, AdmissionGuardianRelationship.Parent, true);
			unlinkedFamily.SetPrimaryGuardian(unlinkedGuardian.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				await _familyRepository.InsertAsync(unlinkedFamily, autoSave: true);
				await _guardianRepository.InsertAsync(unlinkedGuardian, autoSave: true);
			});
			var unlinked = await Should.ThrowAsync<BusinessException>(async () =>
				await inquiryService.CreateAsync(Input("Unknown", "9123456789", unlinkedFamily.Id)));
			unlinked.Code.ShouldBe(AdmissionErrorCodes.InquiryFamilyMismatch);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Rte_direct_registration_starts_at_form_and_reuses_family_without_inquiry(bool withOptionalOffer)
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Direct registration test"),
				autoSave: true);
			var draft = await CreateDirectWorkflowDraftAsync();
			var offering = await CreateAvailableOfferingAsync(draft.Id, assertValid: false);
			var workflowInput = ToUpdateInput(draft);
			workflowInput.Stages.Single(stage => stage.StageType == AdmissionWorkflowStageType.Form)
				.ConfigurationJson = JsonSerializer.Serialize(new
				{
					formDefinitionId = offering.ApplicationFormDefinitionId,
					form = new { version = 1, enabled = true, responsibility = "staff" }
				});
			var updatedWorkflow = await _workflowAppService.UpdateAsync(draft.Id, workflowInput);
			var workflow = await _workflowAppService.PublishAsync(updatedWorkflow.Id, updatedWorkflow.ConcurrencyStamp);
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				stored.UpdateRouting(AdmissionTrack.Rte, AdmissionEntryMode.DirectRegistration, identityLocked: false);
				stored.ConfigureReferences(workflow.Id, null, stored.ApplicationFormDefinitionId);
				await _offeringRepository.UpdateAsync(stored, autoSave: true);
			});
			var validation = await WithUnitOfWorkAsync(async () =>
				await _offeringValidator.ValidateStoredAsync(await _offeringRepository.GetAsync(offering.Id)));
			validation.Errors.ShouldBeEmpty(string.Join(" | ", validation.Errors.Select(error => error.Message)));
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				stored.UpdateRouting(AdmissionTrack.Rte, AdmissionEntryMode.Inquiry, identityLocked: false);
				var invalid = await _offeringValidator.ValidateStoredAsync(stored);
				invalid.Errors.ShouldContain(error => error.Code == AdmissionOfferingValidationCodes.RteRequiresDirectRegistration);
				AdmissionOfferingConfigurationValidator.CanSave(invalid, isActive: false).ShouldBeFalse();
				stored.UpdateRouting(AdmissionTrack.Rte, AdmissionEntryMode.DirectRegistration, identityLocked: false);
			});
			await WithUnitOfWorkAsync(async () =>
			{
				var stageRepository = GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>();
				var assessment = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.Id,
					"ENTRANCE", "Entrance assessment", AdmissionWorkflowStageType.Assessment, 2,
					true, false, null, null);
				await stageRepository.InsertAsync(assessment, autoSave: true);
				var invalid = await _offeringValidator.ValidateStoredAsync(await _offeringRepository.GetAsync(offering.Id));
				invalid.Errors.ShouldContain(error => error.Code == AdmissionOfferingValidationCodes.RteWorkflowContainsAssessment);
				await stageRepository.DeleteAsync(assessment, autoSave: true);
			});
			(await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<IAdmissionInquiryAppService>().CreateAsync(ManagedInquiryInput(offering))))
				.Code.ShouldBe(AdmissionErrorCodes.InquiryEntryNotAllowed);

			var input = new StartAdmissionDirectRegistrationDto
			{
				RequestId = Guid.NewGuid(), AdmissionOfferingId = offering.Id, CounsellorId = TestUserId,
				ApplicantFirstName = "Asha", ApplicantLastName = "Patel",
				ApplicantDateOfBirth = new DateTime(2018, 5, 15), ApplicantGender = "Female",
				GuardianFirstName = "Ravi", GuardianLastName = "Patel",
				GuardianMobile = "9876543210", GuardianRelationship = AdmissionGuardianRelationship.Parent
			};
			var first = await _applicationAppService.StartDirectRegistrationAsync(input);
			first.AssignedCounsellorId.ShouldBe(TestUserId);
			(await _applicationRepository.GetAsync(first.Id)).InquiryId.ShouldBeNull();
			var reservedStudentId = (await _applicantRepository.GetAsync(first.ApplicantId)).ReservedStudentId;
			reservedStudentId.ShouldNotBe(Guid.Empty);
			var runtime = await _applicationAppService.GetWorkflowAsync(first.Id);
			runtime.Stages.Single(stage => stage.StageType == AdmissionWorkflowStageType.Form)
				.Status.ShouldBe(AdmissionRuntimeStageStatus.Active);
			(await _applicationAppService.StartDirectRegistrationAsync(input)).Id.ShouldBe(first.Id);
			(await _applicantRepository.GetAsync(first.ApplicantId)).ReservedStudentId.ShouldBe(reservedStudentId);
			(await Should.ThrowAsync<BusinessException>(() =>
				_applicationAppService.StartDirectRegistrationAsync(new StartAdmissionDirectRegistrationDto
				{
					RequestId = input.RequestId, AdmissionOfferingId = offering.Id,
					ApplicantFirstName = input.ApplicantFirstName, ApplicantLastName = input.ApplicantLastName,
					ApplicantDateOfBirth = input.ApplicantDateOfBirth, ApplicantGender = input.ApplicantGender,
					GuardianFirstName = input.GuardianFirstName, GuardianLastName = input.GuardianLastName,
					GuardianMobile = input.GuardianMobile, GuardianRelationship = input.GuardianRelationship
				}))).Code.ShouldBe(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict);

			var matches = await _applicationAppService.FindDirectRegistrationFamiliesAsync(
				offering.Id, input.GuardianMobile);
			matches.Count.ShouldBe(1);
			matches[0].Applicants.ShouldContain("Asha Patel");
			var second = await _applicationAppService.StartDirectRegistrationAsync(new StartAdmissionDirectRegistrationDto
			{
				RequestId = Guid.NewGuid(), AdmissionOfferingId = offering.Id,
				ExistingFamilyId = matches[0].FamilyId,
				ApplicantFirstName = "Anya", ApplicantLastName = "Patel",
				ApplicantDateOfBirth = new DateTime(2020, 6, 1), ApplicantGender = "Female",
				GuardianFirstName = "Ravi", GuardianLastName = "Patel",
				GuardianMobile = input.GuardianMobile,
				GuardianRelationship = AdmissionGuardianRelationship.Parent
			});
			(await _applicantRepository.GetAsync(second.ApplicantId)).FamilyId.ShouldBe(matches[0].FamilyId);

			var beforeApproval = await _applicationAppService.GetFinalizationPreviewAsync(first.Id);
			beforeApproval.Items.Single(item => item.Key == "offer").Passed.ShouldBeFalse();
			beforeApproval.Items.Single(item => item.Key == "offer").Label.ShouldBe("Direct admission approved");
			var submitted = await _applicationAppService.SubmitAsync(first.Id);
			submitted.ValidationErrors.ShouldBeEmpty();
			var current = await _applicationAppService.GetAsync(first.Id);
			current = await _applicationAppService.ApproveAsync(first.Id, new AdmissionApplicationDecisionDto
			{
				ConcurrencyStamp = current.ConcurrencyStamp,
				Reason = "RTE eligibility verified"
			});
			var approvedPreview = await _applicationAppService.GetFinalizationPreviewAsync(first.Id);
			approvedPreview.Items.Single(item => item.Key == "offer").Passed.ShouldBeTrue();
			current.Status.ShouldBe(AdmissionApplicationStatus.Approved);
			var workflowRuntime = await _applicationAppService.GetWorkflowAsync(first.Id);
			while (workflowRuntime.CurrentStageInstanceId.HasValue)
			{
				workflowRuntime = await _applicationAppService.AdvanceWorkflowAsync(first.Id,
					new AdvanceAdmissionWorkflowDto
					{
						ExpectedCurrentStageInstanceId = workflowRuntime.CurrentStageInstanceId.Value,
						IdempotencyKey = $"rte-complete-{workflowRuntime.CurrentStageInstanceId.Value:N}",
						ConcurrencyStamp = (await _applicationAppService.GetAsync(first.Id)).ConcurrencyStamp
					});
			}
			workflowRuntime.Status.ShouldBe(AdmissionWorkflowInstanceStatus.Completed);
			if (withOptionalOffer)
			{
				current = await _applicationAppService.GetAsync(first.Id);
				current = await _applicationAppService.IssueOfferAsync(first.Id, new AdmissionApplicationDecisionDto
				{
					ConcurrencyStamp = current.ConcurrencyStamp,
					OfferExpiresAt = DateTime.UtcNow.AddDays(7)
				});
				var pendingOffer = await _applicationAppService.GetFinalizationPreviewAsync(first.Id);
				pendingOffer.Items.Single(item => item.Key == "offer").Passed.ShouldBeFalse();
				current = await _applicationAppService.AcceptOfferAsync(first.Id, new AdmissionApplicationDecisionDto
				{
					ConcurrencyStamp = current.ConcurrencyStamp
				});
				current.Status.ShouldBe(AdmissionApplicationStatus.OfferAccepted);
			}
			var ready = await _applicationAppService.GetFinalizationPreviewAsync(first.Id);
			ready.IsReady.ShouldBeTrue(string.Join(" | ", ready.Items.Where(item => !item.Passed)
				.Select(item => item.Detail)));
			var finalized = await _applicationAppService.FinalizeAsync(first.Id, new FinalizeAdmissionApplicationDto
			{
				ConcurrencyStamp = (await _applicationAppService.GetAsync(first.Id)).ConcurrencyStamp,
				IdempotencyKey = $"rte-finalize-{first.Id:N}",
				ConfirmCreateNew = true,
				ConfirmGuardianContactVerified = true
			});
			finalized.Application.Status.ShouldBe(AdmissionApplicationStatus.AdmissionFinalized);
			var onboarding = await _onboardingCaseRepository.GetAsync(finalized.OnboardingCaseId);
			onboarding.ApplicationId.ShouldBe(first.Id);
			onboarding.AssignedToUserId.ShouldBe(TestUserId);
			var generalWorkflow = await CreateAndPublishWorkflowAsync();
			var generalOffering = await WithUnitOfWorkAsync(async () =>
			{
				var parallel = new AdmissionOffering(Guid.NewGuid(), TestTenantId, offering.AdmissionCycleId,
					offering.CampusId, offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId,
					$"GENERAL_{Guid.NewGuid():N}"[..20], true, null, null);
				parallel.ConfigureReferences(generalWorkflow.Id, offering.InquiryFormDefinitionId,
					offering.ApplicationFormDefinitionId);
				await _offeringRepository.InsertAsync(parallel, autoSave: true);
				var check = await _offeringValidator.ValidateStoredAsync(parallel);
				check.Errors.ShouldBeEmpty(string.Join(" | ", check.Errors.Select(error => error.Message)));
				return parallel;
			});
			var nextCycleOffering = await CreateAvailableOfferingAsync(generalWorkflow.Id);
			var inquiryService = GetRequiredService<IAdmissionInquiryAppService>();
			var historicalMatches = await inquiryService.DuplicateCheckAsync(
				new AdmissionInquiryDuplicateCheckInput
				{
					AdmissionCycleId = nextCycleOffering.AdmissionCycleId,
					GuardianPhone = input.GuardianMobile,
					ApplicantName = "Asha"
				});
			var historicalFamily = historicalMatches.First(match => match.FamilyId == matches[0].FamilyId);
			historicalMatches.Count(match => match.FamilyId == matches[0].FamilyId).ShouldBe(2);
			historicalFamily.CanLinkFamily.ShouldBeTrue();
			historicalFamily.ExistingInquiryId.ShouldBeNull();
			var nameOnlyMatches = await inquiryService.DuplicateCheckAsync(
				new AdmissionInquiryDuplicateCheckInput
				{
					AdmissionCycleId = nextCycleOffering.AdmissionCycleId,
					GuardianPhone = "9876543211",
					ApplicantName = "Asha"
				});
			nameOnlyMatches.ShouldBeEmpty();
			await Should.ThrowAsync<EntityNotFoundException>(() => inquiryService.DuplicateCheckAsync(
				new AdmissionInquiryDuplicateCheckInput
				{
					AdmissionCycleId = Guid.NewGuid(),
					GuardianPhone = input.GuardianMobile
				}));
			var outOfScopeUserId = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(outOfScopeUserId, $"out-of-scope-{outOfScopeUserId:N}");
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(
			[
				new Claim(AbpClaimTypes.UserId, outOfScopeUserId.ToString()),
				new Claim(AbpClaimTypes.UserName, $"out-of-scope-{outOfScopeUserId:N}")
			], "Test"))))
			{
				await Should.ThrowAsync<EntityNotFoundException>(() => inquiryService.DuplicateCheckAsync(
					new AdmissionInquiryDuplicateCheckInput
					{
						AdmissionCycleId = nextCycleOffering.AdmissionCycleId,
						GuardianPhone = input.GuardianMobile
					}));
			}
			var otherClassOffering = await WithUnitOfWorkAsync(async () =>
			{
				var standard = await _standardManager.CreateAsync(nextCycleOffering.StageId,
					$"Other class {Guid.NewGuid():N}"[..30], 11, null);
				await _standardRepository.InsertAsync(standard, autoSave: true);
				var otherClass = new AdmissionOffering(Guid.NewGuid(), TestTenantId,
					nextCycleOffering.AdmissionCycleId, null, nextCycleOffering.BoardId,
					nextCycleOffering.MediumId, nextCycleOffering.StageId, standard.Id,
					$"OTHER_{Guid.NewGuid():N}"[..20], true, null, null);
				otherClass.ConfigureReferences(generalWorkflow.Id,
					nextCycleOffering.InquiryFormDefinitionId,
					nextCycleOffering.ApplicationFormDefinitionId);
				await _offeringRepository.InsertAsync(otherClass, autoSave: true);
				return otherClass;
			});
			var duplicateUpdate = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<IAdmissionOfferingAppService>().UpdateAsync(otherClassOffering.Id,
					new UpdateAdmissionOfferingDto
					{
						Name = otherClassOffering.Name,
						Track = AdmissionTrack.General,
						EntryMode = AdmissionEntryMode.Inquiry,
						CampusId = nextCycleOffering.CampusId,
						BoardId = nextCycleOffering.BoardId,
						MediumId = nextCycleOffering.MediumId,
						StageId = nextCycleOffering.StageId,
						StandardId = nextCycleOffering.StandardId,
						Code = otherClassOffering.Code,
						UseCycleDates = true,
						WorkflowDefinitionId = generalWorkflow.Id,
						InquiryFormDefinitionId = nextCycleOffering.InquiryFormDefinitionId,
						ApplicationFormDefinitionId = nextCycleOffering.ApplicationFormDefinitionId,
						IsActive = otherClassOffering.IsActive,
						ConcurrencyStamp = otherClassOffering.ConcurrencyStamp
					}));
			duplicateUpdate.Code.ShouldBe(AdmissionErrorCodes.OfferingDuplicate);
			var limitedUserId = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(limitedUserId, $"limited-{limitedUserId:N}");
			var nextCycle = await _cycleRepository.GetAsync(nextCycleOffering.AdmissionCycleId);
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
				userId: limitedUserId, academicYearId: nextCycle.AcademicYearId,
				educationBoardId: otherClassOffering.BoardId,
				educationMediumId: otherClassOffering.MediumId,
				educationMediumStageId: otherClassOffering.StageId,
				standardId: otherClassOffering.StandardId,
				description: "Only another class can be accessed"), autoSave: true);
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(
			[
				new Claim(AbpClaimTypes.UserId, limitedUserId.ToString()),
				new Claim(AbpClaimTypes.UserName, $"limited-{limitedUserId:N}")
			], "Test"))))
			{
				var hidden = await inquiryService.DuplicateCheckAsync(
					new AdmissionInquiryDuplicateCheckInput
					{
						AdmissionCycleId = nextCycleOffering.AdmissionCycleId,
						GuardianPhone = input.GuardianMobile
					});
				hidden.ShouldBeEmpty();
			}
			var crossCycleInquiry = await inquiryService.CreateAsync(new CreateAdmissionInquiryDto
			{
				AdmissionCycleId = nextCycleOffering.AdmissionCycleId,
				AdmissionOfferingId = nextCycleOffering.Id,
				BoardId = nextCycleOffering.BoardId,
				MediumId = nextCycleOffering.MediumId,
				StandardId = nextCycleOffering.StandardId,
				ExistingFamilyId = historicalFamily.FamilyId,
				ApplicantName = "Ira Patel",
				ApplicantFirstName = "Ira",
				GuardianFirstName = "Ravi",
				GuardianPhone = input.GuardianMobile,
				Phone = input.GuardianMobile,
				Source = AdmissionInquirySource.Manual,
				FormValues =
				[
					new() { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"Ira\"" },
					new() { FieldCode = "APPLICANT_DOB", ValueJson = "\"2019-04-02\"" },
					new() { FieldCode = "APPLICANT_GENDER", ValueJson = "\"Female\"" },
					new() { FieldCode = "GUARDIAN_MOBILE", ValueJson = JsonSerializer.Serialize(input.GuardianMobile) }
				]
			});
			crossCycleInquiry.FamilyId.ShouldBe(historicalFamily.FamilyId);
			var duplicateProfiles = await WithUnitOfWorkAsync(async () =>
			{
				var profiles = GetRequiredService<IRepository<AdmissionIntakeProfile, Guid>>();
				var inquiryProfile = await profiles.InsertAsync(new AdmissionIntakeProfile(
					Guid.NewGuid(), TestTenantId, AdmissionIntakeProfileKind.Inquiry,
					$"INQUIRY_{Guid.NewGuid():N}", "Duplicate offering inquiry fixture",
					JsonSerializer.Serialize(new AdmissionInquiryIntakeRules())), autoSave: true);
				var assignmentProfile = await profiles.InsertAsync(new AdmissionIntakeProfile(
					Guid.NewGuid(), TestTenantId, AdmissionIntakeProfileKind.CounsellorAssignment,
					$"ASSIGN_{Guid.NewGuid():N}", "Duplicate offering assignment fixture",
					JsonSerializer.Serialize(new AdmissionCounsellorAssignmentRules
					{
						CounsellorUserIds = [TestUserId]
					})), autoSave: true);
				return (InquiryId: inquiryProfile.Id, AssignmentId: assignmentProfile.Id);
			});
			var duplicate = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<IAdmissionOfferingAppService>().CreateAsync(generalOffering.AdmissionCycleId,
					new CreateAdmissionOfferingDto
					{
						Name = "Duplicate General class",
						InquiryIntakeProfileId = duplicateProfiles.InquiryId,
						CounsellorAssignmentProfileId = duplicateProfiles.AssignmentId,
						Track = AdmissionTrack.General,
						EntryMode = AdmissionEntryMode.Inquiry,
						CampusId = generalOffering.CampusId,
						BoardId = generalOffering.BoardId,
						MediumId = generalOffering.MediumId,
						StageId = generalOffering.StageId,
						StandardId = generalOffering.StandardId,
						Code = $"DUPLICATE_{Guid.NewGuid():N}"[..20],
						UseCycleDates = true,
						WorkflowDefinitionId = generalWorkflow.Id,
						InquiryFormDefinitionId = generalOffering.InquiryFormDefinitionId,
						ApplicationFormDefinitionId = generalOffering.ApplicationFormDefinitionId
					}));
			duplicate.Code.ShouldBe(AdmissionErrorCodes.OfferingDuplicate);
			var room = await CreateCapacityRoomAsync(1);
			var capacityService = GetRequiredService<IAdmissionClassCapacityAppService>();
			var capacity = await capacityService.UpdateForOfferingAsync(offering.Id,
				new UpdateAdmissionClassRoomsDto { RoomIds = [room.Id] });
			capacity.Capacity.ShouldBe(1);
			capacity.OccupiedCount.ShouldBe(1);
			capacity.IsFull.ShouldBeTrue();
			var generalCapacity = await capacityService.GetForOfferingAsync(generalOffering.Id);
			generalCapacity.StandardId.ShouldBe(offering.StandardId);
			generalCapacity.RemainingSeats.ShouldBe(0);
			generalCapacity.Warning.ShouldNotBeNull();
			var generalApplicant = await CreateApplicantAsync("General");
			(await Should.ThrowAsync<BusinessException>(() => StartFixtureApplicationAsync(generalApplicant.Id, generalOffering.Id)))
				.Code.ShouldBe(AdmissionApplicationErrorCodes.OfferingNotAvailable);
		}
	}

	private async Task<AdmissionWorkflowDto> CreateDirectWorkflowDraftAsync()
	{
		var entryId = Guid.NewGuid();
		var terminalId = Guid.NewGuid();
		var workflow = await _workflowAppService.CreateAsync(new CreateUpdateAdmissionWorkflowDto
		{
			Name = "RTE direct registration",
			Code = $"RTE_{Guid.NewGuid():N}"[..24],
			Stages =
			[
				new CreateUpdateAdmissionWorkflowStageDto
				{
					Id = entryId, Code = "REGISTRATION", Name = "Registration",
					StageType = AdmissionWorkflowStageType.Form, DisplayOrder = 0,
					IsEntryStage = true, IsRequired = true
				},
				new CreateUpdateAdmissionWorkflowStageDto
				{
					Id = terminalId, Code = "ADMISSION", Name = "Admission",
					StageType = AdmissionWorkflowStageType.Custom, DisplayOrder = 1,
					IsTerminalStage = true, IsRequired = true
				}
			],
			Transitions =
			[
				new CreateUpdateAdmissionWorkflowTransitionDto
				{
					FromStageId = entryId, ToStageId = terminalId, Priority = 1, IsDefault = true
				}
			]
		});
		return workflow;
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

	private Task<AdmissionOffering> CreateAvailableOfferingAsync(
		Guid workflowId, bool assertValid = true, bool campusScoped = false) =>
		WithUnitOfWorkAsync(async () =>
	{
		var userRepository = GetRequiredService<IRepository<IdentityUser, Guid>>();
		if (!await userRepository.AnyAsync(user => user.Id == TestUserId))
			await userRepository.InsertAsync(new IdentityUser(
				TestUserId, $"workflow-admin-{TestUserId:N}", "workflow-admin@example.test", TestTenantId),
				autoSave: true);
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
		var inquiryForm = new AdmissionFormDefinition(
			Guid.NewGuid(), TestTenantId, "Grade 5 Inquiry Form",
			$"INQUIRY_{Guid.NewGuid():N}"[..24], AdmissionFormLayoutType.SinglePage,
			purpose: AdmissionFormPurpose.Inquiry);
		inquiryForm.Publish(Guid.NewGuid());
		await _formRepository.InsertAsync(inquiryForm, autoSave: true);
		var inquirySection = new AdmissionFormSectionDefinition(Guid.NewGuid(), inquiryForm.Id,
			"INQUIRY", "Inquiry details", 0, null);
		await _formSectionRepository.InsertAsync(inquirySection, autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), inquiryForm.Id,
			inquirySection.Id, "APPLICANT_FIRST_NAME", "Applicant first name", AdmissionFormFieldType.Text,
			AdmissionFormBindingType.System, "Applicant.FirstName", true, false, 0, null, null, null, null, null), autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), inquiryForm.Id,
			inquirySection.Id, "APPLICANT_DOB", "Applicant date of birth", AdmissionFormFieldType.Date,
			AdmissionFormBindingType.System, "Applicant.DateOfBirth", true, false, 1, null, null, null, null, null), autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), inquiryForm.Id,
			inquirySection.Id, "APPLICANT_GENDER", "Applicant gender", AdmissionFormFieldType.Select,
			AdmissionFormBindingType.System, "Applicant.Gender", true, false, 2, null, null, null, null, null,
			"[{\"value\":\"Male\",\"label\":\"Male\"},{\"value\":\"Female\",\"label\":\"Female\"}]"), autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), inquiryForm.Id,
			inquirySection.Id, "GUARDIAN_MOBILE", "Guardian mobile", AdmissionFormFieldType.Phone,
			AdmissionFormBindingType.System, "Guardian.Mobile", false, false, 3, null, null, null, null, null), autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), inquiryForm.Id,
			inquirySection.Id, "LEARNING_NOTES", "Learning notes", AdmissionFormFieldType.Textarea,
			AdmissionFormBindingType.Custom, "LearningNotes", false, false, 4, null, null, null, null, null), autoSave: true);
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
		Guid? campusId = null;
		if (campusScoped)
		{
			var campus = new AdmissionCampus(
				Guid.NewGuid(), TestTenantId, $"CERT_{Guid.NewGuid():N}"[..24], "Certification Campus");
			await _campusRepository.InsertAsync(campus, autoSave: true);
			campusId = campus.Id;
		}

		var offering = new AdmissionOffering(
			Guid.NewGuid(),
			TestTenantId,
			cycle.Id,
			campusId,
			board.Id,
			medium.Id,
			stage.Id,
			standard.Id,
			$"G5_{Guid.NewGuid():N}"[..20],
			useCycleDates: true,
			null,
			null);
		offering.ConfigureReferences(workflowId, inquiryForm.Id, form.Id);
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
			Guid.NewGuid(), TestTenantId, family.Id, firstName, null, "Patel", new DateTime(2016, 5, 14), "Male", null,
			Guid.NewGuid());
		await _applicantRepository.InsertAsync(applicant, autoSave: true);
		return applicant;
	}

	private Task EnsureTestIdentityUserAsync(Guid userId, string userName) => WithUnitOfWorkAsync(async () =>
	{
		var users = GetRequiredService<IRepository<IdentityUser, Guid>>();
		if (!await users.AnyAsync(user => user.Id == userId))
			await users.InsertAsync(new IdentityUser(userId, userName, $"{userId:N}@example.test", TestTenantId),
				autoSave: true);
	});

	private async Task<AdmissionApplicationDto> StartFixtureApplicationAsync(Guid applicantId, Guid offeringId)
	{
		var offering = await _offeringRepository.GetAsync(offeringId);
		if (offering.EntryMode != AdmissionEntryMode.Inquiry)
			return await _applicationAppService.StartAsync(applicantId, offeringId);
		var applicant = await _applicantRepository.GetAsync(applicantId);
		var inquiryId = await WithUnitOfWorkAsync(async () =>
		{
			var inquiry = new AdmissionInquiry(Guid.NewGuid(), TestTenantId,
				$"FIXTURE-{Guid.NewGuid():N}", offering.AdmissionCycleId, offeringId,
				$"{applicant.FirstName} {applicant.LastName}".Trim(), "9876543210", null,
				AdmissionInquirySource.Manual, "Integration fixture");
			inquiry.LinkIdentity(applicant.FamilyId, applicantId);
			inquiry.SetStatus(AdmissionInquiryStatus.Interested);
			await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().InsertAsync(inquiry, autoSave: true);
			return inquiry.Id;
		});
		return await _applicationAppService.StartAsync(applicantId, offeringId, inquiryId);
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
