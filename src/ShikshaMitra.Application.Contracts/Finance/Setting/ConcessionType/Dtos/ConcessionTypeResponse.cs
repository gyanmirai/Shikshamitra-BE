using ShikshaMitra.Finance.Setting.ConcessionType.Dtos;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.ConcessionType.Dtos;

public class ConcessionTypeResponse : EntityDto<Guid>
{
	public string Name { get; set; }
	public bool IsDocumentRequired { get; set; }
	public List<ConcessionTypeDocumentDto> Documents { get; set; } = new List<ConcessionTypeDocumentDto>();
	public string? Description { get; set; }
}
