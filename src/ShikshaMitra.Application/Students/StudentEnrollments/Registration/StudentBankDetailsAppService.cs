using ShikshaMitra.Students.Enrollments.Registrations;
using System;
using ShikshaMitra.Students.Records;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentBankDetailsAppService(
    IStudentRepository studentRepository,
    IRepository<StudentBankDetail, Guid> studentBankDetailRepository,
    EntityValidationService validationService,
    StudentScopeAccessService studentScopeAccessService
) : StudentRegistrationBaseAppService(studentRepository, validationService, studentScopeAccessService),
    IStudentBankDetailsAppService
{
    public Task<StudentBankDetailsDto> GetBankDetailsAsync(Guid studentId) => ReadAsync(studentId, null);
    internal Task<StudentBankDetailsDto> GetBankDetailsAsync(Guid studentId, StudentRecordSectionReadScope scope) => ReadAsync(studentId, scope);
    private async Task<StudentBankDetailsDto> ReadAsync(Guid studentId, StudentRecordSectionReadScope? scope)
    {
        var student = await GetScopedRegistrationStudentAsync(
            studentId,
            nameof(GetBankDetailsAsync),
            StudentRegistrationErrorCodes.InvalidStudentStatusForBankDetails,
            includeStudentRecords: true);

        scope?.RequireContext(studentId, CurrentTenant.Id, CurrentUser.Id, CurrentUnitOfWork);
        var bankDetail = scope == null ? await studentBankDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId) :
            await scope.ReadAsync("bank", () => studentBankDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId));

        return new StudentBankDetailsDto
        {
            StudentId = student.Id,
            Status = student.Status,
            BankName = bankDetail?.BankName,
            AccountNo = bankDetail?.AccountNo,
            IFSCCode = bankDetail?.IFSCCode,
            BranchName = bankDetail?.BranchName,
            AccountHolderName = bankDetail?.AccountHolderName,
            AccountType = bankDetail?.AccountType,
            BankAddress = bankDetail?.BankAddress,
            MICRCode = bankDetail?.MICRCode,
            SwiftCode = bankDetail?.SwiftCode
        };
    }

    public async Task<StudentBankDetailsDto> UpdateBankDetailsAsync(Guid studentId, UpdateBankDetailsDto input)
    {
        var student = await GetScopedRegistrationStudentAsync(
            studentId,
            nameof(UpdateBankDetailsAsync),
            StudentRegistrationErrorCodes.InvalidStudentStatusForBankDetailsUpdate,
            forProfileWrite: true);

        var bankDetail = await studentBankDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId);

        if (bankDetail == null)
        {
            bankDetail = new StudentBankDetail(studentId, CurrentTenant.Id);
            await studentBankDetailRepository.InsertAsync(bankDetail, autoSave: false);
        }

        bankDetail.UpdateBankDetails(
            input.BankName,
            input.AccountNo,
            input.IFSCCode,
            input.BranchName,
            input.AccountHolderName,
            input.AccountType,
            input.BankAddress,
            input.MICRCode,
            input.SwiftCode
        );

        if (Array.Exists(RegistrationStatuses, status => status == student.Status))
            student.AdditionalDetail?.AdvanceRegistrationState(StudentRegistrationStateEnum.BankDetails);

        await UnitOfWorkManager.Current!.SaveChangesAsync();

        return await GetBankDetailsAsync(studentId);
    }
}
