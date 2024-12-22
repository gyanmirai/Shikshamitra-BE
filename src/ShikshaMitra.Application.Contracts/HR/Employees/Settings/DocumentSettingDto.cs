using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Employees.Settings;

public class DocumentSettingDto : EntityDto<Guid>
{
	public string DocumentName { get; set; } = string.Empty;
	public bool IsRequired { get; set; }
	public List<DocumentType> DocumentTypes { get; set; } = [];
}