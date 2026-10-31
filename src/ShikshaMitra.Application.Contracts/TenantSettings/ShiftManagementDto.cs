using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings;

public class ShiftManagementDto : AuditedEntityDto<Guid>
{
	public string ShiftName { get; set; } = null!; // The response mapper populates these members after construction.
	public TimeOnly StartTime { get; set; }
	public TimeOnly EndTime { get; set; }
}
