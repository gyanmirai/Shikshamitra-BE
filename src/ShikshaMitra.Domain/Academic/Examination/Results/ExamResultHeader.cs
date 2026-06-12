using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.Results;

/// <summary>
/// Header record for a generated exam result (one per Exam+Division).
/// Tracks the full lifecycle: Generated → Published → Frozen.
/// </summary>
public class ExamResultHeader : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid ExamId { get; private set; }
    public Guid DivisionId { get; private set; }
    public ExamResultStatus Status { get; private set; }

    public DateTime? GeneratedOn { get; private set; }
    public Guid? GeneratedById { get; private set; }

    public DateTime? PublishedOn { get; private set; }
    public Guid? PublishedById { get; private set; }

    public DateTime? FrozenOn { get; private set; }
    public Guid? FrozenById { get; private set; }

    public string? Remarks { get; private set; }

    private ExamResultHeader() { }

    public ExamResultHeader(
        Guid id,
        Guid examId,
        Guid divisionId,
        Guid generatedById,
        Guid? tenantId = null) : base(id)
    {
        ExamId = examId;
        DivisionId = divisionId;
        TenantId = tenantId;
        Status = ExamResultStatus.Generated;
        GeneratedOn = DateTime.UtcNow;
        GeneratedById = generatedById;
    }

    /// <summary>
    /// Re-generates the result (allowed unless Frozen without ForceRegenerate).
    /// Resets publish/freeze data.
    /// </summary>
    public void Regenerate(Guid userId)
    {
        Status = ExamResultStatus.Generated;
        GeneratedOn = DateTime.UtcNow;
        GeneratedById = userId;
        PublishedOn = null;
        PublishedById = null;
        FrozenOn = null;
        FrozenById = null;
        Remarks = null;
    }

    public void Publish(Guid userId)
    {
        if (Status == ExamResultStatus.Frozen)
            throw new UserFriendlyException("Result is frozen and cannot be published. Regenerate first.");

        Status = ExamResultStatus.Published;
        PublishedOn = DateTime.UtcNow;
        PublishedById = userId;
    }

    public void Freeze(Guid userId, string? remarks = null)
    {
        if (Status != ExamResultStatus.Published)
            throw new UserFriendlyException("Only published results can be frozen.");

        Status = ExamResultStatus.Frozen;
        FrozenOn = DateTime.UtcNow;
        FrozenById = userId;
        Remarks = remarks;
    }
}
