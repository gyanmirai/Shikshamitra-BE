using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Common;

public class DocumentMetadataDto : EntityDto<Guid>
{
	public string FileName { get; set; }
	public string ContentType { get; set; }

	public long Size { get; set; }
}