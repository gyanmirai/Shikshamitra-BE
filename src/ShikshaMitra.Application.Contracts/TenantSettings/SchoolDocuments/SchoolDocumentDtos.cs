using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Auditing;

namespace ShikshaMitra.TenantSettings.SchoolDocuments;

public class SchoolDocumentListInput : PagedAndSortedResultRequestDto
{
	[StringLength(200)]
	public string? Search { get; set; }
	public SchoolDocumentCategory? Category { get; set; }
}

public class SchoolDocumentWriteDto
{
	[Required, StringLength(SchoolDocumentConsts.MaxNameLength)]
	public string Name { get; set; } = "";
	[EnumDataType(typeof(SchoolDocumentCategory))]
	public SchoolDocumentCategory Category { get; set; }
	[StringLength(SchoolDocumentConsts.MaxNotesLength)]
	public string? Notes { get; set; }
	public string? ConcurrencyStamp { get; set; }
	public List<Guid> RetainedFileIds { get; set; } = [];
	[DisableAuditing]
	public List<SchoolDocumentUploadDto> Files { get; set; } = [];
}

public class SchoolDocumentUploadDto
{
	public string FileName { get; set; } = "";
	public string ContentType { get; set; } = "";
	[DisableAuditing]
	public byte[] Content { get; set; } = [];
}

public class SchoolDocumentDto : EntityDto<Guid>
{
	public string Name { get; set; } = "";
	public SchoolDocumentCategory Category { get; set; }
	public string? Notes { get; set; }
	public string UploadedBy { get; set; } = "";
	public DateTime CreationTime { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
	public List<SchoolDocumentFileDto> Files { get; set; } = [];
}

public class SchoolDocumentFileDto : EntityDto<Guid>
{
	public string FileName { get; set; } = "";
	public string ContentType { get; set; } = "";
	public long Size { get; set; }
}

public class SchoolDocumentDownloadDto : SchoolDocumentFileDto
{
	[DisableAuditing]
	public byte[] Content { get; set; } = [];
}
