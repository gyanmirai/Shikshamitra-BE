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
}
