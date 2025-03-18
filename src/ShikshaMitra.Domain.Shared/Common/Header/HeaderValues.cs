using System;

namespace ShikshaMitra.Models;

public class HeaderValues
{
	// Required GUIDs from X-Custom-Header
	public Guid AcademicYearId { get; set; }
	public Guid EducationBoardId { get; set; }
	public Guid EducationMediumId { get; set; }

	// Context information
	public string CurrentUser { get; set; }
	public DateTime RequestTime { get; set; }

	// Helper properties
	public string FormattedRequestTime => RequestTime.ToString("yyyy-MM-dd HH:mm:ss");

	// Validation
	public bool HasValidAcademicYear => AcademicYearId != Guid.Empty;
	public bool HasValidEducationBoard => EducationBoardId != Guid.Empty;
	public bool HasValidEducationMedium => EducationMediumId != Guid.Empty;
	public bool AllRequiredHeadersPresent => HasValidAcademicYear && HasValidEducationBoard && HasValidEducationMedium;
}
