using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.ConcessionType.Dtos;

public class ConcessionTypeResponse : EntityDto<Guid>
{
	public string Name { get; set; } = null!; // The response mapper populates these members after construction.
	public bool IsDocumentRequired { get; set; }
	public List<ConcessionTypeDocumentDto> Documents { get; set; } = new List<ConcessionTypeDocumentDto>();
	public string? Description { get; set; }
	public bool IsActive { get; set; }
	public string? DeactivationReason { get; set; }
	public bool IsUsedInDownstream { get; set; }
	public bool CanDelete => !IsActive && !IsUsedInDownstream;
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class ConcessionTypeVersionHistoryItemDto : EntityDto<Guid>
{
	public int VersionNumber { get; set; }
	public string ChangeAction { get; set; } = string.Empty;
	public DateTime ChangeTime { get; set; }
	public Guid? ActorId { get; set; }
	public string? ActorName { get; set; }
	public List<ConcessionTypeVersionFieldChangeDto> ChangedFields { get; set; } = [];
}

public class ConcessionTypeVersionFieldChangeDto
{
	public string FieldName { get; set; } = string.Empty;
	public string? OriginalValue { get; set; }
	public string? NewValue { get; set; }
}

public class ConcessionTypeDocumentDefaultsResponse
{
	public long DefaultMaxFileSizeBytes { get; set; }
	public long AbsoluteMaxFileSizeBytes { get; set; }
	public List<ShikshaMitra.Common.DocumentType> DefaultAcceptedFormats { get; set; } = [];
	public List<ShikshaMitra.Common.DocumentType> AllowedFormats { get; set; } = [];
}
