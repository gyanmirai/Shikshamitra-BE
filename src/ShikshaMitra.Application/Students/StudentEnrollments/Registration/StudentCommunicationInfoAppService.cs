using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentCommunicationInfoAppService(
    IStudentRepository studentRepository,
    IRepository<StudentCommunicationDetail, Guid> studentCommunicationDetailRepository,
    EntityValidationService validationService,
    StudentRegistrationMappingHelper mappingHelper,
    StudentScopeAccessService studentScopeAccessService
) : StudentRegistrationBaseAppService(studentRepository, validationService, studentScopeAccessService),
    IStudentCommunicationInfoAppService
{
    public async Task<StudentCommunicationInfoDto> GetCommunicationInfoAsync(Guid studentId)
    {
        var student = await GetScopedRegistrationStudentAsync(
            studentId,
            nameof(GetCommunicationInfoAsync),
            StudentRegistrationErrorCodes.InvalidStudentStatusForCommunicationInfo,
            includeStudentRecords: true);

        var communicationDetail = await studentCommunicationDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId);

        return new StudentCommunicationInfoDto
        {
            StudentId = student.Id,
            Status = student.Status,
            LocalAddress = communicationDetail?.LocalAddress != null
                ? mappingHelper.MapAddressDetailsToDto(communicationDetail.LocalAddress) : null,
            PermanentAddress = communicationDetail?.PermanentAddress != null
                ? mappingHelper.MapAddressDetailsToDto(communicationDetail.PermanentAddress) : null,
            EmergencyAddress = communicationDetail?.EmergencyAddress != null
                ? mappingHelper.MapAddressDetailsToDto(communicationDetail.EmergencyAddress) : null,
            IsPermanentSameAsLocal = communicationDetail?.IsPermanentSameAsLocal ?? false,
            IsEmergencySameAsLocal = communicationDetail?.IsEmergencySameAsLocal ?? false,
            EmergencyContactName = communicationDetail?.EmergencyContactName,
            EmergencyContactPhone = communicationDetail?.EmergencyContactPhone,
            EmergencyContactRelation = communicationDetail?.EmergencyContactRelation
        };
    }

    public async Task<StudentCommunicationInfoDto> UpdateCommunicationInfoAsync(Guid studentId, UpdateCommunicationInfoDto input)
    {
        var student = await GetScopedRegistrationStudentAsync(
            studentId,
            nameof(UpdateCommunicationInfoAsync),
            StudentRegistrationErrorCodes.InvalidStudentStatusForCommunicationUpdate,
            forProfileWrite: true);

        var communicationDetail = await studentCommunicationDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId);

        if (communicationDetail == null)
        {
            communicationDetail = new StudentCommunicationDetail(studentId, CurrentTenant.Id);
            await studentCommunicationDetailRepository.InsertAsync(communicationDetail, autoSave: false);
        }

        var localAddress = input.LocalAddress != null ? mappingHelper.MapDtoToAddressDetails(input.LocalAddress) : null;
        var permanentAddress = input.PermanentAddress != null ? mappingHelper.MapDtoToAddressDetails(input.PermanentAddress) : null;
        var emergencyAddress = input.EmergencyAddress != null ? mappingHelper.MapDtoToAddressDetails(input.EmergencyAddress) : null;

        communicationDetail.UpdateCommunicationInformation(
            localAddress,
            permanentAddress,
            emergencyAddress,
            input.IsPermanentSameAsLocal,
            input.IsEmergencySameAsLocal,
            input.EmergencyContactName,
            input.EmergencyContactPhone,
            input.EmergencyContactRelation
        );

        if (Array.Exists(RegistrationStatuses, status => status == student.Status))
            student.AdditionalDetail?.AdvanceRegistrationState(StudentRegistrationStateEnum.Communication);

        await UnitOfWorkManager.Current!.SaveChangesAsync();

        return await GetCommunicationInfoAsync(studentId);
    }
}
