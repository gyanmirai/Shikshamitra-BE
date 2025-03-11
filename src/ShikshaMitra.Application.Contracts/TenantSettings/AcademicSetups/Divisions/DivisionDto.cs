using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Divisions;

public class DivisionDto : AuditedEntityDto<Guid>
{
	public string Name { get; set; }

	public int Strength { get; set; }

	public string RoomNumber { get; set; }

	public Guid StandardId { get; set; }
}