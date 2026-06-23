using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

/// <summary>
/// Input DTO for listing inquiry sources with optional active-state filtering.
/// </summary>
public class GetInquiryPreferenceListInput : PagedAndSortedResultRequestDto
{
	/// <summary>
	/// Optional filter by active/inactive status.
	/// </summary>
	public bool? IsActive { get; set; }

	/// <summary>
	/// Optional search text for source name or description.
	/// </summary>
	public string? FilterText { get; set; }
}
