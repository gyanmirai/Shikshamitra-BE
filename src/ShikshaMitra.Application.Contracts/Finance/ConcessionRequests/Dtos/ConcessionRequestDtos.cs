using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Finance.ConcessionRequests;

namespace ShikshaMitra.Application.Contracts.Finance.ConcessionRequests.Dtos;

// ─── Step 1+2 (merged) ────────────────────────────────────────────────────────

public class CreateConcessionRequestInput
{
	[Required]
	public Guid BoardId { get; set; }

	[Required]
	public Guid EducationMediumId { get; set; }

	[Required]
	public Guid StandardId { get; set; }

	[Required]
	public Guid DivisionId { get; set; }

	[Required]
	public Guid StudentId { get; set; }

	[Required]
	[MinLength(1)]
	public List<Guid> ConcessionTypeIds { get; set; } = new();
}

// ─── Step 1+2 PUT (Update) ────────────────────────────────────────────────────

public class UpdateConcessionRequestInput
{
	[Required]
	public Guid BoardId { get; set; }

	[Required]
	public Guid EducationMediumId { get; set; }

	[Required]
	public Guid StandardId { get; set; }

	[Required]
	public Guid DivisionId { get; set; }

	[Required]
	public Guid StudentId { get; set; }

	[Required]
	[MinLength(1)]
	public List<Guid> ConcessionTypeIds { get; set; } = new();
}

// ─── Step 3 ───────────────────────────────────────────────────────────────────

public class SaveFeeHeadsDataRequest
{
	[Required]
	public List<FeeHeadsDataItem> FeeHeadsData { get; set; } = new();
}

public class FeeHeadsDataItem
{
	[Required]
	public Guid ConcessionTypeId { get; set; }

	[Required]
	public Guid ConcessionRequestConcessionTypeId { get; set; }

	[Required]
	public List<Guid> feeHeadIds { get; set; } = new();
}

// ─── Step 4 ───────────────────────────────────────────────────────────────────

public class SaveValueEntryRequest
{
	[Required]
	public List<ConcessionTypeValueBlock> ConcessionTypeValues { get; set; } = new();
}

public class ConcessionTypeValueBlock
{
	[Required]
	public Guid ConcessionTypeId { get; set; }
	[Required]
	public Guid ConcessionRequestConcessionTypeId { get; set; }

	[Required]
	public List<FeeHeadValueEntry> FeeHeadValues { get; set; } = new();
}

public class FeeHeadValueEntry
{
	[Required]
	public Guid FeeHeadId { get; set; }

	[Required]
	public DiscountBasedOn BasedOn { get; set; }

	[Required]
	public double Amount { get; set; }

	[Required]
	public double NetAmount { get; set; }
}

// ─── Step 5 — Document Upload ─────────────────────────────────────────────────

public class UploadDocumentsRequest
{
	[Required]
	public List<ConcessionTypeDocumentBlock> ConcessionTypeDocuments { get; set; } = new();
}

public class ConcessionTypeDocumentBlock
{
	[Required]
	public Guid ConcessionTypeId { get; set; }

	[Required]
	public Guid ConcessionRequestConcessionTypeId { get; set; }

	[Required]
	public List<DocumentUploadEntry> Documents { get; set; } = new();
}

public class DocumentUploadEntry
{
	[Required]
	public Guid DocumentId { get; set; }

	public string? Url { get; set; }

	/// <summary>0 = Pending, 1 = Uploaded.</summary>
	[Required]
	[Range(0, 1)]
	public int Status { get; set; }
}

// ─── Approve / Reject ─────────────────────────────────────────────────────────

public class ApprovalRequest
{
	public string? Message { get; set; }
}

public class RejectionRequest
{
	[Required]
	[MinLength(10)]
	[MaxLength(1000)]
	public string Message { get; set; }
}

// ─── Get Applications List ────────────────────────────────────────────────────

public class GetConcessionApplicationsRequest
{
	[Required]
	public Guid BoardId { get; set; }

	[Required]
	public Guid EducationMediumId { get; set; }

	public int? Status { get; set; }
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 20;
	public string? Search { get; set; }
}

// ─── Get Documents ────────────────────────────────────────────────────────────

public class GetDocumentsRequest
{
	public int? Status { get; set; }
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 20;
}
