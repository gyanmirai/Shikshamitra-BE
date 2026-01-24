namespace ShikshaMitra.Students.StudentEnrollments.Settings;

/// <summary>
/// Predefined file formats that can be accepted for document uploads
/// </summary>
public enum AcceptedFileFormat
{
	/// <summary>
	/// JPEG image format
	/// </summary>
	Jpg = 0,

	/// <summary>
	/// JPEG image format (alternative extension)
	/// </summary>
	Jpeg = 1,

	/// <summary>
	/// PNG image format
	/// </summary>
	Png = 2,

	/// <summary>
	/// Microsoft Word document
	/// </summary>
	Docx = 3,

	/// <summary>
	/// PDF document
	/// </summary>
	Pdf = 4,

	/// <summary>
	/// Microsoft Excel spreadsheet
	/// </summary>
	Xlsx = 5
}
