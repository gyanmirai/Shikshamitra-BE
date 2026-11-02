using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Divisions;

public class DivisionDto : AuditedEntityDto<Guid>
{
	public string Name { get; set; } = null!; // The response mapper populates these members after construction.

	public int HeadCount { get; set; }

	public int Strength { get; set; }
	public int SortOrder { get; set; }
	public int CurrentStudentCount { get; set; }
	public int AvailableSeats { get; set; }
	public int MaleCount { get; set; }
	public int FemaleCount { get; set; }

	public string? RoomNumber { get; set; }

	public string? RoomId { get; set; }

	public Guid StandardId { get; set; }
}
