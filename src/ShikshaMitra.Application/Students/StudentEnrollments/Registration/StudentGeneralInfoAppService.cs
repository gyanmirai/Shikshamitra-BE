using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentGeneralInfoAppService(
    IRepository<Student, Guid> studentRepository,
	IRepository<Standard, Guid> standardRepository,
	EntityValidationService validationService
) : StudentRegistrationBaseAppService(studentRepository, validationService),
    IStudentGeneralInfoAppService
{
    public async Task<StudentGeneralDetailsDto> GetRegistrationGeneralDetailsByIdAsync(Guid studentId)
    {
        var student = await studentRepository.GetAsync(studentId);

        validationService.ValidateStudentStatus(
            student,
            RegistrationStatuses,
            StudentRegistrationErrorCodes.InvalidStatusForRegistrationView);

        var result = ObjectMapper.Map<Student, StudentGeneralDetailsDto>(student);

        return result;
    }

    public async Task<StudentGeneralDetailsDto> UpdateGeneralInfoAsync(Guid studentId, UpdateGeneralInfoDto input)
    {
        var student = await studentRepository.GetAsync(studentId);

        validationService.ValidateStudentStatus(
            student,
            RegistrationStatuses,
            StudentRegistrationErrorCodes.InvalidStudentStatusForGeneralInfo);

        student.UpdateGeneralInformation(
            input.FirstName,
            input.MiddleName,
            input.LastName
        );

        student.AdditionalDetail?.UpdateGeneralInformation(
            input.DateOfBirth,
            input.Gender,
            input.ProfilePictureUrl,
            input.AadharNo,
            input.ApparId,
            input.PanNo,
            input.PennNo,
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
            input.FormNumber
        );

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
            input.Pincode
        );

        if (student.Status != StudentLifeCycleStatusEnum.Registration)
        {
            student.ChangeStatus(StudentLifeCycleStatusEnum.Registration);
        }

        await studentRepository.UpdateAsync(student, autoSave: true);

        var result = ObjectMapper.Map<Student, StudentGeneralDetailsDto>(student);

        return result;
    }

    public async Task<StudentGeneralDetailsDto> CreateDirectRegistrationAsync(CreateDirectRegistrationDto createRegistrationDto)
    {
        await validationService.ValidateAcademicMediumExistsAsync(createRegistrationDto.AcademicMediumId);
		await validationService.ValidateStandardExistsAsync(createRegistrationDto.StandardId);

		var studentId = GuidGenerator.Create();

		var standard = await standardRepository.GetAsync(createRegistrationDto.StandardId, includeDetails: true) ?? throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard not found");

		if (standard.EducationMediumStage.EducationMedium.Id != createRegistrationDto.AcademicMediumId)
		{
			throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard does not belong to the specified Academic Medium");
		}

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
            pincode: createRegistrationDto.Pincode
        );

        if (student.AdditionalDetail != null)
        {
            student.AdditionalDetail.UpdateGeneralInformation(
                createRegistrationDto.DateOfBirth,
                createRegistrationDto.Gender,
                createRegistrationDto.ProfilePictureUrl,
                createRegistrationDto.AadharNo,
                createRegistrationDto.ApparId,
                createRegistrationDto.PanNo,
                createRegistrationDto.PennNo,
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
                createRegistrationDto.FormNumber
            );
        }

		student.ChangeStatus(StudentLifeCycleStatusEnum.Registration);

		await studentRepository.InsertAsync(student, autoSave: true);

        var result = ObjectMapper.Map<Student, StudentGeneralDetailsDto>(student);

        return result;
    }
}
