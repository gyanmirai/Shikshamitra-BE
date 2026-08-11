using ShikshaMitra.Library.Members;
using ShikshaMitra.Students.Records.Profiles;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records.Profiles;

public class StudentAdditionalDetailsAppService(
    IStudentRepository studentRepository,
    IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
    EntityValidationService validationService,
    ILibraryMemberAppService libraryMemberAppService,
    StudentScopeAccessService studentScopeAccessService
) : StudentProfileBaseAppService(studentRepository, studentScopeAccessService),
    IStudentAdditionalDetailsAppService
{
    public async Task<StudentAdditionalDetailsDto> GetAdditionalDetailsAsync(Guid studentId)
    {
        var student = await GetScopedStudentAsync(studentId, nameof(GetAdditionalDetailsAsync));

        var additionalDetail = student.AdditionalDetail;

        return new StudentAdditionalDetailsDto
        {
            StudentId = student.Id,
            Status = student.Status,
            IsBPLStudent = additionalDetail?.IsBPLStudent ?? false,
            BPLCardNo = additionalDetail?.BPLCardNo,
            SecondLanguage = additionalDetail?.SecondLanguage,
            HaveDisability = additionalDetail?.HaveDisability ?? false,
            DisabilityDetails = additionalDetail?.DisabilityDetails,
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
        var student = await GetScopedStudentAsync(studentId, nameof(UpdateAdditionalDetailsAsync));

        validationService.ValidateAdditionalDetailExists(student);

        student.AdditionalDetail.UpdateAdditionalDetails(
            input.IsBPLStudent,
            input.BPLCardNo,
            input.SecondLanguage,
            input.HaveDisability,
            input.DisabilityDetails,
            input.EmergencyRelativeName,
            input.EmergencyRelation,
            input.EmergencyContactNo,
            input.IsSingleChild,
            input.IsSingleParentChild,
            input.Category,
            input.IsLibraryMember,
            student.AdditionalDetail.IsTransportMember ?? false
        );

        // Trigger library member creation/removal workflow when membership flag changes
        await libraryMemberAppService.HandleLibraryMemberAsync(studentId, input.IsLibraryMember);
        await studentAdditionalDetailRepository.UpdateAsync(student.AdditionalDetail, autoSave: true);

        return await GetAdditionalDetailsAsync(studentId);
    }
}
