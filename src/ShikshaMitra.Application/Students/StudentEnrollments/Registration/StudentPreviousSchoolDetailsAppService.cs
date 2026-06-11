using ShikshaMitra.Students.Enrollments.Registrations;
using System;
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
    public async Task<StudentPreviousSchoolDetailsDto> GetPreviousSchoolDetailsAsync(Guid studentId)
    {
        var student = await GetScopedRegistrationStudentAsync(
            studentId,
            nameof(GetPreviousSchoolDetailsAsync),
            StudentRegistrationErrorCodes.InvalidStudentStatusForPreviousSchool);

        var previousSchoolDetail = await studentPreviousSchoolDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId);

        return new StudentPreviousSchoolDetailsDto
        {
            StudentId = student.Id,
            Status = student.Status,
            SchoolName = previousSchoolDetail?.SchoolName,
            SchoolAddress = previousSchoolDetail?.SchoolAddress,
            Board = previousSchoolDetail?.Board,
            Medium = previousSchoolDetail?.Medium,
            LastClassAttended = previousSchoolDetail?.LastClassAttended,
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
            StudentRegistrationErrorCodes.InvalidStudentStatusForPreviousSchoolUpdate);

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
            input.TCNumber,
            input.DateOfLeaving,
            input.PercentageObtained,
            input.AcademicYear,
            input.ReasonForLeaving,
            input.Remarks
        );

        student.AdditionalDetail?.AdvanceRegistrationState(StudentRegistrationStateEnum.PrevSchoolDetails);

        await UnitOfWorkManager.Current!.SaveChangesAsync();

        return await GetPreviousSchoolDetailsAsync(studentId);
    }
}
