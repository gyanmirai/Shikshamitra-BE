using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.TenantSettings.EducationBoards;

public class CreateUpdateEducationBoardDto
{
	public EducationBoardType Type { get; set; }
	public Guid AcademicYearId { get; set; }
}