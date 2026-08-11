using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records.Profiles;

/// <summary>
/// Manages the Bank Details section of the student registration form.
/// Covers banking information used for fee payments, refunds, and financial transactions.
/// </summary>
public interface IStudentBankDetailsAppService : IApplicationService
{
    Task<StudentBankDetailsDto> GetBankDetailsAsync(Guid studentId);

    Task<StudentBankDetailsDto> UpdateBankDetailsAsync(Guid studentId, UpdateBankDetailsDto input);
}
