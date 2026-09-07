using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.ConcessionType.Dtos;

public class ConcessionTypeDocumentDto : EntityDto<Guid>
{
	public required string DocumentName { get; set; }
	public long MaxFileSizeBytes { get; set; }
	public bool IsRequired { get; set; } = true;
	public List<DocumentType> AcceptedFormats { get; set; } = new List<DocumentType>();

	[Obsolete("Use MaxFileSizeBytes.")]
	public long DocumentSize
	{
		get => MaxFileSizeBytes;
		set => MaxFileSizeBytes = value;
	}

	[Obsolete("Use AcceptedFormats.")]
	public List<DocumentType> AcceptedFormat
	{
		get => AcceptedFormats;
		set => AcceptedFormats = value ?? [];
	}
}
