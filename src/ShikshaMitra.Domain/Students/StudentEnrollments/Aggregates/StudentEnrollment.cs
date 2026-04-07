using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Aggregate root representing a student's formal enrollment in a specific academic year,
/// standard, and division.
///
/// <para>
/// This is the operational record created after a student's admission is approved.
/// It tracks the active enrollment state for the academic period.
/// </para>
/// </summary>
public class StudentEnrollment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Reference to the enrolled <see cref="Student"/>.</summary>
    public Guid StudentId { get; private set; }

    /// <summary>Academic year of this enrollment.</summary>
    public Guid AcademicYearId { get; private set; }

    /// <summary>Standard (class) the student is enrolled in.</summary>
    public Guid? StandardId { get; private set; }

    /// <summary>Division the student is assigned to.</summary>
    public Guid? DivisionId { get; private set; }

    /// <summary>Indicates whether this enrollment is currently active.</summary>
    public bool IsActive { get; private set; }

    private StudentEnrollment() { /* For ORM */ }

    internal StudentEnrollment(
        Guid id,
        Guid? tenantId,
        Guid studentId,
        Guid academicYearId,
        Guid? standardId,
        Guid? divisionId)
        : base(id)
    {
        TenantId      = tenantId;
        StudentId     = studentId;
        AcademicYearId = academicYearId;
        StandardId    = standardId;
        DivisionId    = divisionId;
        IsActive      = true;
    }

    /// <summary>Marks this enrollment as inactive (e.g. student transferred out).</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Re-activates a previously deactivated enrollment.</summary>
    public void Activate() => IsActive = true;
}
