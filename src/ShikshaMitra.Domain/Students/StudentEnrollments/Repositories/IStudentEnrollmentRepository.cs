using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Repository interface for the <see cref="StudentEnrollment"/> aggregate root.
/// Provides named query methods beyond the generic <see cref="IRepository{TEntity,TKey}"/>.
/// </summary>
public interface IStudentEnrollmentRepository : IRepository<StudentEnrollment, Guid>
{
    /// <summary>
    /// Gets the enrollment for a specific student in a specific academic year.
    /// </summary>
    Task<StudentEnrollment?> GetByStudentAndAcademicYearAsync(
        Guid studentId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active enrollments for a division in an academic year.
    /// </summary>
    Task<List<StudentEnrollment>> GetByDivisionAsync(
        Guid divisionId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);
}
