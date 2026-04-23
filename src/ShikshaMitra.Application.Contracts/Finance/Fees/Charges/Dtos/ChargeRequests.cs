using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Charges.Dtos;

public class CreateChargeRequest
{
	[Required]
	public Guid ChargeCategoryId { get; set; }

	[Required]
	[StringLength(1000, MinimumLength = 10)]
	public string PurposeDescription { get; set; }

	[Required]
	[Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
	public decimal Amount { get; set; }

	[Required]
	public DateTime DueDate { get; set; }

	[Required]
	[Range(0, 2)]
	public int Priority { get; set; }

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

	[StringLength(2000)]
	public string? AdditionalNotes { get; set; }
}

public class UpdateChargeRequest
{
	public Guid? ChargeCategoryId { get; set; }

	[StringLength(1000, MinimumLength = 10)]
	public string? PurposeDescription { get; set; }

	[Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
	public decimal? Amount { get; set; }

	public DateTime? DueDate { get; set; }

	[Range(0, 2)]
	public int? Priority { get; set; }

	[StringLength(2000)]
	public string? AdditionalNotes { get; set; }
}

public class ApproveChargeRequest
{
	[StringLength(2000)]
	public string? ApprovalNotes { get; set; }
}

public class RejectChargeRequest
{
	[Required]
	[StringLength(2000, MinimumLength = 10, ErrorMessage = "Rejection reason must be at least 10 characters")]
	public string RejectionReason { get; set; }
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
