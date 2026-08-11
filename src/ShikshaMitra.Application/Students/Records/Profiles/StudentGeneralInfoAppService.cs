using System;
using System.Threading.Tasks;
using ShikshaMitra.Students.Compliance;
using ShikshaMitra.Students.Records.Profiles;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records.Profiles;

public class StudentGeneralInfoAppService(
	IStudentRepository studentRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	EntityValidationService validationService,
	StudentScopeAccessService studentScopeAccessService,
	StudentIdentityComplianceAppService identityComplianceAppService
) : StudentProfileBaseAppService(studentRepository, studentScopeAccessService),
	IStudentGeneralInfoAppService
{
	public async Task<StudentGeneralDetailsDto> GetGeneralDetailsByIdAsync(Guid studentId)
	{
		var student = await GetScopedStudentAsync(studentId, nameof(GetGeneralDetailsByIdAsync));

		var result = ObjectMapper.Map<Student, StudentGeneralDetailsDto>(student);
		await ApplyComplianceMaskAsync(studentId, result);

		return result;
	}

	public async Task<StudentGeneralDetailsDto> UpdateGeneralInfoAsync(Guid studentId, UpdateGeneralInfoDto input)
	{
		var student = await GetScopedStudentAsync(studentId, nameof(UpdateGeneralInfoAsync));

		await UpdateAcademicPlacementAsync(student, input);

		await studentScopeAccessService.EnsureCanUseInquirySourceAsync(
			input.InquirySourceId,
			student.AcademicYearId ?? throw new BusinessException(StudentRegistrationErrorCodes.InvalidStudentStatusForGeneralInfo),
			null,
			nameof(UpdateGeneralInfoAsync),
			student.AdditionalDetail?.InquirySourceId);

		student.UpdateGeneralInformation(input.FirstName, input.MiddleName, input.LastName);

		student.AdditionalDetail?.UpdateGeneralInformation(
			input.DateOfBirth,
			input.Gender,
			input.ProfilePictureUrl,
			null,
			null,
			input.PanNo,
			null,
			input.Religion,
			input.FamilyCast,
			input.CastCategory,
			input.Nationality,
			input.BirthPlace,
			input.MotherTongue,
			input.PreviousSchool,
			input.LastClassPassed,
			StudentRegistrationStateEnum.General,
			input.InquirySourceId,
			input.FormNumber);

		student.ContactDetail?.UpdateContactInfo(
			input.PrimaryContactNo,
			input.SecondaryContactNo,
			input.Email,
			input.Address1,
			input.Address2,
			input.Address3,
			input.City,
			input.State,
			input.Country,
			input.Pincode);

		if (student.Status == StudentLifeCycleStatusEnum.RegistrationFormIssued)
		{
			student.ChangeStatus(StudentLifeCycleStatusEnum.Registration);
		}

		await studentRepository.UpdateAsync(student, autoSave: true);
		var compliance = await UpsertComplianceAsync(student.Id, input);

		var result = ObjectMapper.Map<Student, StudentGeneralDetailsDto>(student);
		ApplyComplianceMask(result, compliance);

		return result;
	}

	private async Task ApplyComplianceMaskAsync(Guid studentId, StudentGeneralDetailsDto result)
	{
		var compliance = await identityComplianceAppService.GetAsync(studentId);
		ApplyComplianceMask(result, compliance);
	}

	private async Task UpdateAcademicPlacementAsync(Student student, UpdateGeneralInfoDto input)
	{
		if (!input.AcademicMediumId.HasValue &&
		    !input.StandardId.HasValue &&
		    !input.DivisionId.HasValue)
		{
			return;
		}

		var academicMediumId = input.AcademicMediumId ?? student.AcademicMediumId;
		var standardId = input.StandardId ?? student.StandardId
			?? throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard is required");

		await validationService.ValidateAcademicMediumExistsAsync(academicMediumId);
		await validationService.ValidateStandardExistsAsync(standardId);

		var standard = await standardRepository.GetAsync(standardId, includeDetails: true)
			?? throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard not found");

		if (standard.EducationMediumStage.EducationMedium.Id != academicMediumId)
		{
			throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard does not belong to the specified Academic Medium");
		}

		await studentScopeAccessService.EnsureCanAccessStandardAsync(standardId, nameof(UpdateGeneralInfoAsync));

		Guid? divisionId = input.DivisionId;
		if (divisionId.HasValue)
		{
			await EnsureDivisionMatchesStandardAsync(
				divisionId.Value,
				standardId,
				nameof(UpdateGeneralInfoAsync));
		}
		else if (student.StandardId == standardId)
		{
			divisionId = student.DivisionId;
		}

		student.UpdateAcademicPlacement(
			standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId,
			standard.EducationMediumStage.EducationMedium.EducationBoardId,
			academicMediumId,
			standard.EducationMediumStageId,
			standardId,
			divisionId);
	}

	private async Task EnsureDivisionMatchesStandardAsync(Guid divisionId, Guid standardId, string operation)
	{
		var division = await divisionRepository.GetAsync(divisionId);
		if (division.StandardId != standardId)
		{
			throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Division does not belong to the selected Standard");
		}

		await studentScopeAccessService.EnsureCanAccessDivisionAsync(divisionId, operation);
	}

	private static void ApplyComplianceMask(StudentGeneralDetailsDto result, StudentIdentityComplianceDto compliance)
	{
		result.AdharNo = compliance.AadhaarMasked;
		result.AadhaarMasked = compliance.AadhaarMasked;
		result.AadhaarVerificationStatus = compliance.AadhaarVerificationStatus;
		result.ApparId = compliance.ApaarId;
		result.ApaarConsentStatus = compliance.ApaarConsentStatus;
		result.ApaarVerificationStatus = compliance.ApaarVerificationStatus;
		result.PennNo = compliance.PenNumber;
		result.UdiseStudentCode = compliance.UdiseStudentCode;
		result.UdiseSyncStatus = compliance.UdiseSyncStatus;
		result.RteStatus = compliance.RteStatus;
		result.ScholarshipStatus = compliance.ScholarshipStatus;
	}

	private Task<StudentIdentityComplianceDto> UpsertComplianceAsync(Guid studentId, RegistrationGeneralInfoBaseDto input) =>
		identityComplianceAppService.UpsertFromRegistrationAsync(studentId, new UpdateStudentIdentityComplianceDto
		{
			AadhaarNo = input.AadharNo,
			AadhaarName = BuildStudentName(input.FirstName, input.MiddleName, input.LastName),
			AadhaarVerificationStatus = string.IsNullOrWhiteSpace(input.AadharNo) ? IdentityVerificationStatus.NotProvided : IdentityVerificationStatus.Pending,
			ApaarId = input.ApparId,
			ApaarVerificationStatus = string.IsNullOrWhiteSpace(input.ApparId) ? IdentityVerificationStatus.NotProvided : IdentityVerificationStatus.Pending,
			PenNumber = input.PennNo,
			UdiseSyncStatus = UdiseSyncStatus.NotReady
		});

	private static string BuildStudentName(string firstName, string? middleName, string lastName) =>
		$"{firstName} {middleName} {lastName}".Replace("  ", " ").Trim();
}
