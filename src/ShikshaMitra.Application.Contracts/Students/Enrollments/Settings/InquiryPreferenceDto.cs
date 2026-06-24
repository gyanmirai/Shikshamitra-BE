using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

/// <summary>
/// DTO for displaying inquiry preference information
/// </summary>
public class InquiryPreferenceDto : FullAuditedEntityDto<Guid>
{
	/// <summary>
	/// Name of the inquiry preference (e.g., "Online", "Offline", "Facebook", "Walk-in")
	/// </summary>
	public string PreferenceName { get; set; } = string.Empty;

	/// <summary>
	/// Optional description for staff-facing context.
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Academic year identifier
	/// </summary>
	public Guid AcademicYearId { get; set; }

	/// <summary>
	/// Display order for settings and dropdowns.
	/// </summary>
	public int DisplayOrder { get; set; }

	/// <summary>
	/// Indicates whether this source can be selected for new or changed inquiries.
	/// </summary>
	public bool IsActive { get; set; }

	/// <summary>
	/// Optional reason captured when staff deactivate this source.
	/// </summary>
	public string? DeactivationReason { get; set; }

	/// <summary>
	/// Number of student additional-detail rows using this source.
	/// </summary>
	public int AssignedStudentsCount { get; set; }

	/// <summary>
	/// True when this source is unused and can be safely deleted.
	/// </summary>
	public bool CanDelete { get; set; }
}
