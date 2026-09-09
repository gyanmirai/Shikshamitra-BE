using System;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public class StudentFeeHeadDetailDto
{
	[Required]
	public Guid FeeHeadId { get; set; }

	[Required]
	public decimal Amount { get; set; }

	// For one-time optional fees
	public MonthEnum? PayableMonth { get; set; }
	public int? PayableYear { get; set; }

	// For recurring optional fees
	public MonthEnum? StartPayableMonth { get; set; }
	public int? StartPayableYear { get; set; }
	public MonthEnum? EndPayableMonth { get; set; }
	public int? EndPayableYear { get; set; }

	public DateTime? ChargeStartDate { get; set; }
	public DateTime? ChargeEndDate { get; set; }
}
