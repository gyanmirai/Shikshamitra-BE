using System;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentReportDto
{
	public Guid? AcademicYearId { get; set; }
	public Guid? EducationBoardId { get; set; }
	public Guid? EducationMediumId { get; set; }
	public Guid? EducaitonMediumStageId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
}
