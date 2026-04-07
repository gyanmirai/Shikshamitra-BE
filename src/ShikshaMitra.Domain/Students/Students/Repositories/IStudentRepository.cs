using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students;

/// <summary>
/// Repository interface for the <see cref="Student"/> aggregate root.
/// Mirrors <c>IEmployeeRepository</c> in the HR module.
/// </summary>
public interface IStudentRepository : IRepository<Student, Guid>
{
    /// <summary>
    /// Gets a student with all 1:1 aggregate parts eagerly loaded
    /// (AdditionalDetail, ContactDetail, FamilyDetail, CommunicationDetail,
    ///  MedicalRecord, PreviousSchoolDetail, BankDetail, Documents, Notes).
    /// </summary>
    Task<Student?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a student by their General Register number within the current tenant.
    /// </summary>
    Task<Student?> GetByGrNoAsync(int grNo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all students belonging to a specific division in an academic year.
    /// Navigation properties are auto-included via EF configuration.
    /// </summary>
    Task<List<Student>> GetByDivisionAsync(
        Guid divisionId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk lookup of students by GR numbers (case-sensitive integer match).
    /// Useful for import operations.
    /// </summary>
    /// <returns>Dictionary mapping GR number to student entity.</returns>
    Task<Dictionary<int, Student>> GetStudentsByGrNosAsync(
        IEnumerable<int> grNos,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of students enrolled in the current tenant for a given academic year.
    /// Used by <see cref="StudentCodeGenerator"/> to generate sequential GR numbers.
    /// </summary>
    Task<int> GetEnrolledCountForYearAsync(
        Guid? tenantId,
        int academicYear,
        CancellationToken cancellationToken = default);
}
