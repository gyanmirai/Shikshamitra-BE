using System;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;

public class CreateUpdateEducationBoardDto
{
	public EducationBoardType Type { get; set; }
	public Guid AcademicYearId { get; set; }
}