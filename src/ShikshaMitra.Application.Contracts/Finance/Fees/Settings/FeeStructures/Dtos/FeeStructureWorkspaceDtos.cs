using System;
using System.Collections.Generic;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;

public class FeeStructurePreviewRequest : StandardFeeStructureSaveRequest
{
	public Guid FeeStructureMetadataId { get; set; }
	public Guid? StructureId { get; set; }
}

public class FeeStructurePublishRequest
{
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class FeeStructureRevisionRequest : FeeStructurePublishRequest { }

public class FeeStructurePreviewDto
{
	public Dictionary<string, string> PolicyLabels { get; set; } = [];
	public string? ReceiptBookName { get; set; }
	public bool IsReady => Errors.Count == 0;
	public List<string> Errors { get; set; } = [];
	public List<string> Warnings { get; set; } = [];
	public List<FeeStructureCoverageDto> Coverage { get; set; } = [];
	public List<ResolvedFeeHeadScheduleDto> Schedules { get; set; } = [];
	public StandardFeeStructureRequest Candidate { get; set; } = new();
}

public class FeeStructureCoverageDto
{
	public Guid FeeHeadId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Collection { get; set; } = "No late fee";
	public string Concession { get; set; } = "Not eligible";
	public string Proration { get; set; } = "Full charge";
	public string Refund { get; set; } = "No refund policy attached";
	public string Accounting { get; set; } = string.Empty;
}

public class FeeStructureContextDto
{
	public Guid AcademicYearId { get; set; }
	public Guid BoardId { get; set; }
	public string AcademicYearName { get; set; } = string.Empty;
	public string ClassName { get; set; } = string.Empty;
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public Dictionary<string, bool> Permissions { get; set; } = [];
}

public class FeeStructureSettingsDto : FeeStructureContextDto
{
	public List<FeeStructureHeadOptionDto> Heads { get; set; } = [];
	public List<FeeStructurePolicyOptionDto> Policies { get; set; } = [];
	public List<FeeStructureTermOptionDto> Terms { get; set; } = [];
	public List<FeeStructureBookOptionDto> ReceiptBooks { get; set; } = [];
}

public class FeeStructureHeadOptionDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string CategoryName { get; set; } = string.Empty;
	public bool IsConcessionApplied { get; set; }
}

public class FeeStructurePolicyOptionDto
{
	public Guid Id { get; set; }
	public Guid VersionId { get; set; }
	public string Family { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public int VersionNumber { get; set; }
	public bool IsCurrent { get; set; }
}

public class FeeStructureTermOptionDto
{
	public Guid Id { get; set; }
	public Guid VersionId { get; set; }
	public string Name { get; set; } = string.Empty;
	public int VersionNumber { get; set; }
	public bool IsCurrent { get; set; }
	public string DemandGenerationRule { get; set; } = string.Empty;
	public int? DemandOffsetDays { get; set; }
	public List<ScheduleOverrideDto> Installments { get; set; } = [];
}

public class FeeStructureBookOptionDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
}

public class FeeStructureClassOptionDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
}
