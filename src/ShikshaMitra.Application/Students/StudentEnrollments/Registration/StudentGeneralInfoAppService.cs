using System;
using System.Threading.Tasks;
using ShikshaMitra.Students.Compliance;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentGeneralInfoAppService(
	IStudentRepository studentRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
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
			StudentRegistrationErrorCodes.InvalidStatusForRegistrationView,
			includeStudentRecords: true);

		var result = ObjectMapper.Map<Student, StudentGeneralDetailsDto>(student);
		await ApplyComplianceMaskAsync(studentId, result);

		return result;
	}

	public async Task<StudentGeneralDetailsDto> UpdateGeneralInfoAsync(Guid studentId, UpdateGeneralInfoDto input)
	{
		var student = await GetScopedRegistrationStudentAsync(
			studentId,
			nameof(UpdateGeneralInfoAsync),
			StudentRegistrationErrorCodes.InvalidStudentStatusForGeneralInfo,
			forProfileWrite: true);

		if (student.Status is StudentLifeCycleStatusEnum.Enrolled or StudentLifeCycleStatusEnum.InActive or StudentLifeCycleStatusEnum.Left)
		{
			if ((input.AcademicMediumId.HasValue && input.AcademicMediumId != student.AcademicMediumId) ||
				(input.StandardId.HasValue && input.StandardId != student.StandardId) ||
				(input.DivisionId.HasValue && input.DivisionId != student.DivisionId))
				throw new BusinessException(StudentRegistrationErrorCodes.InvalidStudentStatusForGeneralInfo,
					"Use Student Management to change an enrolled student's placement.");
		}
		else await UpdateAcademicPlacementAsync(student, input);

		await studentScopeAccessService.EnsureCanUseInquiryPreferenceAsync(
			input.InquiryPreferenceId,
			student.AcademicYearId ?? throw new BusinessException(StudentRegistrationErrorCodes.InvalidStudentStatusForGeneralInfo),
			nameof(UpdateGeneralInfoAsync),
			student.AdditionalDetail?.InquiryPreferenceId);

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

		if (student.Status == StudentLifeCycleStatusEnum.RegistrationFormIssued)
		{
			student.ChangeStatus(StudentLifeCycleStatusEnum.Registration);
		}

		await studentRepository.UpdateAsync(student, autoSave: true);
		// The profile form receives masked identity numbers. Keep the verified
		// compliance record intact when an enrolled student's details are edited.
		var compliance = student.Status is StudentLifeCycleStatusEnum.Enrolled or StudentLifeCycleStatusEnum.InActive or StudentLifeCycleStatusEnum.Left
			? await identityComplianceAppService.GetAsync(student.Id)
			: await UpsertComplianceAsync(student.Id, input);

		var result = ObjectMapper.Map<Student, StudentGeneralDetailsDto>(student);
		ApplyComplianceMask(result, compliance);

		return result;
	}

	public async Task<StudentGeneralDetailsDto> CreateDirectRegistrationAsync(CreateDirectRegistrationDto createRegistrationDto)
	{
		var academicMediumId = createRegistrationDto.AcademicMediumId;
		if (academicMediumId == Guid.Empty)
		{
			throw new BusinessException(StudentRegistrationErrorCodes.AcademicMediumNotFound, "Academic medium is required");
		}

		var standardId = createRegistrationDto.StandardId;
		if (standardId == Guid.Empty)
		{
			throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard is required");
		}

		await validationService.ValidateAcademicMediumExistsAsync(academicMediumId);
		await validationService.ValidateStandardExistsAsync(standardId);

		var studentId = GuidGenerator.Create();

		var standard = await standardRepository.GetAsync(standardId, includeDetails: true)
			?? throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard not found");

		if (standard.EducationMediumStage.EducationMedium.Id != academicMediumId)
		{
			throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard does not belong to the specified Academic Medium");
		}

		await studentScopeAccessService.EnsureCanAccessStandardAsync(standardId, nameof(CreateDirectRegistrationAsync));
		if (createRegistrationDto.DivisionId.HasValue)
		{
			await EnsureDivisionMatchesStandardAsync(
				createRegistrationDto.DivisionId.Value,
				standardId,
				nameof(CreateDirectRegistrationAsync));
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
			educationMediumId: academicMediumId,
			educationMediumStageId: standard.EducationMediumStageId,
			standardId: standardId,
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
