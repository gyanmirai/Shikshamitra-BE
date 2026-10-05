using System;
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
	public async Task<AdmissionApplicationDto> StartAsync(StartAdmissionDirectRegistrationDto input)
		=> await StartCoreAsync(input, null);

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
			if (returningStudent != null && existingApplicant.ExistingStudentId != returningStudent.Id)
				throw new BusinessException(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict);
			return await ResolveReplayAsync(existingApplicant, input);
		}

		var offering = (await offeringRuntime.GetAvailableAsync(input.AdmissionOfferingId)).Offering;
		if (offering.EntryMode is not (AdmissionEntryMode.DirectRegistration or AdmissionEntryMode.Both))
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationEntryNotAllowed,
				"Choose a direct Registration Offering. This Offering starts with Inquiry.");
		var selectedCounsellorId = input.CounsellorId ?? await intakeAssignmentService.ResolveAsync(offering);
		if (input.CounsellorId.HasValue)
			await peopleService.EnsureCanAccessOfferingAsync(input.CounsellorId.Value, offering.Id);
		if (string.IsNullOrWhiteSpace(input.ApplicantFirstName) || !input.ApplicantDateOfBirth.HasValue ||
			input.ApplicantDateOfBirth.Value.Date > Clock.Now.Date ||
			!Enum.TryParse<GenderEnum>(input.ApplicantGender, true, out var gender) || !Enum.IsDefined(gender) ||
			string.IsNullOrWhiteSpace(input.GuardianFirstName) ||
			!Enum.IsDefined(input.GuardianRelationship))
			throw new BusinessException(AdmissionErrorCodes.InquiryIdentityIncomplete,
				"Applicant and primary guardian identity is incomplete.");
		var phone = AdmissionInquiryPhoneValidator.NormalizeAndValidateIndianMobile(input.GuardianMobile);
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
		if (family != null && (guardian == null || guardian.FamilyId != family.Id || guardian.Mobile != phone))
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationFamilyMismatch,
				"The selected family's primary guardian does not match this mobile number.");
		if (family != null)
			await familyMatchService.EnsureCanLinkAsync(family.Id, phone);

		if (family == null)
		{
			var accessibleMatches = await familyMatchService.FindAsync(offering.Id, phone);
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
		await applicantRepository.InsertAsync(applicant, autoSave: true);
		var application = await lifecycleService.StartAsync(applicant.Id, offering.Id, inquiryId: null);
		if (selectedCounsellorId.HasValue)
		{
			var created = await applicationRepository.GetAsync(application.Id);
			created.AssignCounsellor(selectedCounsellorId.Value);
			await applicationRepository.UpdateAsync(created, autoSave: true);
			application = AdmissionApplicationMapper.ToDto(created);
		}
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			"DirectRegistrationStarted", "Registration started without an inquiry", applicant.Id, application.Id, null,
			JsonSerializer.Serialize(new
			{
				offering.Track, input.CounsellorId, AssignedCounsellorId = selectedCounsellorId,
				input.ExistingFamilyId, input.CreateSeparateFamily,
				input.SeparateFamilyReason, GuardianRelationship = input.GuardianRelationship.ToString()
			})), autoSave: true);
		return application;
	}

	private async Task<AdmissionApplicationDto> ResolveReplayAsync(
		AdmissionApplicant applicant, StartAdmissionDirectRegistrationDto input)
	{
		var offering = await offeringRepository.FindAsync(input.AdmissionOfferingId);
		if (offering == null || offering.EntryMode is not (AdmissionEntryMode.DirectRegistration or AdmissionEntryMode.Both))
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict);
		var query = await applicationRepository.GetQueryableAsync();
		var application = await AsyncExecuter.FirstOrDefaultAsync(query.Where(item =>
			item.ApplicantId == applicant.Id && item.AdmissionOfferingId == input.AdmissionOfferingId &&
			!item.InquiryId.HasValue));
		if (application == null)
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict,
				"This request ID already belongs to another registration.");
		if (await GetInitialCounsellorAsync(application.Id) != input.CounsellorId)
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict,
				"This request ID was already used with a different counsellor.");
		await applicationScope.EnsureCanAccessAsync(application);
		var family = await familyRepository.FindAsync(applicant.FamilyId);
		var guardian = family?.PrimaryGuardianId.HasValue == true
			? await guardianRepository.FindAsync(family.PrimaryGuardianId.Value) : null;
		var phone = AdmissionInquiryPhoneValidator.NormalizeAndValidateIndianMobile(input.GuardianMobile);
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
			guardian?.Mobile != phone ||
			(input.ExistingFamilyId.HasValue && applicant.FamilyId != input.ExistingFamilyId.Value))
			throw new BusinessException(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict,
				"This request ID was already used for different applicant or family details.");
		return AdmissionApplicationMapper.ToDto(application);
	}

	private static string? Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	private async Task<Guid?> GetInitialCounsellorAsync(Guid applicationId)
	{
		var activity = await activityRepository.FindAsync(item => item.ApplicationId == applicationId &&
			item.ActivityType == "DirectRegistrationStarted");
		if (string.IsNullOrWhiteSpace(activity?.MetadataJson)) return null;
		using var metadata = JsonDocument.Parse(activity.MetadataJson);
		return metadata.RootElement.TryGetProperty("CounsellorId", out var value) &&
			value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id)
			? id : null;
	}
}
