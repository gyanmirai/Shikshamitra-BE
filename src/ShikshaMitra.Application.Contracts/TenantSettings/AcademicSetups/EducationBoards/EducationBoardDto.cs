using System;
using System.Collections.Generic;
using ShikshaMitra.Onboarding.EducationMediums;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Onboarding.EducationBoards;

public class EducationBoardDto : AuditedEntityDto<Guid>
{
	public string Name { get; set; }
	public Guid AcademicYearId { get; set; }

	public List<EducationMediumDto> EducationMediums { get; set; }
}