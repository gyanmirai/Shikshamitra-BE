using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

/// <summary>
/// DTO for activating or deactivating an inquiry source.
/// </summary>
public class UpdateInquiryPreferenceStatusDto
{
	/// <summary>
	/// Active sources can be selected for new or changed inquiries.
	/// </summary>
	public bool IsActive { get; set; }

	/// <summary>
	/// Optional reason captured when deactivating a source.
	/// </summary>
	[StringLength(InquiryPreferenceConsts.MaxDeactivationReasonLength)]
	public string? DeactivationReason { get; set; }
}
