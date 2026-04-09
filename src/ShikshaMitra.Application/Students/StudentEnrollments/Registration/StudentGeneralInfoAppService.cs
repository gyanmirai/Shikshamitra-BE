using ShikshaMitra.Students.Enrollments.Registrations;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentGeneralInfoAppService(
    IRepository<Student, Guid> studentRepository,
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

        if (student.Status != StudentLifeCycleStatusEnum.Registation)
        {
            student.ChangeStatus(StudentLifeCycleStatusEnum.Registation);
        }

        await studentRepository.UpdateAsync(student, autoSave: true);

        var result = ObjectMapper.Map<Student, StudentGeneralDetailsDto>(student);

        return result;
    }

    public async Task<StudentGeneralDetailsDto> CreateDirectRegistrationAsync(CreateDirectRegistrationDto input)
    {
        await validationService.ValidateAcademicMediumExistsAsync(input.AcademicMediumId);

        var studentId = GuidGenerator.Create();

        var student = Student.CreateDirectRegistration(
            id: studentId,
            tenantId: CurrentTenant.Id,
            firstName: input.FirstName,
            middleName: input.MiddleName,
            lastName: input.LastName,
            educationYearId: Guid.Empty, // TODO
            educationBoardId: Guid.Empty, // TODO
            educationMediumId: input.AcademicMediumId,
            educationMediumStageId: Guid.Empty, // TODO
            standardId: input.StandardId,
            divisionId: input.DivisionId,
            dateOfBirth: input.DateOfBirth,
            gender: input.Gender,
            primaryContactNo: input.PrimaryContactNo,
            secondaryContactNo: input.SecondaryContactNo,
            email: input.Email,
            address1: input.Address1,
            address2: input.Address2,
            address3: input.Address3,
            city: input.City,
            state: input.State,
            country: input.Country,
            pincode: input.Pincode
        );

        if (student.AdditionalDetail != null)
        {
            student.AdditionalDetail.UpdateGeneralInformation(
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
        }

        await studentRepository.InsertAsync(student, autoSave: true);

        var result = ObjectMapper.Map<Student, StudentGeneralDetailsDto>(student);

        return result;
    }
}
