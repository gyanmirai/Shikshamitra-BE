namespace ShikshaMitra.Students.StudentEnrollments.Settings;

/// <summary>
/// Predefined file formats that can be accepted for document uploads
/// </summary>
public enum AcceptedFileFormat
{
	/// <summary>
	/// JPEG image format
	/// </summary>
	Jpg = 1,

	/// <summary>
	/// JPEG image format (alternative extension)
	/// </summary>
	Jpeg = 2,

	/// <summary>
	/// PNG image format
	/// </summary>
	Png = 3,

	/// <summary>
	/// Microsoft Word document
	/// </summary>
	Docx = 4,

	/// <summary>
	/// PDF document
	/// </summary>
	Pdf = 5,

	/// <summary>
	/// Microsoft Excel spreadsheet
	/// </summary>
	Xlsx = 6
}
