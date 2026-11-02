using ShikshaMitra.Students.Enrollments.Registrations;
using System;
using ShikshaMitra.Students.Records;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentPreviousSchoolDetailsAppService(
    IStudentRepository studentRepository,
    IRepository<StudentPreviousSchoolDetail, Guid> studentPreviousSchoolDetailRepository,
    EntityValidationService validationService,
    StudentScopeAccessService studentScopeAccessService
) : StudentRegistrationBaseAppService(studentRepository, validationService, studentScopeAccessService),
    IStudentPreviousSchoolDetailsAppService
{
    public Task<StudentPreviousSchoolDetailsDto> GetPreviousSchoolDetailsAsync(Guid studentId) => ReadAsync(studentId, null);
    internal Task<StudentPreviousSchoolDetailsDto> GetPreviousSchoolDetailsAsync(Guid studentId, StudentRecordSectionReadScope scope) => ReadAsync(studentId, scope);
    private async Task<StudentPreviousSchoolDetailsDto> ReadAsync(Guid studentId, StudentRecordSectionReadScope? scope)
    {
        var student = await GetScopedRegistrationStudentAsync(
            studentId,
            nameof(GetPreviousSchoolDetailsAsync),
            StudentRegistrationErrorCodes.InvalidStudentStatusForPreviousSchool,
            includeStudentRecords: true);

        scope?.RequireContext(studentId, CurrentTenant.Id, CurrentUser.Id, CurrentUnitOfWork);
        var previousSchoolDetail = scope == null ? await studentPreviousSchoolDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId) :
            await scope.ReadAsync("previous-school", () => studentPreviousSchoolDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId));

        return new StudentPreviousSchoolDetailsDto
        {
            StudentId = student.Id,
            Status = student.Status,
            SchoolName = previousSchoolDetail?.SchoolName,
            PreviousSchool = student.AdditionalDetail?.PreviousSchool,
            SchoolAddress = previousSchoolDetail?.SchoolAddress,
            Board = previousSchoolDetail?.Board,
            Medium = previousSchoolDetail?.Medium,
            LastClassAttended = previousSchoolDetail?.LastClassAttended,
            LastClassPassed = student.AdditionalDetail?.LastClassPassed,
            SchoolIndexNumber = previousSchoolDetail?.SchoolIndexNumber,
            TCNumber = previousSchoolDetail?.TCNumber,
            DateOfLeaving = previousSchoolDetail?.DateOfLeaving,
            PercentageObtained = previousSchoolDetail?.PercentageObtained,
            AcademicYear = previousSchoolDetail?.AcademicYear,
            ReasonForLeaving = previousSchoolDetail?.ReasonForLeaving,
            Remarks = previousSchoolDetail?.Remarks
        };
    }

    public async Task<StudentPreviousSchoolDetailsDto> UpdatePreviousSchoolDetailsAsync(Guid studentId, UpdatePreviousSchoolDetailsDto input)
    {
        var student = await GetScopedRegistrationStudentAsync(
            studentId,
            nameof(UpdatePreviousSchoolDetailsAsync),
            StudentRegistrationErrorCodes.InvalidStudentStatusForPreviousSchoolUpdate,
            forProfileWrite: true);

        var previousSchoolDetail = await studentPreviousSchoolDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId);

        if (previousSchoolDetail == null)
        {
            previousSchoolDetail = new StudentPreviousSchoolDetail(studentId, CurrentTenant.Id);
            await studentPreviousSchoolDetailRepository.InsertAsync(previousSchoolDetail, autoSave: false);
        }

        previousSchoolDetail.UpdatePreviousSchoolDetails(
            input.SchoolName,
            input.SchoolAddress,
            input.Board,
            input.Medium,
            input.LastClassAttended,
            input.SchoolIndexNumber,
            input.TCNumber,
            input.DateOfLeaving,
            input.PercentageObtained,
            input.AcademicYear,
            input.ReasonForLeaving,
            input.Remarks
        );

        if (Array.Exists(RegistrationStatuses, status => status == student.Status))
            student.AdditionalDetail?.AdvanceRegistrationState(StudentRegistrationStateEnum.PrevSchoolDetails);
        student.AdditionalDetail?.UpdatePreviousSchoolSummary(input.PreviousSchool, input.LastClassPassed);

        await UnitOfWorkManager.Current!.SaveChangesAsync();

        return await GetPreviousSchoolDetailsAsync(studentId);
    }
}
