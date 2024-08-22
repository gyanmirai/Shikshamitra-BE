using System;
using System.Collections.Generic;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

public class AcademicSetupPermissionsDto
{
	public List<Guid> AcademicYearIds { get; set; } = new List<Guid>();
	public List<Guid> EducationBoardIds { get; set; } = new List<Guid>();
	public List<Guid> EducationMediumIds { get; set; } = new List<Guid>();
	
	public List<Guid> EducationMediumStageIds { get; set; } = new List<Guid>();
	public List<Guid> StandardIds { get; set; } = new List<Guid>();
	public List<Guid> DivisionIds { get; set; } = new List<Guid>();
}