using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.WorkShifts;

public class EmployeeWorkShift : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string ShiftName { get; private set; } = string.Empty;
	public TimeOnly StartTime { get; private set; }
	public TimeOnly EndTime { get; private set; }
	public bool IsActive { get; private set; } = true;
	public Guid? LegacySchoolShiftId { get; private set; }
	private EmployeeWorkShift() { }
	public EmployeeWorkShift(Guid id, Guid? tenantId, string name, TimeOnly start, TimeOnly end) : base(id)
	{ TenantId = tenantId; Update(name, start, end, true); }
	public void Update(string name, TimeOnly start, TimeOnly end, bool active)
	{
		if (end <= start) throw new BusinessException(HRErrorCodes.WorkShiftInvalid, "End time must be after start time. Overnight shifts are not supported.");
		ShiftName = Check.NotNullOrWhiteSpace(name, nameof(name), 128).Trim();
		StartTime = start; EndTime = end; IsActive = active;
	}
}