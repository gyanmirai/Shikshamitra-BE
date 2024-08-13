using System;
using System.Collections.Generic;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.EducationBoards;

public class EducationBoardDto : AuditedEntityDto<Guid>
{
	public string Name { get; set; }
	public Guid AcademicYearId { get; set; }

	public List<EducationMediumDto> EducationMediums { get; set; }
}