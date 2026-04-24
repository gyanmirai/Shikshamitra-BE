using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.ConcessionType.Dtos;

public class ConcessionTypeDocumentDto : EntityDto<Guid>
{
	public required string DocumentName { get; set; }
	public required long DocumentSize { get; set; }
	public List<DocumentType> AcceptedFormat { get; set; } = new List<DocumentType>();
}
