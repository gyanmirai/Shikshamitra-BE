using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.ValueObjects;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationDecisionService(
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<AdmissionApplicant, Guid> applicantRepository,
	IRepository<AdmissionFamily, Guid> familyRepository,
	IRepository<AdmissionGuardian, Guid> guardianRepository,
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionWorkflowStageDefinition, Guid> workflowStageRepository,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflowRepository,
	IRepository<AdmissionApplicationDecisionHistory, Guid> historyRepository,
	IRepository<AdmissionActivity, Guid> activityRepository,
	IRepository<StudentOnboardingCase, Guid> onboardingRepository,
	IRepository<StudentOnboardingStep, Guid> stepRepository,
	IRepository<StudentOnboardingEvent, Guid> eventRepository,
	IRepository<StudentOnboardingPolicy, Guid> policyRepository,
	IStudentRepository studentRepository,
	IRepository<StudentEnrollment, Guid> enrollmentRepository,
	AdmissionDocumentHandoffService documentHandoff,
	AdmissionEligibilityRuntimeService eligibilityRuntime,
	AdmissionReviewDecisionGuard reviewGuard,
	AdmissionSeatRuntimeService seatRuntime,
	AdmissionOfferCommandService offerCommands,
	AdmissionInquiryPeopleService peopleService,
	AdmissionApplicationScopeAccessService applicationScope,
	IAdmissionFinalizationConflictClassifier finalizationConflicts)
	: ApplicationService, ITransientDependency
{
	public Task<AdmissionApplicationDto> MarkInReviewAsync(Guid id, AdmissionApplicationDecisionDto input) =>
		ChangeAsync(id, input, application => application.MarkInReview(CurrentUser.Id, input.Reason));

	public Task<AdmissionApplicationDto> ApproveAsync(Guid id, AdmissionApplicationDecisionDto input) =>
		ChangeAsync(id, input, application => application.Approve(CurrentUser.Id, input.Reason), "approve");

	public Task<AdmissionApplicationDto> RejectAsync(Guid id, AdmissionApplicationDecisionDto input) =>
		ChangeAsync(id, input, application => application.Reject(CurrentUser.Id, input.Reason ?? string.Empty), "reject");

	public Task<AdmissionApplicationDto> WaitlistAsync(Guid id, AdmissionApplicationDecisionDto input) =>
		ChangeAsync(id, input, application => application.Waitlist(CurrentUser.Id, input.Reason ?? string.Empty), "waitlist");

	public async Task<AdmissionApplicationDto> IssueOfferAsync(Guid id, AdmissionApplicationDecisionDto input)
	{
		if (await offerCommands.TryIssueLegacyAsync(id, input)) return await GetDecisionResultAsync(id);
		var target = await GetApplicationAsync(id);
		if (input.CounsellorId.HasValue && input.CounsellorId != target.AssignedCounsellorId)
			throw new BusinessException(AdmissionOfferStageErrorCodes.InvalidState,
				"Change the journey counsellor through the application assignment action before issuing an offer.");
		var effectiveCounsellorId = input.CounsellorId ?? target.AssignedCounsellorId;
		if (effectiveCounsellorId.HasValue)
			await peopleService.EnsureCanAccessOfferingAsync(effectiveCounsellorId.Value, target.AdmissionOfferingId);
		return await ChangeAsync(id, input, application =>
		{
			application.IssueOffer(
				input.OfferExpiresAt.HasValue
					? input.OfferExpiresAt.Value.UtcDateTime
					: throw new BusinessException(AdmissionErrorCodes.ApplicationOfferExpiryInvalid),
				CurrentUser.Id, input.Reason);
		}, "issueOffer");
	}

	public async Task<AdmissionApplicationDto> AcceptOfferAsync(Guid id, AdmissionApplicationDecisionDto input)
	{
		if (await offerCommands.TryRespondLegacyAsync(id, input, true)) return await GetDecisionResultAsync(id);
		return await ChangeAsync(id, input, application => application.AcceptOffer(CurrentUser.Id, input.Reason), "acceptOffer");
	}

	public async Task<AdmissionApplicationDto> DeclineOfferAsync(Guid id, AdmissionApplicationDecisionDto input)
	{
		if (await offerCommands.TryRespondLegacyAsync(id, input, false)) return await GetDecisionResultAsync(id);
		return await ChangeAsync(id, input, application => application.DeclineOffer(CurrentUser.Id, input.Reason ?? string.Empty));
	}

	public async Task<AdmissionFinalizationPreviewDto> GetFinalizationPreviewAsync(Guid id)
	{
		var context = await LoadFinalizationContextAsync(id);
		var matches = await FindPossibleMatchesAsync(context.Applicant);
		var items = BuildReadinessItems(context, matches);
		try { await documentHandoff.EnsureCompleteAsync(context.Application); }
		catch (BusinessException exception) when (exception.Code is AdmissionDocumentErrorCodes.EvidenceRequired or AdmissionDocumentErrorCodes.InvalidPolicy)
		{
			items.Add(Item("admission_documents", "Admission document evidence accepted", false, exception.Message));
		}
		try { await eligibilityRuntime.EnsureApplicationCompleteAsync(context.Application); }
		catch (BusinessException exception) when (exception.Code is AdmissionEligibilityErrorCodes.EvaluationRequired or AdmissionEligibilityErrorCodes.StaleEvaluation or AdmissionEligibilityErrorCodes.InvalidPolicy)
		{
			items.Add(Item("admission_eligibility", "Admission eligibility checked", false, "Check the latest eligibility information and resolve unmet criteria before finalizing admission."));
		}
		try { await reviewGuard.EnsureApplicationCompleteAsync(context.Application); }
		catch (BusinessException exception) when (exception.Code?.StartsWith("Admissions:Review:", StringComparison.Ordinal) == true)
		{ items.Add(Item("admission_review", "Admission review approved", false, AdmissionReviewDecisionGuard.Message(exception.Code))); }
		try { await seatRuntime.EnsureApplicationCompleteAsync(context.Application); }
		catch (BusinessException exception) when (exception.Code is AdmissionSeatStageErrorCodes.CompletionRequired or AdmissionSeatStageErrorCodes.InvalidPolicy)
		{ items.Add(Item("admission_seat", "Admission seat confirmed", false, exception.Message)); }
		return new AdmissionFinalizationPreviewDto
		{
			ApplicationId = id,
			LinkedStudentId = context.Applicant.ExistingStudentId,
			IsReady = items.All(item => item.Passed),
			Items = items,
			PossibleMatches = matches.Select(ToMatchDto).ToList()
		};
	}

	public async Task<AdmissionFinalizationResultDto> FinalizeAsync(Guid id, FinalizeAdmissionApplicationDto input)
	{
		try
		{
			return await FinalizeCoreAsync(id, input);
		}
		catch (Exception exception) when (finalizationConflicts.IsStudentIdentityConflict(exception))
		{
			// Includes hidden/deleted Students and racing requests, without disabling tenant filters.
			throw new BusinessException(AdmissionApplicantErrorCodes.ReservedStudentIdConflict,
				innerException: exception);
		}
		catch (Exception exception) when (finalizationConflicts.IsConcurrentFinalization(exception))
		{
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict,
				innerException: exception);
		}
	}

	private async Task<AdmissionFinalizationResultDto> FinalizeCoreAsync(Guid id, FinalizeAdmissionApplicationDto input)
	{
		var context = await LoadFinalizationContextAsync(id);

		if (context.Application.Status == AdmissionApplicationStatus.AdmissionFinalized &&
		    context.Application.StudentId.HasValue && context.Application.OnboardingCaseId.HasValue)
		{
			await documentHandoff.LinkAsync(context.Application,
				await studentRepository.GetAsync(context.Application.StudentId.Value));
			await CurrentUnitOfWork!.SaveChangesAsync();
			return BuildResult(context.Application, context.Application.StudentId.Value,
				context.Application.OnboardingCaseId.Value, false);
		}

		var existingCase = await onboardingRepository.FindAsync(item => item.ApplicationId == id);
		var idempotentCase = await onboardingRepository.FindAsync(item => item.IdempotencyKey == input.IdempotencyKey);
		if (idempotentCase != null && idempotentCase.ApplicationId != id)
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict)
				.WithData("Reason", "The idempotency key has already been used for another application.");
		existingCase ??= idempotentCase;
		if (existingCase != null)
		{
			await documentHandoff.EnsureCompleteAsync(context.Application);
			await eligibilityRuntime.EnsureApplicationCompleteAsync(context.Application);
			await reviewGuard.EnsureApplicationCompleteAsync(context.Application);
			await seatRuntime.EnsureApplicationCompleteAsync(context.Application);
			context.Applicant.LinkStudent(existingCase.StudentId);
			FinalizeApplication(context, existingCase.StudentId, existingCase.Id, existingCase.CreationTime);
			await documentHandoff.LinkAsync(context.Application, await studentRepository.GetAsync(existingCase.StudentId));
			await applicantRepository.UpdateAsync(context.Applicant, false);
			await applicationRepository.UpdateAsync(context.Application, false);
			await CurrentUnitOfWork!.SaveChangesAsync();
			return BuildResult(context.Application, existingCase.StudentId, existingCase.Id, false);
		}
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp))
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict);
		EnsureConcurrency(context.Application, input.ConcurrencyStamp);

		var matches = await FindPossibleMatchesAsync(context.Applicant);
		var blockingItems = BuildReadinessItems(context, matches)
			.Where(item => item.Key != "duplicate_resolution" && !item.Passed)
			.ToList();
		if (blockingItems.Count > 0)
			throw new BusinessException(StudentOnboardingErrorCodes.FinalizationNotReady)
				.WithData("Blockers", string.Join(", ", blockingItems.Select(item => item.Label)));
		if (!input.ConfirmGuardianContactVerified)
			throw new BusinessException(StudentOnboardingErrorCodes.GuardianContactUnverified);
		await documentHandoff.EnsureCompleteAsync(context.Application);
		await eligibilityRuntime.EnsureApplicationCompleteAsync(context.Application);
		await reviewGuard.EnsureApplicationCompleteAsync(context.Application);
		await seatRuntime.EnsureApplicationCompleteAsync(context.Application);

		var selectedStudentId = context.Applicant.ExistingStudentId ?? input.ExistingStudentId;
		if (selectedStudentId.HasValue && input.ConfirmCreateNew)
			throw new BusinessException(StudentOnboardingErrorCodes.DuplicateResolutionRequired);
		if (!selectedStudentId.HasValue && matches.Count > 0 && !input.ConfirmCreateNew)
			throw new BusinessException(StudentOnboardingErrorCodes.DuplicateResolutionRequired)
				.WithData("CandidateCount", matches.Count);
		if (input.ExistingStudentId.HasValue && !matches.Any(match => match.Id == input.ExistingStudentId.Value))
			throw new BusinessException(StudentOnboardingErrorCodes.ExistingStudentInvalid);

		Student student;
		var reusedExistingStudent = selectedStudentId.HasValue;
		if (selectedStudentId.HasValue)
		{
			student = await studentRepository.FindAsync(selectedStudentId.Value) ??
				throw new BusinessException(StudentOnboardingErrorCodes.ExistingStudentInvalid);
			AdmissionAdditionalStudentFieldCatalog.CopyToStudent(context.Applicant, student);
			await studentRepository.UpdateAsync(student, false);
		}
		else
		{
			if (await studentRepository.AnyAsync(item => item.Id == context.Applicant.ReservedStudentId))
				throw new BusinessException(AdmissionApplicantErrorCodes.ReservedStudentIdConflict);
			student = CreateStudent(context);
			await studentRepository.InsertAsync(student, false);
		}

		if (await enrollmentRepository.AnyAsync(item =>
			item.StudentId == student.Id && item.AcademicYearId == context.Cycle.AcademicYearId && item.IsActive))
			throw new BusinessException(StudentOnboardingErrorCodes.EnrollmentAlreadyExists);
		if (await onboardingRepository.AnyAsync(item =>
			item.StudentId == student.Id && item.AcademicYearId == context.Cycle.AcademicYearId &&
			item.Status != StudentOnboardingStatus.Cancelled))
			throw new BusinessException(StudentOnboardingErrorCodes.OnboardingAlreadyExists);

		var now = DateTime.UtcNow;
		var policy = await ResolvePolicyAsync(context.Cycle.AcademicYearId, context.Offering.Id, context.Offering.StandardId);
		var onboardingCase = new StudentOnboardingCase(
			GuidGenerator.Create(), CurrentTenant.Id, context.Application.Id, context.Applicant.Id, student.Id,
			context.Offering.Id, context.Cycle.AcademicYearId, context.Offering.BoardId, context.Offering.MediumId,
			context.Offering.StageId, context.Offering.StandardId, input.IdempotencyKey, now,
			now.AddHours(policy?.SlaHours ?? 72));
		onboardingCase.SetCampusId(context.Offering.CampusId);
		if (context.Application.AssignedCounsellorId.HasValue)
			onboardingCase.AssignOwner(context.Application.AssignedCounsellorId);
		await onboardingRepository.InsertAsync(onboardingCase, false);
		await CreateDefaultStepsAsync(onboardingCase, policy, now);

		context.Applicant.LinkStudent(student.Id);
		FinalizeApplication(context, student.Id, onboardingCase.Id, now);
		await documentHandoff.LinkAsync(context.Application, student);
		await applicantRepository.UpdateAsync(context.Applicant, false);
		await applicationRepository.UpdateAsync(context.Application, false);
		await historyRepository.InsertAsync(new AdmissionApplicationDecisionHistory(GuidGenerator.Create(),
			CurrentTenant.Id, context.Application.Id, AdmissionApplicationStatus.AdmissionFinalized,
			"Admission finalized and transferred to Student Onboarding", CurrentUser.Id), false);
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			"AdmissionFinalized", "Student identity linked and onboarding case created",
			context.Applicant.Id, context.Application.Id), false);
		await eventRepository.InsertAsync(new StudentOnboardingEvent(GuidGenerator.Create(), CurrentTenant.Id,
			onboardingCase.Id, "CaseCreated", "Admission finalized; onboarding responsibility started."), false);
		await CurrentUnitOfWork!.SaveChangesAsync();

		return BuildResult(context.Application, student.Id, onboardingCase.Id, reusedExistingStudent);
	}

	[Obsolete("Admissions finalization no longer creates StudentEnrollment rows.")]
	public Task<AdmissionEnrollmentResultDto> EnrollAsync(Guid id, EnrollAdmissionApplicationDto input) =>
		throw new BusinessException(StudentOnboardingErrorCodes.LegacyEnrollDisabled)
			.WithData("Replacement", $"/api/student/enrollment/admissions/applications/{id}/finalize");

	[Obsolete("Placement choices belong to Student Onboarding.")]
	public Task<AdmissionEnrollmentOptionsDto> GetEnrollmentOptionsAsync(Guid id) =>
		throw new BusinessException(StudentOnboardingErrorCodes.LegacyEnrollDisabled)
			.WithData("Replacement", "/api/student/onboarding");

	public async Task<List<AdmissionApplicationDecisionHistoryDto>> GetDecisionHistoryAsync(Guid id)
	{
		await GetApplicationAsync(id);
		var rows = await historyRepository.GetListAsync(item => item.ApplicationId == id);
		return rows.OrderByDescending(item => item.OccurredAt).Select(item => new AdmissionApplicationDecisionHistoryDto
		{
			Id = item.Id, Status = item.Status, Reason = item.Reason, ActorId = item.ActorId, OccurredAt = item.OccurredAt
		}).ToList();
	}

	private async Task CreateDefaultStepsAsync(StudentOnboardingCase onboardingCase, StudentOnboardingPolicy? policy, DateTime now)
	{
		for (var index = 0; index < StudentOnboardingStepKeys.Defaults.Length; index++)
		{
			var definition = StudentOnboardingStepKeys.Defaults[index];
			var required = IsRequired(definition.Key, policy);
			var initialStatus = required ? StudentOnboardingStepStatus.Pending : StudentOnboardingStepStatus.NotRequired;
			var step = new StudentOnboardingStep(GuidGenerator.Create(), CurrentTenant.Id, onboardingCase.Id,
				definition.Key, definition.Label, index + 1, required, initialStatus);
			if (definition.Key == StudentOnboardingStepKeys.Identity)
				step.Complete(CurrentUser.Id, now, "Core identity verified during admission finalization.");
			await stepRepository.InsertAsync(step, false);
		}
	}

	private async Task<StudentOnboardingPolicy?> ResolvePolicyAsync(Guid academicYearId, Guid offeringId, Guid standardId)
	{
		var policies = await policyRepository.GetListAsync(item => item.AcademicYearId == academicYearId && item.IsActive);
		return policies
			.OrderByDescending(item => item.OfferingId == offeringId && item.StandardId == standardId)
			.ThenByDescending(item => item.OfferingId == offeringId)
			.ThenByDescending(item => item.StandardId == standardId)
			.ThenByDescending(item => !item.OfferingId.HasValue && !item.StandardId.HasValue)
			.FirstOrDefault(item =>
				(item.OfferingId == offeringId || !item.OfferingId.HasValue) &&
				(item.StandardId == standardId || !item.StandardId.HasValue));
	}

	private static bool IsRequired(string key, StudentOnboardingPolicy? policy) => key switch
	{
		StudentOnboardingStepKeys.Identity => true,
		StudentOnboardingStepKeys.Family => policy?.RequireFamilyDetails ?? true,
		StudentOnboardingStepKeys.Communication => policy?.RequireCommunicationDetails ?? true,
		StudentOnboardingStepKeys.Documents => policy?.RequireDocuments ?? true,
		StudentOnboardingStepKeys.MedicalAndPreviousSchool => policy?.RequireMedicalDetails ?? false,
		StudentOnboardingStepKeys.PlacementAndSeat => true,
		StudentOnboardingStepKeys.IdentifiersAndRoll => true,
		StudentOnboardingStepKeys.FinanceAccountsServices => policy is not null &&
			(policy.RequireFeeClearance || policy.RequireStudentAccount || policy.RequireGuardianAccount || policy.RequireServiceConsent),
		StudentOnboardingStepKeys.FinalReadiness => true,
		_ => true
	};

	private async Task<List<Student>> FindPossibleMatchesAsync(AdmissionApplicant applicant)
	{
		if (applicant.ExistingStudentId.HasValue)
		{
			var linked = await studentRepository.FindAsync(applicant.ExistingStudentId.Value);
			return linked == null ? [] : [linked];
		}

		var normalizedFirstName = applicant.FirstName.Trim().ToLower();
		var normalizedLastName = applicant.LastName?.Trim().ToLower();
		var query = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		var candidates = await AsyncExecuter.ToListAsync(query.Where(student =>
			student.FirstName != null && student.FirstName.ToLower() == normalizedFirstName &&
			(normalizedLastName == null || student.LastName != null && student.LastName.ToLower() == normalizedLastName)));

		if (!applicant.DateOfBirth.HasValue)
			return candidates.Take(10).ToList();
		var applicantBirthDate = applicant.DateOfBirth.Value.Date;
		var detailed = new List<Student>();
		foreach (var candidate in candidates.Take(25))
		{
			var student = await studentRepository.GetWithDetailsAsync(candidate.Id);
			if (student?.AdditionalDetail?.DateOfBirth?.Date == applicantBirthDate)
				detailed.Add(student);
		}
		return detailed.Take(10).ToList();
	}

	private static AdmissionStudentMatchDto ToMatchDto(Student student) => new()
	{
		StudentId = student.Id,
		StudentName = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
			.Where(value => !string.IsNullOrWhiteSpace(value))),
		DateOfBirth = student.AdditionalDetail?.DateOfBirth?.UtcDateTime,
		Status = student.Status,
		GrNumber = student.GrNumber ?? student.GrNo?.ToString(),
		AcademicYearId = student.AcademicYearId
	};

	private static List<AdmissionFinalizationReadinessItemDto> BuildReadinessItems(
		FinalizationContext context, IReadOnlyCollection<Student> matches)
	{
		var application = context.Application;
		var applicant = context.Applicant;
		var usesDirectApproval = context.AllowsApprovedDirectFinalization &&
			application.Status is not (AdmissionApplicationStatus.Offered or
				AdmissionApplicationStatus.OfferAccepted or AdmissionApplicationStatus.OfferDeclined);
		var offerReady = usesDirectApproval
			? application.Status == AdmissionApplicationStatus.Approved
			: application.Status == AdmissionApplicationStatus.OfferAccepted &&
				application.OfferAcceptedAt.HasValue &&
				(!application.OfferExpiresAt.HasValue || application.OfferExpiresAt.Value > DateTime.UtcNow);
		var identityReady = !string.IsNullOrWhiteSpace(applicant.FirstName) &&
			!string.IsNullOrWhiteSpace(applicant.LastName) && applicant.DateOfBirth.HasValue &&
			Enum.TryParse<GenderEnum>(applicant.Gender, true, out _);
		var guardianReady = context.Guardian != null &&
			(AdmissionInquiryPhoneValidator.IsValidIndianMobile(context.Guardian.Mobile) ||
			 MailAddress.TryCreate(context.Guardian.Email, out _));
		var workflowReady = context.Workflow?.Status == AdmissionWorkflowInstanceStatus.Completed &&
			!application.CurrentStageInstanceId.HasValue;
		return
		[
			Item("offer", usesDirectApproval ? "Direct admission approved" : "Accepted, unexpired offer",
				offerReady, offerReady ? "Ready" : usesDirectApproval
					? "Approve this direct admission before finalizing." : "Offer must be accepted and unexpired."),
			Item("workflow", "Admission workflow completed", workflowReady, workflowReady ? "Ready" : "Complete the admission workflow first."),
			Item("identity", "Core identity complete", identityReady, identityReady ? "Ready" : "Name, date of birth, and gender are required."),
			Item("guardian", "Primary guardian contact valid", guardianReady, guardianReady ? "Verification confirmation is required when finalizing." : "Enter a valid 10-digit Indian mobile number or email for the primary guardian."),
			Item("duplicate_resolution", "Student identity resolved", applicant.ExistingStudentId.HasValue || matches.Count == 0,
				applicant.ExistingStudentId.HasValue ? "Existing Student is already linked." : matches.Count == 0 ? "No exact identity matches found." : $"{matches.Count} possible match(es) require a staff decision.")
		];
	}

	private static AdmissionFinalizationReadinessItemDto Item(string key, string label, bool passed, string detail) =>
		new() { Key = key, Label = label, Passed = passed, Detail = detail };

	private static void FinalizeApplication(FinalizationContext context, Guid studentId, Guid caseId, DateTime finalizedAt)
	{
		if (context.AllowsApprovedDirectFinalization && context.Application.Status == AdmissionApplicationStatus.Approved)
			context.Application.FinalizeApprovedDirectAdmission(studentId, caseId, finalizedAt);
		else
			context.Application.FinalizeAdmission(studentId, caseId, finalizedAt);
	}


	private Student CreateStudent(FinalizationContext context)
	{
		if (!context.Applicant.DateOfBirth.HasValue ||
		    !Enum.TryParse<GenderEnum>(context.Applicant.Gender, true, out var gender) || context.Guardian == null)
			throw new BusinessException(StudentOnboardingErrorCodes.CoreIdentityIncomplete);
		var applicant = context.Applicant;
		var guardian = context.Guardian;
		var student = Student.CreateDirectRegistration(applicant.ReservedStudentId, CurrentTenant.Id,
			applicant.FirstName, applicant.MiddleName, applicant.LastName, context.Cycle.AcademicYearId,
			context.Offering.BoardId, context.Offering.MediumId, context.Offering.StageId, null, null,
			applicant.DateOfBirth.Value, gender, guardian.Mobile, email: guardian.Email,
			address1: applicant.AddressLine1, address2: applicant.AddressLine2, city: applicant.City,
			state: string.IsNullOrWhiteSpace(applicant.State) ? null : new State(applicant.State, string.Empty),
			country: string.IsNullOrWhiteSpace(applicant.Country) ? null : new Country(applicant.Country, string.Empty),
			pincode: applicant.PostalCode);
		student.UpdateFamilyInfo(null, null, new ParentGuardianDetails(
			$"{guardian.FirstName} {guardian.LastName}".Trim(), null, null, guardian.Email, guardian.Mobile,
			null, null, null, null, null, null, null, null), null);
		AdmissionAdditionalStudentFieldCatalog.CopyToStudent(applicant, student);
		student.MoveToAdmission();
		return student;
	}

	private async Task<FinalizationContext> LoadFinalizationContextAsync(Guid id)
	{
		var application = await GetApplicationAsync(id);
		var applicant = await applicantRepository.FindAsync(application.ApplicantId) ??
			throw new EntityNotFoundException(typeof(AdmissionApplicant), application.ApplicantId);
		var family = applicant.FamilyId == Guid.Empty ? null : await familyRepository.FindAsync(applicant.FamilyId);
		var guardian = family?.PrimaryGuardianId.HasValue == true
			? await guardianRepository.FindAsync(family.PrimaryGuardianId.Value)
			: await guardianRepository.FindAsync(item => item.FamilyId == applicant.FamilyId && item.IsPrimaryContact);
		var cycle = await cycleRepository.FindAsync(application.AdmissionCycleId) ??
			throw new EntityNotFoundException(typeof(AdmissionCycle), application.AdmissionCycleId);
		var offering = await offeringRepository.FindAsync(application.AdmissionOfferingId) ??
			throw new EntityNotFoundException(typeof(AdmissionOffering), application.AdmissionOfferingId);
		var workflow = await workflowRepository.FindAsync(item => item.ApplicationId == id);
		var hasOfferStage = await workflowStageRepository.AnyAsync(stage =>
			stage.WorkflowDefinitionId == application.WorkflowDefinitionId &&
			stage.StageType == AdmissionWorkflowStageType.Offer);
		var allowsApprovedDirectFinalization = offering.EntryMode == AdmissionEntryMode.DirectRegistration &&
			offering.Track != AdmissionTrack.General && !hasOfferStage;
		return new FinalizationContext(application, applicant, guardian, cycle, offering, workflow,
			allowsApprovedDirectFinalization);
	}

	private async Task<AdmissionApplicationDto> ChangeAsync(Guid id, AdmissionApplicationDecisionDto input, Action<AdmissionApplication> change, string? decision = null)
	{
		var application = await GetApplicationAsync(id);
		EnsureConcurrency(application, input.ConcurrencyStamp);
		var review = decision is "approve" or "reject" or "waitlist" ? await reviewGuard.EnsureDecisionAsync(application, decision) : null;
		if (decision is "issueOffer" or "acceptOffer") await reviewGuard.EnsureApplicationCompleteAsync(application);
		var reaffirm = review != null && decision == "approve" && application.Status is AdmissionApplicationStatus.Approved or AdmissionApplicationStatus.Offered or AdmissionApplicationStatus.OfferAccepted;
		if (!reaffirm) change(application);
		if (review != null) await reviewGuard.RecordDecisionAsync(review, decision!, input.Reason);
		await applicationRepository.UpdateAsync(application, false);
		await historyRepository.InsertAsync(new AdmissionApplicationDecisionHistory(GuidGenerator.Create(), CurrentTenant.Id,
			application.Id, application.Status, input.Reason, CurrentUser.Id), false);
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			"ApplicationDecisionChanged", $"Application status changed to {application.Status}", application.ApplicantId, application.Id), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		var result = AdmissionApplicationMapper.ToDto(application);
		result.ReviewDecision = await reviewGuard.GetCapabilitiesAsync(application);
		return result;
	}

	private async Task<AdmissionApplication> GetApplicationAsync(Guid id)
	{
		var application = await applicationRepository.FindAsync(id) ??
			throw new EntityNotFoundException(typeof(AdmissionApplication), id);
		await applicationScope.EnsureCanAccessAsync(application);
		return application;
	}

	private async Task<AdmissionApplicationDto> GetDecisionResultAsync(Guid id)
	{
		var application = await GetApplicationAsync(id);
		var result = AdmissionApplicationMapper.ToDto(application);
		result.ReviewDecision = await reviewGuard.GetCapabilitiesAsync(application);
		return result;
	}

	private static void EnsureConcurrency(AdmissionApplication application, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) ||
		    !string.Equals(stamp, application.ConcurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict);
	}

	private static AdmissionFinalizationResultDto BuildResult(
		AdmissionApplication application, Guid studentId, Guid caseId, bool reusedExistingStudent) => new()
	{
		Application = AdmissionApplicationMapper.ToDto(application),
		StudentId = studentId,
		OnboardingCaseId = caseId,
		ReusedExistingStudent = reusedExistingStudent
	};

	private sealed record FinalizationContext(
		AdmissionApplication Application,
		AdmissionApplicant Applicant,
		AdmissionGuardian? Guardian,
		AdmissionCycle Cycle,
		AdmissionOffering Offering,
		AdmissionApplicationWorkflowInstance? Workflow,
		bool AllowsApprovedDirectFinalization);
}
