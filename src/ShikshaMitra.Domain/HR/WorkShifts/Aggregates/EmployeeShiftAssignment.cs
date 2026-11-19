using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.WorkShifts;

public class EmployeeShiftAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public Guid WorkShiftId { get; private set; }
	public DateOnly EffectiveFrom { get; private set; }
	public DateOnly? EffectiveTo { get; private set; }
	public string ShiftName { get; private set; } = string.Empty;
	public TimeOnly StartTime { get; private set; }
	public TimeOnly EndTime { get; private set; }
	public string Provenance { get; private set; } = "Scheduled";
	private EmployeeShiftAssignment() { }
	public EmployeeShiftAssignment(Guid id, Guid employeeId, EmployeeWorkShift shift, DateOnly from, DateOnly? to = null, string provenance = "Scheduled") : base(id)
	{
		if (!shift.IsActive) throw new BusinessException(HRErrorCodes.WorkShiftInvalid, "Select an active work shift.");
		TenantId = shift.TenantId; EmployeeId = employeeId; WorkShiftId = shift.Id;
		EffectiveFrom = from; CloseAt(to); ShiftName = shift.ShiftName;
		StartTime = shift.StartTime; EndTime = shift.EndTime; Provenance = provenance;
	}
	public void CloseAt(DateOnly? end)
	{
		if (end.HasValue && end <= EffectiveFrom) throw new BusinessException(HRErrorCodes.ShiftAssignmentInvalidDate);
		EffectiveTo = end;
	}
	public bool Covers(DateOnly date) => date >= EffectiveFrom && (!EffectiveTo.HasValue || date < EffectiveTo.Value);
}