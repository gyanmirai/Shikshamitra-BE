using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using ShikshaMitra.TenantSettings.SchoolDocuments;
using Volo.Abp.Auditing;
using Volo.Abp.Validation;

namespace ShikshaMitra.Models;

public class SchoolDocumentWriteRequest
{
	[Required, StringLength(SchoolDocumentConsts.MaxNameLength)]
	public string Name { get; set; } = "";
	[Required, EnumDataType(typeof(SchoolDocumentCategory))]
	public SchoolDocumentCategory? Category { get; set; }
	[StringLength(SchoolDocumentConsts.MaxNotesLength)]
	public string? Notes { get; set; }
	public string? ConcurrencyStamp { get; set; }
	public List<Guid> RetainedFileIds { get; set; } = [];
	[DisableValidation, DisableAuditing]
	public List<IFormFile> Files { get; set; } = [];
}
