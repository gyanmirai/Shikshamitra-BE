using ShikshaMitra.Students.Records.Profiles;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records.Profiles;

public class StudentPreviousSchoolDetailsAppService(
    IStudentRepository studentRepository,
    IRepository<StudentPreviousSchoolDetail, Guid> studentPreviousSchoolDetailRepository,
    EntityValidationService validationService,
    StudentScopeAccessService studentScopeAccessService
) : StudentProfileBaseAppService(studentRepository, studentScopeAccessService),
    IStudentPreviousSchoolDetailsAppService
{
    public async Task<StudentPreviousSchoolDetailsDto> GetPreviousSchoolDetailsAsync(Guid studentId)
    {
        var student = await GetScopedStudentAsync(studentId, nameof(GetPreviousSchoolDetailsAsync));

        var previousSchoolDetail = await studentPreviousSchoolDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId);

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
        var student = await GetScopedStudentAsync(studentId, nameof(UpdatePreviousSchoolDetailsAsync));

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

        student.AdditionalDetail?.AdvanceRegistrationState(StudentRegistrationStateEnum.PrevSchoolDetails);
        student.AdditionalDetail?.UpdatePreviousSchoolSummary(input.PreviousSchool, input.LastClassPassed);

        await UnitOfWorkManager.Current!.SaveChangesAsync();

        return await GetPreviousSchoolDetailsAsync(studentId);
    }
}
