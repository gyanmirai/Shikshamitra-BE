using System;
using System.Collections.Generic;
using ShikshaMitra.Finance.Setting.ConcessionType.Dtos;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.ConcessionType.Dtos;

public class ConcessionTypeRequest
{
	public required string Name { get; set; }
	public required List<ConcessionTypeDocumentDto> Documents { get; set; }
	public string? Description { get; set; }
}
