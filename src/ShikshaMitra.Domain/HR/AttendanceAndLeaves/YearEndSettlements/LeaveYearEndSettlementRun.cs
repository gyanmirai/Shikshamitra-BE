using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;

public class LeaveYearEndSettlementRun : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid? TargetAcademicYearId { get; private set; }
	public LeaveYearEndSettlementRunStatus Status { get; private set; }
	public DateTime PreviewedAt { get; private set; }
	public DateTime? FinalizedAt { get; private set; }
	public int TotalItems { get; private set; }
	public int FinalizedItems { get; private set; }
	public int BlockedItems { get; private set; }
	public decimal TotalCarriedDays { get; private set; }
	public decimal TotalEncashedDays { get; private set; }
	public decimal TotalForfeitedDays { get; private set; }
	public decimal TotalEncashmentAmount { get; private set; }

	private LeaveYearEndSettlementRun()
	{
	}

	public LeaveYearEndSettlementRun(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid? targetAcademicYearId,
		DateTime previewedAt) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		TargetAcademicYearId = targetAcademicYearId;
		Status = LeaveYearEndSettlementRunStatus.Draft;
		PreviewedAt = previewedAt;
	}

	public void Refresh(Guid? targetAcademicYearId, DateTime previewedAt, IReadOnlyCollection<LeaveYearEndSettlementItem> items)
	{
		TargetAcademicYearId = targetAcademicYearId;
		PreviewedAt = previewedAt;
		RefreshSummary(items);
	}

	public void MarkFinalized(DateTime finalizedAt, IReadOnlyCollection<LeaveYearEndSettlementItem> items)
	{
		Status = LeaveYearEndSettlementRunStatus.Finalized;
		FinalizedAt = finalizedAt;
		RefreshSummary(items);
	}

	public void RefreshSummary(IReadOnlyCollection<LeaveYearEndSettlementItem> items)
	{
		TotalItems = items.Count;
		FinalizedItems = items.Count(x => x.Status == LeaveYearEndSettlementItemStatus.Finalized);
		BlockedItems = items.Count(x => x.Status == LeaveYearEndSettlementItemStatus.Blocked);
		TotalCarriedDays = items.Sum(x => x.CarriedDays);
		TotalEncashedDays = items.Sum(x => x.EncashedDays);
		TotalForfeitedDays = items.Sum(x => x.ForfeitedDays);
		TotalEncashmentAmount = items.Sum(x => x.Amount);
	}
}
