using System;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.TenantSettings.Reports;

public class InstituteHierarchyRow : IUserScopedEntity
{
	public Guid? AcademicYearId { get; set; }
	public Guid? EducationBoardId { get; set; }
	public Guid? EducationMediumId { get; set; }
	public Guid? EducationMediumStageId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
	public string Year { get; set; } = "";
	public string Board { get; set; } = "";
	public string Medium { get; set; } = "";
	public string Stage { get; set; } = "";
	public string Standard { get; set; } = "";
	public string Division { get; set; } = "";
	public int? Capacity { get; set; }
	public string? RoomReference { get; set; }
	public Guid? ShiftId { get; set; }
}
