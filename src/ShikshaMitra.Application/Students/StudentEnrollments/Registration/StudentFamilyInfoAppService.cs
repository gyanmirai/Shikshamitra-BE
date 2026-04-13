using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentFamilyInfoAppService(
    IRepository<Student, Guid> studentRepository,
    EntityValidationService validationService,
    StudentRegistrationMappingHelper mappingHelper
) : StudentRegistrationBaseAppService(studentRepository, validationService),
    IStudentFamilyInfoAppService
{
    public async Task<StudentFamilyInfoDto> GetFamilyInfoAsync(Guid studentId)
    {
        var student = await studentRepository.GetAsync(studentId);

        validationService.ValidateStudentStatus(
            student,
            RegistrationStatuses,
            StudentRegistrationErrorCodes.InvalidStudentStatusForFamilyInfo);

        var familyDetail = student.FamilyDetail;

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

            result.SiblingStudents = siblings
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
        var student = await studentRepository.GetAsync(studentId);

        validationService.ValidateStudentStatus(
            student,
            RegistrationStatuses,
            StudentRegistrationErrorCodes.InvalidStudentStatusForFamilyUpdate);

        await validationService.ValidateSiblingStudentsExistAsync(input.SiblingStudentIds);

        var fatherDetails = input.FatherDetails != null
            ? mappingHelper.MapDtoToParentGuardianDetails(input.FatherDetails) : null;
        var motherDetails = input.MotherDetails != null
            ? mappingHelper.MapDtoToParentGuardianDetails(input.MotherDetails) : null;
        var guardianDetails = input.GuardianDetails != null
            ? mappingHelper.MapDtoToParentGuardianDetails(input.GuardianDetails) : null;

        student.UpdateFamilyInfo(fatherDetails, motherDetails, guardianDetails, input.SiblingStudentIds);

        await studentRepository.UpdateAsync(student, autoSave: true);

        return await GetFamilyInfoAsync(studentId);
    }
}
