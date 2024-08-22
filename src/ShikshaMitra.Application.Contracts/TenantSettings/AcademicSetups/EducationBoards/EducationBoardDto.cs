using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using System;
using System.Collections.Generic;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.EducationBoards;

public class EducationBoardDto : AuditedEntityDto<Guid>
{
	public EducationBoardType Type { get; set; }
	public string Name { get { return Type.ToString(); } }
	public Guid AcademicYearId { get; set; }
	public List<EducationMediumDto> EducationMediums { get; set; }
}