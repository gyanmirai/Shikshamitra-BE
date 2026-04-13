using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Fees;

/// <summary>
/// Repository interface for the <see cref="StudentFeeRecord"/> aggregate root.
/// Domain analogue of HR's <c>ISalarySlipRepository</c>.
/// </summary>
public interface IStudentFeeRepository : IRepository<StudentFeeRecord, Guid>
{
    /// <summary>
    /// Finds an existing fee record for a student in a specific academic year.
    /// Returns <c>null</c> if no record exists.
    /// </summary>
    Task<StudentFeeRecord?> FindByStudentAndYearAsync(
        Guid studentId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);
}
