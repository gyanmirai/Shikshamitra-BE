using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollment.Settings;

public class StudentDocumentSettingDto : EntityDto<Guid>
{
	public string DocumentName { get; set; }
	public required bool isRequired { get; set; }
	public List<DocumentType> DocumentTypes { get; set; } = [];
}