using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Fees;

/// <summary>
/// Aggregate root representing a fee obligation for one student for one academic period.
/// Domain analogue of HR's <c>Payroll</c> aggregate.
///
/// <para>
/// Full implementation is tracked in <c>docs/planning/student/fees-subdomain.md</c>.
/// Fee assignment logic currently lives in the <c>Finance</c> application module;
/// a dedicated ADR will clarify the bounded-context boundary before full implementation.
/// </para>
/// </summary>
public class StudentFeeRecord : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Reference to the student this fee record belongs to.</summary>
    public Guid StudentId { get; private set; }

    /// <summary>Academic year for which fees are being tracked.</summary>
    public Guid AcademicYearId { get; private set; }

    /// <summary>Current payment status of this fee record.</summary>
    public FeePaymentStatus Status { get; private set; }

    /// <summary>Lock state — once locked, no further edits are allowed.</summary>
    public FeeRecordLockState LockState { get; private set; }

    /// <summary>Optional remarks from the fee administrator.</summary>
    public string? Remarks { get; private set; }

    private StudentFeeRecord() { /* For ORM */ }

    /// <summary>Creates a new fee record for a student in the given academic year.</summary>
    internal StudentFeeRecord(Guid id, Guid? tenantId, Guid studentId, Guid academicYearId)
        : base(id)
    {
        TenantId      = tenantId;
        StudentId     = studentId;
        AcademicYearId = academicYearId;
        Status        = FeePaymentStatus.Pending;
        LockState     = FeeRecordLockState.Open;
    }

    /// <summary>Locks this fee record to prevent further edits.</summary>
    public void Lock()
    {
        if (LockState == FeeRecordLockState.Locked)
            throw new InvalidOperationException("Fee record is already locked.");

        LockState = FeeRecordLockState.Locked;
    }

    /// <summary>Updates the payment status.</summary>
    internal void SetStatus(FeePaymentStatus status) => Status = status;

    /// <summary>Updates the remarks.</summary>
    internal void SetRemarks(string? remarks) => Remarks = remarks?.Trim();
}
