using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.Fees;

/// <summary>
/// Domain manager for <see cref="StudentFeeRecord"/>.
/// Domain analogue of HR's <c>PayrollManager</c>.
///
/// <para>
/// Full implementation is tracked in <c>docs/planning/student/fees-subdomain.md</c>.
/// </para>
/// </summary>
public class StudentFeeManager(IStudentFeeRepository feeRepository) : DomainService
{
    /// <summary>
    /// Creates a new fee record for a student in an academic year.
    /// Throws if a record already exists for the same student+year.
    /// </summary>
    public async Task<StudentFeeRecord> CreateAsync(
        Guid studentId,
        Guid academicYearId)
    {
        var existing = await feeRepository.FindByStudentAndYearAsync(studentId, academicYearId);
        if (existing != null)
            throw new BusinessException(StudentErrorCodes.FeeRecordAlreadyLocked)
                .WithData("StudentId", studentId)
                .WithData("AcademicYearId", academicYearId);

        return new StudentFeeRecord(
            id: GuidGenerator.Create(),
            tenantId: CurrentTenant.Id,
            studentId: studentId,
            academicYearId: academicYearId);
    }

    /// <summary>
    /// Locks a fee record so that no further edits are possible.
    /// Mirrors <c>PayrollManager.LockAsync</c>.
    /// </summary>
    public async Task<StudentFeeRecord> LockAsync(Guid feeRecordId)
    {
        var record = await feeRepository.GetAsync(feeRecordId);
        record.Lock();
        return record;
    }
}
