using System;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;

public class CreateUpdateEducationBoardDto
{
	public EducationBoardType Type { get; set; }
	public Guid AcademicYearId { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
}