using System;
using System.Collections.Generic;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.Standards;

public class StandardDto : AuditedEntityDto<Guid>
{
	public string Name { get; set; }

	public List<DivisionDto> Divisions { get; set; }

	public Guid EducationMediumStageId { get; set; }
}