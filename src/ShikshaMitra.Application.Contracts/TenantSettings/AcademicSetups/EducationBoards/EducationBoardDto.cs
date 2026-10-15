using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;

public class EducationBoardDto : AuditedEntityDto<Guid>
{
	public EducationBoardType Type { get; set; }
	public string? CustomName { get; set; }
	public string Name => EducationBoardNames.DisplayName(Type);
	public Guid AcademicYearId { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public List<EducationMediumDto> EducationMediums { get; set; }
}
