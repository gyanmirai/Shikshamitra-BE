using ShikshaMitra.Common;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollment.Settings;

public class CreateUpdateStudentDocumentSettingDto
{
	public string DocumentName { get; set; }
	public required bool isRequired { get; set; }
	public List<DocumentType> DocumentTypes { get; set; } = [];


}