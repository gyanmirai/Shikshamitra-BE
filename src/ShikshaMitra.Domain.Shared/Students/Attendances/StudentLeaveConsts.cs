namespace ShikshaMitra.Students.Attendances;

/// <summary>
/// Constants for StudentLeave entity validation and constraints
/// </summary>
public static class StudentLeaveConsts
{
	/// <summary>
	/// Maximum length for leave description (rich text HTML)
	/// </summary>
	public const int MaxDescriptionLength = 5000;
	
	/// <summary>
	/// Maximum length for admin/approver remarks
	/// </summary>
	public const int MaxRemarksLength = 1000;
	
	/// <summary>
	/// Maximum length for rejection reason
	/// </summary>
	public const int MaxRejectionReasonLength = 500;
	
	/// <summary>
	/// Maximum attachment file size (5MB in bytes)
	/// </summary>
	public const int MaxAttachmentSize = 5 * 1024 * 1024; // 5MB
	
	/// <summary>
	/// Maximum length for attachment file name
	/// </summary>
	public const int MaxAttachmentFileNameLength = 255;
	
	/// <summary>
	/// Maximum length for attachment content type (MIME type)
	/// </summary>
	public const int MaxAttachmentContentTypeLength = 100;
	
	/// <summary>
	/// Maximum number of days a leave can span (configurable business rule)
	/// </summary>
	public const int MaxLeaveDurationDays = 90;
	
	/// <summary>
	/// Maximum number of days in the past a leave can be created (configurable business rule)
	/// </summary>
	public const int MaxPastDays = 365;
	
	/// <summary>
	/// Allowed file content types for attachments
	/// </summary>
	public static readonly string[] AllowedContentTypes = new[]
	{
		"application/pdf",
		"image/jpeg",
		"image/jpg",
		"image/png"
	};
}
