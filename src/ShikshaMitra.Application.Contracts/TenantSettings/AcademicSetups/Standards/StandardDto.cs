using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Standards;

public class StandardDto : AuditedEntityDto<Guid>
{
	public string Name { get; set; } = null!; // The response mapper populates these members after construction.

	public int AgeLowerLimit { get; set; }
	public int SortOrder { get; set; }

	public List<DivisionDto> Divisions { get; set; } = null!;

	public Guid EducationMediumStageId { get; set; }
	public Guid? ShiftId { get; set; }
	public ShiftManagementDto Shift { get; set; } = new ShiftManagementDto();
}
