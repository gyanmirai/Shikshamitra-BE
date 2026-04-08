using ShikshaMitra.Students.Enrollments.Registrations;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentBankDetailsAppService(
    IRepository<Student, Guid> studentRepository,
    IRepository<StudentBankDetail, Guid> studentBankDetailRepository,
    EntityValidationService validationService
) : StudentRegistrationBaseAppService(studentRepository, validationService),
    IStudentBankDetailsAppService
{
    public async Task<StudentBankDetailsDto> GetBankDetailsAsync(Guid studentId)
    {
        var student = await studentRepository.GetAsync(studentId);

        validationService.ValidateStudentStatus(
            student,
            RegistrationStatuses,
            StudentRegistrationErrorCodes.InvalidStudentStatusForBankDetails);

        var bankDetail = await studentBankDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId);

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
        var student = await studentRepository.GetAsync(studentId);

        validationService.ValidateStudentStatus(
            student,
            RegistrationStatuses,
            StudentRegistrationErrorCodes.InvalidStudentStatusForBankDetailsUpdate);

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

        student.AdditionalDetail?.AdvanceRegistrationState(StudentRegistrationStateEnum.BankDetails);

        await UnitOfWorkManager.Current!.SaveChangesAsync();

        return await GetBankDetailsAsync(studentId);
    }
}
