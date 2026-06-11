using ShikshaMitra.Library.Members;
using ShikshaMitra.Students.Enrollments.Registrations;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentAdditionalDetailsAppService(
    IStudentRepository studentRepository,
    IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
    EntityValidationService validationService,
    ILibraryMemberAppService libraryMemberAppService,
    StudentScopeAccessService studentScopeAccessService
) : StudentRegistrationBaseAppService(studentRepository, validationService, studentScopeAccessService),
    IStudentAdditionalDetailsAppService
{
    public async Task<StudentAdditionalDetailsDto> GetAdditionalDetailsAsync(Guid studentId)
    {
        var student = await GetScopedRegistrationStudentAsync(
            studentId,
            nameof(GetAdditionalDetailsAsync),
            StudentRegistrationErrorCodes.InvalidStudentStatusForAdditionalDetails);

        var additionalDetail = student.AdditionalDetail;

        return new StudentAdditionalDetailsDto
        {
            StudentId = student.Id,
            Status = student.Status,
            IsBPLStudent = additionalDetail?.IsBPLStudent ?? false,
            BPLCardNo = additionalDetail?.BPLCardNo,
            SecondLanguage = additionalDetail?.SecondLanguage,
            HaveDisability = additionalDetail?.HaveDisability ?? false,
            EmergencyRelativeName = additionalDetail?.EmergencyRelationName,
            EmergencyRelation = additionalDetail?.EmergencyRelationType,
            EmergencyContactNo = additionalDetail?.EmergencyContactNo,
            IsSingleChild = additionalDetail?.IsSingleChild ?? false,
            IsSingleParentChild = additionalDetail?.IsSingleParentChild ?? false,
            IsLibraryMember = additionalDetail?.IsLibraryMember ?? false,
            IsTransportMember = additionalDetail?.IsTransportMember ?? false
        };
    }

    public async Task<StudentAdditionalDetailsDto> UpdateAdditionalDetailsAsync(Guid studentId, UpdateAdditionalDetailsDto input)
    {
        var student = await GetScopedRegistrationStudentAsync(
            studentId,
            nameof(UpdateAdditionalDetailsAsync),
            StudentRegistrationErrorCodes.InvalidStudentStatusForAdditionalDetailsUpdate);

        validationService.ValidateAdditionalDetailExists(student);

        student.AdditionalDetail.UpdateAdditionalDetails(
            input.IsBPLStudent,
            input.BPLCardNo,
            input.SecondLanguage,
            input.HaveDisability,
            input.EmergencyRelativeName,
            input.EmergencyRelation,
            input.EmergencyContactNo,
            input.IsSingleChild,
            input.IsSingleParentChild,
            input.Category,
            input.IsLibraryMember,
            input.IsTransportMember
        );

        // Trigger library member creation/removal workflow when membership flag changes
        await libraryMemberAppService.HandleLibraryMemberAsync(studentId, input.IsLibraryMember);

        // TODO: trigger transport member creation/removal workflow when membership flag changes

        await studentAdditionalDetailRepository.UpdateAsync(student.AdditionalDetail, autoSave: true);

        return await GetAdditionalDetailsAsync(studentId);
    }
}
