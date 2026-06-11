using System;
using System.Threading.Tasks;
using ShikshaMitra.Students.Compliance;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentGeneralInfoAppService(
	IStudentRepository studentRepository,
	IRepository<Standard, Guid> standardRepository,
	EntityValidationService validationService,
	StudentScopeAccessService studentScopeAccessService,
	StudentIdentityComplianceAppService identityComplianceAppService
) : StudentRegistrationBaseAppService(studentRepository, validationService, studentScopeAccessService),
	IStudentGeneralInfoAppService
{
	public async Task<StudentGeneralDetailsDto> GetRegistrationGeneralDetailsByIdAsync(Guid studentId)
	{
		var student = await GetScopedRegistrationStudentAsync(
			studentId,
			nameof(GetRegistrationGeneralDetailsByIdAsync),
			StudentRegistrationErrorCodes.InvalidStatusForRegistrationView);

		var result = ObjectMapper.Map<Student, StudentGeneralDetailsDto>(student);
		await ApplyComplianceMaskAsync(studentId, result);

		return result;
	}

	public async Task<StudentGeneralDetailsDto> UpdateGeneralInfoAsync(Guid studentId, UpdateGeneralInfoDto input)
	{
		var student = await GetScopedRegistrationStudentAsync(
			studentId,
			nameof(UpdateGeneralInfoAsync),
			StudentRegistrationErrorCodes.InvalidStudentStatusForGeneralInfo);
		await studentScopeAccessService.EnsureCanUseInquiryPreferenceAsync(
			input.InquiryPreferenceId,
			student.AcademicYearId ?? throw new BusinessException(StudentRegistrationErrorCodes.InvalidStudentStatusForGeneralInfo),
			nameof(UpdateGeneralInfoAsync));

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
			input.InquiryPreferenceId,
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

		if (student.Status != StudentLifeCycleStatusEnum.Registration)
		{
			student.ChangeStatus(StudentLifeCycleStatusEnum.Registration);
		}

		await studentRepository.UpdateAsync(student, autoSave: true);
		var compliance = await UpsertComplianceAsync(student.Id, input);

		var result = ObjectMapper.Map<Student, StudentGeneralDetailsDto>(student);
		ApplyComplianceMask(result, compliance);

		return result;
	}

	public async Task<StudentGeneralDetailsDto> CreateDirectRegistrationAsync(CreateDirectRegistrationDto createRegistrationDto)
	{
		await validationService.ValidateAcademicMediumExistsAsync(createRegistrationDto.AcademicMediumId);
		await validationService.ValidateStandardExistsAsync(createRegistrationDto.StandardId);

		var studentId = GuidGenerator.Create();

		var standard = await standardRepository.GetAsync(createRegistrationDto.StandardId, includeDetails: true)
			?? throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard not found");

		if (standard.EducationMediumStage.EducationMedium.Id != createRegistrationDto.AcademicMediumId)
		{
			throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard does not belong to the specified Academic Medium");
		}

		await studentScopeAccessService.EnsureCanAccessStandardAsync(createRegistrationDto.StandardId, nameof(CreateDirectRegistrationAsync));
		if (createRegistrationDto.DivisionId.HasValue)
		{
			await studentScopeAccessService.EnsureCanAccessDivisionAsync(createRegistrationDto.DivisionId.Value, nameof(CreateDirectRegistrationAsync));
		}

		await studentScopeAccessService.EnsureCanUseInquiryPreferenceAsync(
			createRegistrationDto.InquiryPreferenceId,
			standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId,
			nameof(CreateDirectRegistrationAsync));

		var student = Student.CreateDirectRegistration(
			id: studentId,
			tenantId: CurrentTenant.Id,
			firstName: createRegistrationDto.FirstName,
			middleName: createRegistrationDto.MiddleName,
			lastName: createRegistrationDto.LastName,
			educationYearId: standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId,
			educationBoardId: standard.EducationMediumStage.EducationMedium.EducationBoardId,
			educationMediumId: createRegistrationDto.AcademicMediumId,
			educationMediumStageId: standard.EducationMediumStageId,
			standardId: createRegistrationDto.StandardId,
			divisionId: createRegistrationDto.DivisionId,
			dateOfBirth: createRegistrationDto.DateOfBirth,
			gender: createRegistrationDto.Gender,
			primaryContactNo: createRegistrationDto.PrimaryContactNo,
			secondaryContactNo: createRegistrationDto.SecondaryContactNo,
			email: createRegistrationDto.Email,
			address1: createRegistrationDto.Address1,
			address2: createRegistrationDto.Address2,
			address3: createRegistrationDto.Address3,
			city: createRegistrationDto.City,
			state: createRegistrationDto.State,
			country: createRegistrationDto.Country,
			pincode: createRegistrationDto.Pincode);

		student.AdditionalDetail?.UpdateGeneralInformation(
			createRegistrationDto.DateOfBirth,
			createRegistrationDto.Gender,
			createRegistrationDto.ProfilePictureUrl,
			null,
			null,
			createRegistrationDto.PanNo,
			null,
			createRegistrationDto.Religion,
			createRegistrationDto.FamilyCast,
			createRegistrationDto.CastCategory,
			createRegistrationDto.Nationality,
			createRegistrationDto.BirthPlace,
			createRegistrationDto.MotherTongue,
			createRegistrationDto.PreviousSchool,
			createRegistrationDto.LastClassPassed,
			StudentRegistrationStateEnum.General,
			createRegistrationDto.InquiryPreferenceId,
			createRegistrationDto.FormNumber);

		student.ChangeStatus(StudentLifeCycleStatusEnum.Registration);

		await studentRepository.InsertAsync(student, autoSave: true);
		var compliance = await UpsertComplianceAsync(student.Id, createRegistrationDto);

		var result = ObjectMapper.Map<Student, StudentGeneralDetailsDto>(student);
		ApplyComplianceMask(result, compliance);

		return result;
	}

	private async Task ApplyComplianceMaskAsync(Guid studentId, StudentGeneralDetailsDto result)
	{
		var compliance = await identityComplianceAppService.GetAsync(studentId);
		ApplyComplianceMask(result, compliance);
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

	private Task<StudentIdentityComplianceDto> UpsertComplianceAsync(Guid studentId, UpdateGeneralInfoDto input) =>
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

	private Task<StudentIdentityComplianceDto> UpsertComplianceAsync(Guid studentId, CreateDirectRegistrationDto input) =>
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
