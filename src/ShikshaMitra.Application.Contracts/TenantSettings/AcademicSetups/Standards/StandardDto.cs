using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Standards;

public class StandardDto : AuditedEntityDto<Guid>
{
	public string Name { get; set; }

	public int AgeLowerLimit { get; set; }

	public List<DivisionDto> Divisions { get; set; }

	public Guid EducationMediumStageId { get; set; }
}