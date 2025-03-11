using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings;

public class ShiftManagementDto : AuditedEntityDto<Guid>
{
	public string ShiftName { get; set; }
	public TimeOnly StartTime { get; set; }
	public TimeOnly EndTime { get; set; }
}
