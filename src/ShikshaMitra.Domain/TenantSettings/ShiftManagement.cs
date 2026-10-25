using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;
using Volo.Abp;
using ShikshaMitra.HR;

namespace ShikshaMitra.TenantSettings;

public class ShiftManagement : AggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public string ShiftName { get; set; }
	public TimeOnly StartTime { get; set; }
	public TimeOnly EndTime { get; set; }

	public ShiftManagement() { }

	public ShiftManagement(Guid id, Guid? tenantId, string shiftName, TimeOnly startTime, TimeOnly endTime) : base(id)
	{
		if (endTime <= startTime) throw new BusinessException(HRErrorCodes.AttendancePolicyEvaluationInvalidShift);
		TenantId = tenantId;
		ShiftName = Check.NotNullOrWhiteSpace(shiftName, nameof(shiftName));
		StartTime = startTime;
		EndTime = endTime;
	}
}
