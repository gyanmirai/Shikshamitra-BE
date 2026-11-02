using System;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public class StudentFeeHeadDto
{
	public Guid FeeHeadId { get; set; }

	public string FeeHeadName { get; set; } = null!; // The response mapper populates these members after construction.

	public string? CategoryName { get; set; }

	public decimal Amount { get; set; }

	public bool IsOptional { get; set; }

	public bool IsOneTime { get; set; }

	public bool IsSelected { get; set; }

	// Reconstructed custom selection properties
	public decimal? SelectedAmount { get; set; }
	public MonthEnum? PayableMonth { get; set; }
	public int? PayableYear { get; set; }
	public MonthEnum? StartPayableMonth { get; set; }
	public int? StartPayableYear { get; set; }
	public MonthEnum? EndPayableMonth { get; set; }
	public int? EndPayableYear { get; set; }
}

