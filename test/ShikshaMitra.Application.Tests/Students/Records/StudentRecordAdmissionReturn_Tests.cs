using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private async Task<(IStudentRecordTransitionAppService Service, Student Student, AdmissionOffering Offering)> AdmissionReturnFixtureAsync(AdmissionEntryMode mode, bool approval = false, bool masked = false)
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Transitions.AllowedReturnRoutes.Add("ResumeStage");
		policy.Changes["Status"].Mode = approval ? "Approval" : "Direct";
		if (approval) policy.Changes["Status"].Condition = new() { IsFormerStudent = true };
		if (masked) foreach (var group in new[] { "Identity", "Guardian", "Contact" }) policy.Access.Masks.Add(new() { FieldGroup = group, AllowedRoleNames = [] });
		var fixture = await TransitionFixtureAsync(policy); await fixture.Service.SubmitBatchAsync(fixture.Input);
		var existing = await WithUnitOfWorkAsync(async () => (await _offeringRepository.GetListAsync(x => x.StandardId == fixture.Student.StandardId)).First());
		var registrationWorkflow = mode is AdmissionEntryMode.DirectRegistration or AdmissionEntryMode.Both
			? await CreateAdmissionReturnRegistrationWorkflowAsync(existing.ApplicationFormDefinitionId!.Value) : null;
		var offering = await WithUnitOfWorkAsync(async () =>
		{
			var item = await _offeringRepository.GetAsync(existing.Id);
			item.UpdateRouting(AdmissionTrack.General, mode, false);
			if (registrationWorkflow != null) item.ConfigureReferences(registrationWorkflow.Id, item.InquiryFormDefinitionId, item.ApplicationFormDefinitionId);
			await _offeringRepository.UpdateAsync(item, true);
			var validation = await _offeringValidator.ValidateStoredAsync(item);
			validation.IsValid.ShouldBeTrue(string.Join(", ", validation.Errors.Select(x => x.Code)));
			return item;
		});
		return (fixture.Service, fixture.Student, offering);
	}
	private async Task<AdmissionWorkflowDto> CreateAdmissionReturnRegistrationWorkflowAsync(Guid formId)
	{
		var formStage = Guid.NewGuid(); var terminalStage = Guid.NewGuid();
		var workflow = await _workflowAppService.CreateAsync(new CreateUpdateAdmissionWorkflowDto
		{
			Name = "Returning registration", Code = $"RETURN_FORM_{Guid.NewGuid():N}"[..24],
			Stages =
			[
				new() { Id = formStage, Code = "FORM", Name = "Registration form", StageType = AdmissionWorkflowStageType.Form,
					DisplayOrder = 0, IsEntryStage = true, IsRequired = true,
					ConfigurationJson = JsonSerializer.Serialize(new { formDefinitionId = formId, form = new AdmissionFormStagePolicy
					{ Version = 1, Enabled = true, Responsibility = "shared", AllowSaveAndResume = true, AllowLateSubmission = true,
						AutoAdvanceOnSubmit = true, RequireSectionVerification = false } }) },
				new() { Id = terminalStage, Code = "DONE", Name = "Ready for review", StageType = AdmissionWorkflowStageType.Custom,
					DisplayOrder = 1, IsTerminalStage = true, IsRequired = true }
			],
			Transitions = [new() { FromStageId = formStage, ToStageId = terminalStage, Priority = 1, IsDefault = true }]
		});
		return await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
	}
	private static StudentRecordReturnInput FreshAdmissionReturn(Student student, AdmissionOffering offering) => new()
	{
		StudentId = student.Id, TargetAcademicYearId = student.AcademicYearId!.Value, AdmissionOfferingId = offering.Id,
		Route = "FreshApplication", Reason = "School reviewed the returning family's request", IdempotencyKey = "admission-return-" + Guid.NewGuid().ToString("N"),
		Inquiry = new() { Source = AdmissionInquirySource.WalkIn, SourceDetail = "Records reviewed return" }
	};
	private async Task<Guid> CreateAdmissionReturnReviewerAsync(bool administrator = true)
	{
		var actor = Guid.NewGuid(); await EnsureTestIdentityUserAsync(actor, $"return-reviewer-{actor:N}");
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: actor), true);
			if (administrator)
			{
				var users = GetRequiredService<IdentityUserManager>();
				(await users.AddToRoleAsync((await users.FindByIdAsync(actor.ToString()))!, "admin")).Succeeded.ShouldBeTrue();
			}
		});
		return actor;
	}
	private async Task<AdmissionApplicationDto> ReadyAdmissionReturnApplicationAsync(Guid applicationId)
	{
		(await _applicationAppService.SubmitAsync(applicationId)).ValidationErrors.ShouldBeEmpty();
		await CompleteCertificationWorkflowAsync(applicationId, stopBeforeApproval: true);
		var application = await _applicationAppService.GetAsync(applicationId);
		application = await _applicationAppService.MarkInReviewAsync(application.Id, Decision(application, "Returning application reviewed"));
		application = await _applicationAppService.ApproveAsync(application.Id, Decision(application, "Returning admission approved"));
		await CompleteCertificationWorkflowAsync(applicationId);
		application = await _applicationAppService.GetAsync(applicationId);
		application = await _applicationAppService.IssueOfferAsync(application.Id, Decision(application, "Returning offer issued", DateTime.UtcNow.AddDays(30)));
		return await _applicationAppService.AcceptOfferAsync(application.Id, Decision(application, "Returning family accepted"));
	}
	[Fact] public Task Record_admission_return_inquiry_is_new_and_idempotent_and_retains_completed_history() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await AdmissionReturnFixtureAsync(AdmissionEntryMode.Inquiry);
		var input = FreshAdmissionReturn(fixture.Student, fixture.Offering);
		var existingApplicants = await WithUnitOfWorkAsync(() => _applicantRepository.CountAsync(x => x.ExistingStudentId == fixture.Student.Id));
		var result = await fixture.Service.ReturnAsync(input);
		result.Status.ShouldBe("AwaitingAdmission"); result.AdmissionInquiryId.ShouldNotBeNull(); result.AdmissionApplicationId.ShouldBeNull();
		(await fixture.Service.ReturnAsync(input)).AdmissionInquiryId.ShouldBe(result.AdmissionInquiryId);
		await WithUnitOfWorkAsync(async () =>
		{
			var inquiry = await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(result.AdmissionInquiryId!.Value);
			inquiry.Status.ShouldBe(AdmissionInquiryStatus.New); inquiry.ContactedAt.ShouldBeNull();
			var applicant = await _applicantRepository.GetAsync(inquiry.ApplicantId!.Value); applicant.ExistingStudentId.ShouldBe(fixture.Student.Id);
			(await _applicantRepository.CountAsync(x => x.ExistingStudentId == fixture.Student.Id)).ShouldBe(existingApplicants + 1);
			(await _studentEnrollmentRepository.CountAsync(x => x.StudentId == fixture.Student.Id)).ShouldBe(1);
			(await _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.Student.Id)).IsActive.ShouldBeFalse();
			(await GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().CountAsync(x => x.StudentId == fixture.Student.Id && x.Status == StudentOnboardingStatus.Completed)).ShouldBe(1);
		});
		input.Reason = "Changed retry request";
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.ReturnAsync(input))).Code.ShouldBe("Students:Records:IdempotencyConflict");
	});
	[Fact] public Task Record_admission_return_both_requires_explicit_real_entry_path() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await AdmissionReturnFixtureAsync(AdmissionEntryMode.Both); var input = FreshAdmissionReturn(fixture.Student, fixture.Offering);
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.ReturnAsync(input))).Code.ShouldBe("Students:Records:ReturnEntryPathRequired");
		input.EntryPath = AdmissionEntryMode.Both;
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.ReturnAsync(input))).Code.ShouldBe("Students:Records:ReturnEntryPathRequired");
		input.EntryPath = AdmissionEntryMode.DirectRegistration;
		var result = await fixture.Service.ReturnAsync(input); result.Status.ShouldBe("AwaitingAdmission"); result.AdmissionInquiryId.ShouldBeNull(); result.AdmissionApplicationId.ShouldNotBeNull();
		await WithUnitOfWorkAsync(async () =>
		{
			var application = await _applicationRepository.GetAsync(result.AdmissionApplicationId!.Value);
			var applicant = await _applicantRepository.GetAsync(application.ApplicantId); applicant.ExistingStudentId.ShouldBe(fixture.Student.Id);
			applicant.Id.ShouldNotBe((await GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().GetAsync(x => x.StudentId == fixture.Student.Id && x.Status == StudentOnboardingStatus.Completed)).ApplicantId);
		});
	});
	[Fact] public Task Record_admission_return_keeps_intake_closed_until_records_review_and_masks_bound_identity() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await AdmissionReturnFixtureAsync(AdmissionEntryMode.Inquiry, approval: true, masked: true);
		var input = FreshAdmissionReturn(fixture.Student, fixture.Offering);
		var inquiries = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionInquiry, Guid>>().CountAsync());
		var result = await fixture.Service.ReturnAsync(input); result.Status.ShouldBe("Submitted"); result.AdmissionInquiryId.ShouldBeNull();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionInquiry, Guid>>().CountAsync())).ShouldBe(inquiries);
		var options = await fixture.Service.GetOptionsAsync(fixture.Student.AcademicYearId, fixture.Student.Id, fixture.Offering.Id);
		var intake = options.Offerings.Single(x => x.Id == fixture.Offering.Id).InquiryIntake!;
		intake.ServerBoundFieldCodes.ShouldContain("APPLICANT_FIRST_NAME"); intake.Form!.Sections.SelectMany(x => x.Fields).Single(x => x.Code == "APPLICANT_FIRST_NAME").ReadOnly.ShouldBeTrue();
		intake.BoundValues.ShouldBeEmpty();
	});
	[Fact] public Task Record_admission_return_activation_reopens_original_row_without_replacing_origin_or_gr() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await AdmissionReturnFixtureAsync(AdmissionEntryMode.DirectRegistration);
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetAsync(fixture.Student.Id); student.AssignGrNumber("PERMANENT-GR-17", 17); await _studentRepository.UpdateAsync(student, true);
			var origin = await GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().GetAsync(x => x.StudentId == student.Id && x.Status == StudentOnboardingStatus.Completed);
			var enrollment = await _studentEnrollmentRepository.GetAsync(x => x.StudentId == student.Id); enrollment.LinkOnboardingCase(origin.Id); await _studentEnrollmentRepository.UpdateAsync(enrollment, true);
		});
		var result = await fixture.Service.ReturnAsync(FreshAdmissionReturn(fixture.Student, fixture.Offering));
		await WithUnitOfWorkAsync(async () =>
		{
			var source = await _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.Student.Id); var origin = source.OnboardingCaseId;
			var application = await _applicationRepository.GetAsync(result.AdmissionApplicationId!.Value);
			var division = new Division(Guid.NewGuid(), TestTenantId, "Returned A", fixture.Offering.StandardId, 30, "Return test division");
			await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
			var item = new StudentOnboardingCase(Guid.NewGuid(), TestTenantId, application.Id, application.ApplicantId, fixture.Student.Id, fixture.Offering.Id,
				fixture.Student.AcademicYearId!.Value, fixture.Offering.BoardId, fixture.Offering.MediumId, fixture.Offering.StageId, fixture.Offering.StandardId, "reviewed-return-case", DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
			item.ExtraProperties["RecordReturnTransitionId"] = result.TransitionId!.Value.ToString("D");
			item.RecordPlacement(division.Id, Guid.NewGuid(), DateTime.UtcNow); item.RecordIdentifier(null, 29, DateTime.UtcNow);
			var enrollment = await GetRequiredService<StudentRecordAdmissionReturnService>().ActivateEnrollmentAsync(item);
			enrollment.Id.ShouldBe(source.Id); enrollment.OnboardingCaseId.ShouldBe(origin); enrollment.IsActive.ShouldBeTrue(); enrollment.RollNumber.ShouldBe(29);
			(await _studentRepository.GetAsync(fixture.Student.Id)).GrNumber.ShouldBe("PERMANENT-GR-17");
			(await _studentEnrollmentRepository.CountAsync(x => x.StudentId == fixture.Student.Id)).ShouldBe(1);
			var student = await _studentRepository.GetAsync(fixture.Student.Id);
			student.UpdateAcademicPlacement(item.AcademicYearId, item.IntendedBoardId, item.IntendedMediumId, item.IntendedStageId, item.IntendedStandardId, item.DivisionId);
			student.AssignRollNo(item.RollNumber); student.ChangeOperationalStatus(StudentStatus.Active, "Reviewed return test activated"); await _studentRepository.UpdateAsync(student);
			item.MarkReady(DateTime.UtcNow); item.BeginProvisioning(enrollment.Id); item.CompleteProvisioning(DateTime.UtcNow);
			await GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().InsertAsync(item);
			await GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>().Current!.SaveChangesAsync();
			var returnService = GetRequiredService<StudentRecordAdmissionReturnService>();
			await returnService.CompleteAsync(item); await returnService.CompleteAsync(item); await returnService.ValidateOnboardingAsync(item, false);
			await GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>().Current!.SaveChangesAsync();
			(await GetRequiredService<IRepository<StudentRecordTransition, Guid>>().GetAsync(result.TransitionId.Value)).Status.ShouldBe("Applied");
			(await GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>().CountAsync(x => x.TransitionId == result.TransitionId && x.Action == "Applied")).ShouldBe(1);
		});
	});
	[Fact] public Task Record_admission_return_records_approval_opens_intake_and_revoked_reviewer_blocks_finalization() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await AdmissionReturnFixtureAsync(AdmissionEntryMode.DirectRegistration, approval: true);
		var input = FreshAdmissionReturn(fixture.Student, fixture.Offering);
		var result = await fixture.Service.ReturnAsync(input);
		var transition = await fixture.Service.GetAsync(result.TransitionId!.Value); transition.Status.ShouldBe("Submitted"); transition.AdmissionApplicationId.ShouldBeNull();
		await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(() => fixture.Service.ApproveAsync(transition.Id,
			new() { ConcurrencyStamp = transition.ConcurrencyStamp, Remarks = "Requester cannot review their own return" }));
		var unqualified = await CreateAdmissionReturnReviewerAsync(administrator: false);
		using (_principalAccessor.Change(ProfilePrincipal(unqualified)))
			await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(() => fixture.Service.ApproveAsync(transition.Id,
				new() { ConcurrencyStamp = transition.ConcurrencyStamp, Remarks = "No current database approver role" }));
		var reviewer = await CreateAdmissionReturnReviewerAsync();
		using (_principalAccessor.Change(ProfilePrincipal(reviewer)))
			transition = await fixture.Service.ApproveAsync(transition.Id, new() { ConcurrencyStamp = transition.ConcurrencyStamp, Remarks = "Verified original school register" });
		transition.Status.ShouldBe("AwaitingAdmission"); transition.AdmissionApplicationId.ShouldNotBeNull();
		(await fixture.Service.ReturnAsync(input)).AdmissionApplicationId.ShouldBe(transition.AdmissionApplicationId);
		var application = await ReadyAdmissionReturnApplicationAsync(transition.AdmissionApplicationId!.Value);
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IdentityUserManager>();
			(await users.RemoveFromRoleAsync((await users.FindByIdAsync(reviewer.ToString()))!, "admin")).Succeeded.ShouldBeTrue();
		});
		await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(() => FinalizeAsync(application, false, "revoked-return-reviewer"));
		(await WithUnitOfWorkAsync(() => _onboardingCaseRepository.CountAsync(x => x.StudentId == fixture.Student.Id))).ShouldBe(1);
		(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(fixture.Student.Id))).Status.ShouldBe(StudentLifeCycleStatusEnum.Left);
	});
	[Fact] public Task Record_direct_return_finalizes_activates_and_completes_once_with_permanent_profile_and_history() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await AdmissionReturnFixtureAsync(AdmissionEntryMode.DirectRegistration);
		var original = await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetAsync(fixture.Student.Id); student.AssignGrNumber("PERMANENT-RETURN-GR", 42);
			student.ExtraProperties["AdmissionStudentField.Medical.BloodGroup"] = "A+"; await _studentRepository.UpdateAsync(student, true);
			var completed = await _onboardingCaseRepository.GetAsync(x => x.StudentId == student.Id && x.Status == StudentOnboardingStatus.Completed);
			var enrollment = await _studentEnrollmentRepository.GetAsync(x => x.StudentId == student.Id); enrollment.LinkOnboardingCase(completed.Id); await _studentEnrollmentRepository.UpdateAsync(enrollment, true);
			var family = await _familyRepository.GetAsync((await _applicantRepository.GetAsync(completed.ApplicantId)).FamilyId);
			var guardian = await _guardianRepository.GetAsync(family.PrimaryGuardianId!.Value);
			var sibling = new AdmissionApplicant(Guid.NewGuid(), TestTenantId, family.Id, "Sibling", null, "Student", CertificationBirthDate, "Female", null, Guid.NewGuid());
			await _applicantRepository.InsertAsync(sibling, true);
			var section = (await _formSectionRepository.GetListAsync(x => x.FormDefinitionId == fixture.Offering.ApplicationFormDefinitionId)).Single();
			await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), fixture.Offering.ApplicationFormDefinitionId!.Value, section.Id,
				"BLOOD_GROUP", "Blood group", AdmissionFormFieldType.Select, AdmissionFormBindingType.System, "Medical.BloodGroup", false, false, 6,
				null, null, null, null, null, AdmissionAdditionalStudentFieldCatalog.Metadata["Medical.BloodGroup"].OptionsJson), true);
			return (Case: completed, Enrollment: enrollment, Guardian: guardian, Family: family, Sibling: sibling);
		});
		var onboarding = GetRequiredService<IStudentOnboardingAppService>();
		await onboarding.CreatePolicyAsync(new() { AcademicYearId = fixture.Student.AcademicYearId!.Value, Name = "Reviewed return completion",
			RequireDocuments = false, RequireFamilyDetails = false, RequireCommunicationDetails = false, RequireStudentAccount = false, RequireGuardianAccount = false });
		var returned = await fixture.Service.ReturnAsync(FreshAdmissionReturn(fixture.Student, fixture.Offering));
		var draft = await _applicationAppService.GetAsync(returned.AdmissionApplicationId!.Value);
		await _applicationAppService.UpdateFormValuesAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp, Values =
			[new() { FieldCode = "GUARDIAN_MOBILE", ValueJson = "\"9999999999\"" }, new() { FieldCode = "BLOOD_GROUP", ValueJson = "\"O+\"" }] });
		(await _applicationAppService.GetFormAsync(draft.Id)).Values.ShouldContain(x => x.FieldCode == "GUARDIAN_MOBILE" && x.ValueJson == "\"9999999999\"");
		var application = await ReadyAdmissionReturnApplicationAsync(draft.Id);
		var finalized = await FinalizeAsync(application, false, "reviewed-direct-return-finalization");
		finalized.StudentId.ShouldBe(fixture.Student.Id); finalized.ReusedExistingStudent.ShouldBeTrue(); finalized.OnboardingCaseId.ShouldNotBe(original.Case.Id);
		var item = await onboarding.GetAsync(finalized.OnboardingCaseId);
		item.AdmissionRoute.ShouldBe("Returning student");
		(await WithUnitOfWorkAsync(() => _onboardingCaseRepository.GetAsync(item.Id))).ExtraProperties["RecordReturnTransitionId"].ToString().ShouldBe(returned.TransitionId.Value.ToString("D"));
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetAsync(fixture.Student.Id); student.Status.ShouldBe(StudentLifeCycleStatusEnum.Left);
			student.ExtraProperties["AdmissionStudentField.Medical.BloodGroup"].ToString().ShouldBe("A+"); student.GrNumber.ShouldBe("PERMANENT-RETURN-GR");
			var guardian = await _guardianRepository.GetAsync(original.Guardian.Id); guardian.Mobile.ShouldBe(original.Guardian.Mobile);
			(await _familyRepository.GetAsync(original.Family.Id)).PrimaryGuardianId.ShouldBe(original.Guardian.Id);
			(await _applicantRepository.GetAsync(original.Sibling.Id)).FamilyId.ShouldBe(original.Family.Id);
			(await _onboardingCaseRepository.GetAsync(original.Case.Id)).ConcurrencyStamp.ShouldBe(original.Case.ConcurrencyStamp);
		});
		var division = await WithUnitOfWorkAsync(async () => await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
			await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, "ReturnPublic", 40), true));
		item = await onboarding.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
		item = await onboarding.AllocateIdentifierAsync(item.Id, new() { NumberSeriesId = Guid.NewGuid(), RollNumber = 17, ConcurrencyStamp = item.ConcurrencyStamp });
		item = await onboarding.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
		var activation = new ActivateStudentOnboardingDto { IdempotencyKey = "reviewed-return-activate", ConcurrencyStamp = item.ConcurrencyStamp };
		item = await onboarding.ActivateAsync(item.Id, activation); item.Status.ShouldBe(StudentOnboardingStatus.Provisioning);
		item = await onboarding.ActivateAsync(item.Id, activation); item.StudentEnrollmentId.ShouldBe(original.Enrollment.Id);
		var finance = await SeedRecordRenewalFinanceAsync(fixture.Student.Id, fixture.Student.AcademicYearId.Value);
		await CollectRecordRenewalCashAsync(fixture.Student.Id, fixture.Student.AcademicYearId.Value, finance.FeeHeadId, finance.DueId, 6000);
		item = await onboarding.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp); item.Status.ShouldBe(StudentOnboardingStatus.Completed, item.LastError);
		(await onboarding.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp)).Status.ShouldBe(StudentOnboardingStatus.Completed);
		await WithUnitOfWorkAsync(async () =>
		{
			var enrollment = await _studentEnrollmentRepository.GetAsync(original.Enrollment.Id); enrollment.IsActive.ShouldBeTrue(); enrollment.OnboardingCaseId.ShouldBe(original.Case.Id);
			(await _studentEnrollmentRepository.CountAsync(x => x.StudentId == fixture.Student.Id)).ShouldBe(1);
			(await _onboardingCaseRepository.CountAsync(x => x.StudentId == fixture.Student.Id && x.Status == StudentOnboardingStatus.Completed)).ShouldBe(2);
			(await _studentRepository.GetAsync(fixture.Student.Id)).GrNumber.ShouldBe("PERMANENT-RETURN-GR");
			(await GetRequiredService<IRepository<StudentRecordTransition, Guid>>().GetAsync(returned.TransitionId!.Value)).Status.ShouldBe("Applied");
			(await GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>().CountAsync(x => x.TransitionId == returned.TransitionId && x.Action == "Applied")).ShouldBe(1);
		});
	});
	[Fact] public Task Record_historical_admission_return_requires_independent_override_and_creates_only_real_enrollment() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await AdmissionReturnFixtureAsync(AdmissionEntryMode.DirectRegistration);
		var historical = await CreateExistingStudentAsync(fixture.Offering, "HistoricalReturn", "Student");
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetAsync(historical.Id); student.ChangeStatus(StudentLifeCycleStatusEnum.Left); student.AssignGrNumber("HISTORICAL-GR-93", 93);
			await _studentRepository.UpdateAsync(student, true);
		});
		var directory = GetRequiredService<IStudentDirectoryAppService>();
		var request = await directory.RequestLegacyOverrideAsync(historical.Id, new() { Reason = "Original permanent register verified", EvidenceReference = "REGISTER-93" });
		var input = FreshAdmissionReturn(historical, fixture.Offering);
		input.Registration = new() { GuardianFirstName = "Verified", GuardianLastName = "Guardian", GuardianMobile = "9876543210", GuardianEmail = "verified.return@example.test" };
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.ReturnAsync(input))).Code.ShouldBe("Students:Records:OnboardingIncomplete");
		(await Should.ThrowAsync<BusinessException>(() => directory.ApproveLegacyOverrideAsync(request.Id, new() { ConcurrencyStamp = request.ConcurrencyStamp }))).Code.ShouldBe("Students:Records:SelfApprovalDenied");
		var reviewer = await CreateAdmissionReturnReviewerAsync();
		using (_principalAccessor.Change(ProfilePrincipal(reviewer)))
			(await directory.ApproveLegacyOverrideAsync(request.Id, new() { ConcurrencyStamp = request.ConcurrencyStamp, Remarks = "Independent original register verification" })).Status.ShouldBe("Approved");
		var returned = await fixture.Service.ReturnAsync(input); returned.Status.ShouldBe("AwaitingAdmission");
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<StudentRecordTransition, Guid>>().GetAsync(returned.TransitionId!.Value)).SourceEnrollmentId.ShouldBeNull();
			(await _studentEnrollmentRepository.CountAsync(x => x.StudentId == historical.Id)).ShouldBe(0);
			(await _onboardingCaseRepository.CountAsync(x => x.StudentId == historical.Id)).ShouldBe(0);
		});
		var onboarding = GetRequiredService<IStudentOnboardingAppService>();
		await onboarding.CreatePolicyAsync(new() { AcademicYearId = historical.AcademicYearId!.Value, Name = "Historical reviewed return",
			RequireDocuments = false, RequireFamilyDetails = false, RequireCommunicationDetails = false, RequireStudentAccount = false, RequireGuardianAccount = false });
		var finalized = await FinalizeAsync(await ReadyAdmissionReturnApplicationAsync(returned.AdmissionApplicationId!.Value), false, "historical-reviewed-finalization");
		var item = await onboarding.GetAsync(finalized.OnboardingCaseId);
		var division = await WithUnitOfWorkAsync(async () => await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
			await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, "HistoricalReturn", 40), true));
		item = await onboarding.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
		item = await onboarding.AllocateIdentifierAsync(item.Id, new() { NumberSeriesId = Guid.NewGuid(), RollNumber = 23, ConcurrencyStamp = item.ConcurrencyStamp });
		item = await onboarding.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
		item = await onboarding.ActivateAsync(item.Id, new() { IdempotencyKey = "historical-reviewed-activate", ConcurrencyStamp = item.ConcurrencyStamp });
		item.Status.ShouldBe(StudentOnboardingStatus.Provisioning);
		await WithUnitOfWorkAsync(async () =>
		{
			var enrollment = await _studentEnrollmentRepository.GetAsync(x => x.StudentId == historical.Id);
			enrollment.Id.ShouldBe(item.StudentEnrollmentId!.Value); enrollment.OnboardingCaseId.ShouldBe(item.Id); enrollment.IsActive.ShouldBeTrue();
			(await _studentEnrollmentRepository.CountAsync(x => x.StudentId == historical.Id)).ShouldBe(1);
			(await _onboardingCaseRepository.CountAsync(x => x.StudentId == historical.Id)).ShouldBe(1);
			(await _studentRepository.GetAsync(historical.Id)).GrNumber.ShouldBe("HISTORICAL-GR-93");
		});
	});
}
