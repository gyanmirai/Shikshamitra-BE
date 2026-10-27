using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionDirectRegistrationService(
	IRepository<AdmissionApplicant, Guid> applicantRepository,
	IRepository<AdmissionFamily, Guid> familyRepository,
	IRepository<AdmissionGuardian, Guid> guardianRepository,
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<AdmissionActivity, Guid> activityRepository,
	IAdmissionReferenceSequenceRepository referenceSequenceRepository,
	AdmissionOfferingRuntimeService offeringRuntime,
	AdmissionApplicationScopeAccessService applicationScope,
	AdmissionDirectFamilyMatchService familyMatchService,
	AdmissionInquiryPeopleService peopleService,
	AdmissionIntakeAssignmentService intakeAssignmentService,
	AdmissionApplicationLifecycleService lifecycleService)
	: ApplicationService, ITransientDependency
{
	public Task<AdmissionApplicationDto> StartAsync(StartAdmissionDirectRegistrationDto input) =>
		StartCoreAsync(input, null);

	public Task<List<AdmissionDirectFamilyMatchDto>> FindFamiliesAsync(Guid offeringId, string guardianMobile, string? guardianEmail = null) =>
		familyMatchService.FindForSpecialRegistrationAsync(offeringId, guardianMobile, guardianEmail);

	internal Task<AdmissionApplicationDto> StartForReturnAsync(StartAdmissionDirectRegistrationDto input, Student student)
		=> StartCoreAsync(input, student);

	private async Task<AdmissionApplicationDto> StartCoreAsync(StartAdmissionDirectRegistrationDto input, Student? returningStudent)
	{
		ArgumentNullException.ThrowIfNull(input);
		if (input.RequestId == Guid.Empty || input.AdmissionOfferingId == Guid.Empty)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"A request ID and Admission Offering are required.");

		// The client keeps RequestId stable across retries. No second family/applicant is created.
		var existingApplicant = await applicantRepository.FindAsync(input.RequestId);
		if (existingApplicant != null)
		{
			if (returningStudent == null && existingApplicant.ExistingStudentId.HasValue)
				throw new BusinessException(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict);
			if (returningStudent != null && existingApplicant.ExistingStudentId != returningStudent.Id)
				throw new BusinessException(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict);
			return await ResolveReplayAsync(existingApplicant, input, returningStudent == null);
		}

		var offering = (await offeringRuntime.GetAvailableAsync(input.AdmissionOfferingId)).Offering;
		if (returningStudent == null)
			AdmissionDirectRegistrationEntryPolicy.EnsureAllowed(offering);
		if (offering.EntryMode is not (AdmissionEntryMode.DirectRegistration or AdmissionEntryMode.Both))
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationEntryNotAllowed,
				"Choose a direct Registration Offering. This Offering starts with Inquiry.");
		Guid? selectedCounsellorId = null;
		if (string.IsNullOrWhiteSpace(input.ApplicantFirstName) || !input.ApplicantDateOfBirth.HasValue ||
			input.ApplicantDateOfBirth.Value.Date > Clock.Now.Date ||
			!Enum.TryParse<GenderEnum>(input.ApplicantGender, true, out var gender) || !Enum.IsDefined(gender) ||
			string.IsNullOrWhiteSpace(input.GuardianFirstName) ||
			!Enum.IsDefined(input.GuardianRelationship))
			throw new BusinessException(AdmissionErrorCodes.InquiryIdentityIncomplete,
				"Applicant and primary guardian identity is incomplete.");
		var phone = AdmissionInquiryPhoneValidator.NormalizeAndValidateIndianMobile(input.GuardianMobile);
		if (input.ParentRelationship != null && (!AdmissionSiblingParentSelection.IsRelationship(input.ParentRelationship) || input.GuardianRelationship != AdmissionGuardianRelationship.Parent) ||
			input.ExistingParentApplicantId.HasValue && (!input.ExistingFamilyId.HasValue || input.ParentRelationship == null) ||
			input.ExistingFamilyId.HasValue && input.ParentRelationship != null && !input.ExistingParentApplicantId.HasValue)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Choose an existing family and Father or Mother.");
		if (input.ExistingFamilyId.HasValue && input.CreateSeparateFamily)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Choose an existing family or create a separate family, not both.");

		var family = input.ExistingFamilyId.HasValue
			? await familyRepository.FindAsync(input.ExistingFamilyId.Value)
			: null;
		if (input.ExistingFamilyId.HasValue && family is not { Status: AdmissionFamilyStatus.Active })
			throw new BusinessException(AdmissionApplicantErrorCodes.ApplicantFamilyNotFound);
		var guardian = family?.PrimaryGuardianId.HasValue == true
			? await guardianRepository.FindAsync(family.PrimaryGuardianId.Value)
			: null;
		AdmissionSiblingParentCandidateDto? selectedParent = null;
		if (family != null && input.ExistingParentApplicantId.HasValue)
		{
			if (!CurrentUser.Id.HasValue) throw new BusinessException(AdmissionErrorCodes.DirectRegistrationFamilyMismatch);
			selectedParent = await familyMatchService.ResolveParentAsync(family.Id, input.ExistingParentApplicantId.Value,
				input.ParentRelationship, phone, input.GuardianEmail, AdmissionErrorCodes.DirectRegistrationFamilyMismatch);
		}
		if (family != null && selectedParent == null && (guardian == null || guardian.FamilyId != family.Id || guardian.Mobile != phone))
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationFamilyMismatch,
				"The selected family's primary guardian does not match this mobile number.");
		if (family != null && selectedParent == null)
			await familyMatchService.EnsureCanLinkAsync(family.Id, phone);

		if (family == null)
		{
			var accessibleMatches = await familyMatchService.FindAsync(offering.Id, phone, input.ParentRelationship == null ? null : input.GuardianEmail);
			if (accessibleMatches.Count > 0 && !input.CreateSeparateFamily)
				throw new BusinessException(AdmissionApplicantErrorCodes.DuplicateFamily,
					"A family with this guardian mobile already exists. Link that family or explicitly create a separate family.");
			if (input.CreateSeparateFamily && string.IsNullOrWhiteSpace(input.SeparateFamilyReason))
				throw new BusinessException(AdmissionApplicantErrorCodes.DuplicateFamily,
					"Record a reason before creating a separate family with the same guardian mobile.");
			var year = Clock.Now.ToUniversalTime().Year;
			var number = AdmissionReferenceNumber.Format(AdmissionReferenceKind.Family, year,
				await referenceSequenceRepository.IncrementAndGetAsync(year, AdmissionReferenceKind.Family));
			family = new AdmissionFamily(GuidGenerator.Create(), CurrentTenant.Id, number);
			guardian = new AdmissionGuardian(GuidGenerator.Create(), CurrentTenant.Id, family.Id,
				input.GuardianFirstName, input.GuardianLastName, phone, input.GuardianEmail,
				input.GuardianRelationship, isPrimaryContact: true);
			family.SetPrimaryGuardian(guardian.Id);
			await familyRepository.InsertAsync(family, autoSave: false);
			await guardianRepository.InsertAsync(guardian, autoSave: false);
		}

		var applicant = new AdmissionApplicant(input.RequestId, CurrentTenant.Id, family.Id,
			input.ApplicantFirstName, input.ApplicantMiddleName, input.ApplicantLastName,
			input.ApplicantDateOfBirth, input.ApplicantGender, existingStudentId: returningStudent?.Id,
			reservedStudentId: GuidGenerator.Create());
		if (selectedParent != null)
			AdmissionSiblingParentSelection.Confirm(applicant, selectedParent, CurrentUser.Id!.Value, Clock.Now.ToUniversalTime(), phone, input.GuardianEmail);
		else if (input.ParentRelationship != null)
			AdmissionSiblingParentSelection.CaptureParent(applicant, input.ParentRelationship,
				string.Join(" ", new[] { input.GuardianFirstName, input.GuardianLastName }.Where(value => !string.IsNullOrWhiteSpace(value))), phone, input.GuardianEmail);
		AdmissionFamilyContactCapture.Capture(applicant, input.FamilyContacts, input.ParentRelationship);
		await applicantRepository.InsertAsync(applicant, autoSave: true);
		var application = returningStudent == null
			? await lifecycleService.StartForSpecialRegistrationAsync(applicant.Id, offering.Id)
			: await lifecycleService.StartForReturnAsync(applicant.Id, offering.Id);
		await activityRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity("DirectRegistrationStarted", "Registration started without an inquiry", applicant.Id, application.Id, null,
			JsonSerializer.Serialize(new
			{
				offering.Track, input.CounsellorId, AssignedCounsellorId = selectedCounsellorId,
				input.ExistingFamilyId, input.CreateSeparateFamily,
				input.SeparateFamilyReason, GuardianRelationship = input.GuardianRelationship.ToString(), input.ExistingParentApplicantId, input.ParentRelationship
			})), autoSave: true);
		return application;
	}

	private async Task<AdmissionApplicationDto> ResolveReplayAsync(
		AdmissionApplicant applicant, StartAdmissionDirectRegistrationDto input, bool requireSpecialTrack)
	{
		var offering = await offeringRepository.FindAsync(input.AdmissionOfferingId);
		if (offering == null || offering.EntryMode is not (AdmissionEntryMode.DirectRegistration or AdmissionEntryMode.Both))
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict);
		if (requireSpecialTrack)
			AdmissionDirectRegistrationEntryPolicy.EnsureAllowed(offering);
		var query = await applicationRepository.GetQueryableAsync();
		var application = await AsyncExecuter.FirstOrDefaultAsync(query.Where(item =>
			item.ApplicantId == applicant.Id && item.AdmissionOfferingId == input.AdmissionOfferingId &&
			!item.InquiryId.HasValue));
		if (application == null)
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict,
				"This request ID already belongs to another registration.");
		await applicationScope.EnsureCanAccessAsync(application);
		if (!AdmissionFamilyContactCapture.MatchesRequest(applicant, input.FamilyContacts, input.ParentRelationship))
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict,
				"This request ID already belongs to different family contact choices.");
		var family = await familyRepository.FindAsync(applicant.FamilyId);
		var guardian = family?.PrimaryGuardianId.HasValue == true
			? await guardianRepository.FindAsync(family.PrimaryGuardianId.Value) : null;
		var phone = AdmissionInquiryPhoneValidator.NormalizeAndValidateIndianMobile(input.GuardianMobile);
		var confirmation = AdmissionSiblingParentSelection.ReadConfirmation(applicant);
		if (AdmissionSiblingParentSelection.IsParentFlow(applicant) && AdmissionSiblingParentSelection.ReadRelationship(applicant) != input.ParentRelationship)
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict);
		if (input.ExistingParentApplicantId.HasValue || confirmation != null)
		{
			if (confirmation == null || confirmation.SourceApplicantId != input.ExistingParentApplicantId ||
				confirmation.Relationship != input.ParentRelationship || confirmation.SubmittedMobile != phone ||
				confirmation.SubmittedEmail != AdmissionSiblingParentSelection.NormalizeEmail(input.GuardianEmail))
				throw new BusinessException(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict);
		}
		if (!string.Equals(applicant.FirstName, input.ApplicantFirstName?.Trim(), StringComparison.OrdinalIgnoreCase) ||
			!string.Equals(applicant.MiddleName, Normalize(input.ApplicantMiddleName), StringComparison.OrdinalIgnoreCase) ||
			!string.Equals(applicant.LastName, Normalize(input.ApplicantLastName), StringComparison.OrdinalIgnoreCase) ||
			applicant.DateOfBirth?.Date != input.ApplicantDateOfBirth?.Date ||
			!string.Equals(applicant.Gender, Normalize(input.ApplicantGender), StringComparison.OrdinalIgnoreCase) ||
			(!input.ExistingFamilyId.HasValue &&
				(!string.Equals(guardian?.FirstName, input.GuardianFirstName?.Trim(), StringComparison.OrdinalIgnoreCase) ||
				 !string.Equals(guardian?.LastName, Normalize(input.GuardianLastName), StringComparison.OrdinalIgnoreCase) ||
				 !string.Equals(guardian?.Email, Normalize(input.GuardianEmail)?.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) ||
				 guardian?.Relationship != input.GuardianRelationship)) ||
			(confirmation == null && guardian?.Mobile != phone) ||
			(input.ExistingFamilyId.HasValue && applicant.FamilyId != input.ExistingFamilyId.Value))
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict,
				"This request ID was already used for different applicant or family details.");
		return AdmissionApplicationMapper.ToDto(application);
	}

	private static string? Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}
