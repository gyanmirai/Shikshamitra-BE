using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using System;
using System.Collections.Generic;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

public class AcademicSetupPermissionsDto
{
	public List<AcademicYearDto> AcademicYears { get;set;} = new List<AcademicYearDto>();

	public List<EducationBoardDto> EducationBoards { get; set; } = new List<EducationBoardDto>();

	public List<EducationMediumDto> EducationMediums { get; set; } = new List<EducationMediumDto>();

	public List<EducationMediumStageDto> EducationMediumStages { get; set; } = new List<EducationMediumStageDto>();

	public List<StandardDto> Standards { get; set; } = new List<StandardDto>();

	public List<DivisionDto> Divisions { get; set; } = new List<DivisionDto>();

	public Guid UserId { get; set; }
}