using System.Collections.Generic;

namespace ShikshaMitra.HR.Employees.Settings;

public class CreateUpdateDocumentSettingDto
{
	public string DocumentName { get; set; } = string.Empty;
	public bool IsRequired { get; set; }

	public List<DocumentType> DocumentTypes { get; set; } = [];
}