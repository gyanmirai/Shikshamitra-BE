using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Common;

public class DocumentMetadataDto : EntityDto<Guid>
{
	public string FileName { get; set; } = null!; // The response mapper populates these members after construction.
	public string ContentType { get; set; } = null!;

	public long Size { get; set; }
}