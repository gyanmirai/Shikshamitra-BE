using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentFamilyInfoAppService(
    IStudentRepository studentRepository,
    IRepository<StudentFamilyDetail, Guid> familyDetailRepository,
    EntityValidationService validationService,
    StudentRegistrationMappingHelper mappingHelper,
    StudentScopeAccessService studentScopeAccessService
) : StudentRegistrationBaseAppService(studentRepository, validationService, studentScopeAccessService),
    IStudentFamilyInfoAppService
{
    public async Task<StudentFamilyInfoDto> GetFamilyInfoAsync(Guid studentId)
    {
        var student = await GetScopedRegistrationStudentAsync(
            studentId,
            nameof(GetFamilyInfoAsync),
            StudentRegistrationErrorCodes.InvalidStudentStatusForFamilyInfo,
            includeStudentRecords: true);

        var familyDetail = await familyDetailRepository.FindAsync(studentId);

        var result = new StudentFamilyInfoDto
        {
            StudentId = student.Id,
            Status = student.Status,
            FatherDetails = familyDetail?.FatherDetails != null
                ? mappingHelper.MapParentGuardianDetailsToDto(familyDetail.FatherDetails) : null,
            MotherDetails = familyDetail?.MotherDetails != null
                ? mappingHelper.MapParentGuardianDetailsToDto(familyDetail.MotherDetails) : null,
            GuardianDetails = familyDetail?.GuardianDetails != null
                ? mappingHelper.MapParentGuardianDetailsToDto(familyDetail.GuardianDetails) : null,
            SiblingStudents = new List<SiblingStudentDto>()
        };

        if (familyDetail?.SiblingStudentIds != null && familyDetail.SiblingStudentIds.Any())
        {
            var siblings = await studentRepository
                .GetListAsync(s => familyDetail.SiblingStudentIds.Contains(s.Id));
            var scopedSiblingIds = (await studentScopeAccessService.ApplyCurrentUserScopeAsync(
                    siblings.AsQueryable()))
                .Select(s => s.Id)
                .ToHashSet();

            result.SiblingStudents = siblings
                .Where(s => scopedSiblingIds.Contains(s.Id))
                .Select(s => new SiblingStudentDto
                {
                    StudentId = s.Id,
                    FullName = $"{s.FirstName} {s.MiddleName} {s.LastName}".Trim(),
                    StandardName = s.Standard?.Name,
                    DivisionName = s.Division?.Name,
                    GrNo = s.GrNo
                })
                .ToList();
        }

        return result;
    }

    public async Task<StudentFamilyInfoDto> UpdateFamilyInfoAsync(Guid studentId, UpdateFamilyInfoDto input)
    {
        var student = await GetScopedRegistrationStudentAsync(
            studentId,
            nameof(UpdateFamilyInfoAsync),
            StudentRegistrationErrorCodes.InvalidStudentStatusForFamilyUpdate,
            forProfileWrite: true);

        await validationService.ValidateSiblingStudentsExistAsync(input.SiblingStudentIds);
        if (input.SiblingStudentIds != null)
        {
            await studentScopeAccessService.EnsureCanAccessStudentsAsync(
                input.SiblingStudentIds,
                nameof(UpdateFamilyInfoAsync));
        }

        var fatherDetails = input.FatherDetails != null
            ? mappingHelper.MapDtoToParentGuardianDetails(input.FatherDetails) : null;
        var motherDetails = input.MotherDetails != null
            ? mappingHelper.MapDtoToParentGuardianDetails(input.MotherDetails) : null;
        var guardianDetails = input.GuardianDetails != null
            ? mappingHelper.MapDtoToParentGuardianDetails(input.GuardianDetails) : null;

        var existingFamilyDetail = await familyDetailRepository.FindAsync(studentId);
        var familyDetail = existingFamilyDetail ?? new StudentFamilyDetail(student.Id, student.TenantId);
        var shouldCreateFamilyDetail = existingFamilyDetail == null;
        familyDetail.UpdateFamilyInformation(fatherDetails, motherDetails, guardianDetails, input.SiblingStudentIds);

        if (shouldCreateFamilyDetail)
        {
            await familyDetailRepository.InsertAsync(familyDetail, autoSave: true);
        }
        else
        {
            await familyDetailRepository.UpdateAsync(familyDetail, autoSave: true);
        }

        return await GetFamilyInfoAsync(studentId);
    }
}
