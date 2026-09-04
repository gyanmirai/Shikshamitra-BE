using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Settings.ConcessionType.Dtos;

public class ConcessionTypeRequest
{
	public required string Name { get; set; }
	public required List<ConcessionTypeDocumentDto> Documents { get; set; }
	public string? Description { get; set; }
	public string? ConcurrencyStamp { get; set; }
}

public class ConcessionTypeStatusUpdateRequest
{
	public bool IsActive { get; set; }
	public string? DeactivationReason { get; set; }
	public string? ConcurrencyStamp { get; set; }
}
