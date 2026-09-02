using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

/// <summary>
/// The immutable examination-level publication boundary for seating.
/// Its existence means every seating-plan group for the examination was
/// validated and published together and no draft mutation may continue.
/// </summary>
public class ExamSeatingPublication : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid ExamId { get; private set; }
    public DateTime PublishedAt { get; private set; }
    public Guid PublishedByUserId { get; private set; }

    private ExamSeatingPublication() { }

    public ExamSeatingPublication(
        Guid id,
        Guid? tenantId,
        Guid examId,
        DateTime publishedAt,
        Guid publishedByUserId) : base(id)
    {
        TenantId = tenantId;
        ExamId = examId;
        PublishedAt = publishedAt;
        PublishedByUserId = publishedByUserId;
    }
}
