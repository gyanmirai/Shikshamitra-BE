using System;

namespace ShikshaMitra.Finance.Fees.Charges.Dtos;

public class CreateChargeRequest
{
	public Guid ChargeCategoryId { get; set; }

	public string PurposeDescription { get; set; } = string.Empty;

	public decimal Amount { get; set; }

	public DateTime DueDate { get; set; }

	public int Priority { get; set; }

	public Guid StudentId { get; set; }

	public string? AdditionalNotes { get; set; }
}

public class UpdateChargeRequest
{
	public Guid? ChargeCategoryId { get; set; }

	public string? PurposeDescription { get; set; }

	public decimal? Amount { get; set; }

	public DateTime? DueDate { get; set; }

	public int? Priority { get; set; }

	public string? AdditionalNotes { get; set; }
}

public class ApproveChargeRequest
{
	public string? ApprovalNotes { get; set; }
}

public class RejectChargeRequest
{
	public string RejectionReason { get; set; } = string.Empty;
}

public class GetChargesRequest
{
	public int Page { get; set; } = 1;

	public int PageSize { get; set; } = 20;

	public string? Search { get; set; }

	public int Status { get; set; } = 0; // Default to PENDING

	public string? SortBy { get; set; }

	public string? SortOrder { get; set; } = "asc";

	public Guid? EducationMediumId { get; set; }

	public Guid? BoardId { get; set; }
}
