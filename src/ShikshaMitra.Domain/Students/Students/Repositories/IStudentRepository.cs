using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Gets the tracked student root for an already authorized fee-assignment update.
    /// Academic and profile graphs are not needed to change the fee-structure reference.
    /// </summary>
    Task<Student?> GetForFeeAssignmentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a student query without configured automatic navigation includes.
    /// Use for list projections where loading the whole aggregate graph would be wasteful.
    /// </summary>
    Task<IQueryable<Student>> GetQueryableWithoutAutoIncludesAsync();

    /// <summary>
    /// Returns an untracked report query with only printed profile, family and academic-year details.
    /// Callers must restrict the query to the report's current student batch before materialization.
    /// </summary>
    Task<IQueryable<Student>> GetAcademicReportQueryableAsync();

    /// <summary>
    /// Gets a student with only the aggregate parts required for inquiry editing.
    /// </summary>
    Task<Student?> GetInquiryForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a student with the registration section aggregate parts used by registration forms.
    /// </summary>
    Task<Student?> GetRegistrationAggregateAsync(Guid id, CancellationToken cancellationToken = default);

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
    /// Gets enrolled students matching board, medium, class, and optional division filters.
    /// </summary>
    Task<List<Student>> GetActiveStudentsInScopeAsync(
        Guid academicYearId,
        Guid boardId,
        Guid educationMediumId,
        IReadOnlyList<Guid> classIds,
        IReadOnlyList<Guid>? divisionIds = null,
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

    /// <summary>
    /// Returns the highest sequential number embedded in <c>InquiryNo</c> values
    /// (format: <c>INQ{NNN}</c>) for the given academic medium and tenant.
    /// Returns <c>0</c> when no valid inquiry numbers exist yet.
    /// </summary>
    Task<int> GetMaxInquirySequenceForMediumAsync(
        Guid academicMediumId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
