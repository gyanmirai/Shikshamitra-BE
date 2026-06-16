using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.Results;

/// <summary>
/// Immutable event log for result lifecycle actions (Generated, Regenerated, Published, Frozen).
/// </summary>
public class ExamResultAuditLog : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid ExamId { get; private set; }
    public Guid DivisionId { get; private set; }

    /// <summary>Generated | Regenerated | Published | Frozen</summary>
    public string Action { get; private set; } = string.Empty;
    public Guid PerformedById { get; private set; }
    public string? Remarks { get; private set; }

    private ExamResultAuditLog() { }

    public ExamResultAuditLog(
        Guid id,
        Guid examId,
        Guid divisionId,
        string action,
        Guid performedById,
        string? remarks = null,
        Guid? tenantId = null) : base(id)
    {
        ExamId = examId;
        DivisionId = divisionId;
        Action = action;
        PerformedById = performedById;
        Remarks = remarks;
        TenantId = tenantId;
    }
}
