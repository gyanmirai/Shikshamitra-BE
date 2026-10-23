using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Collections.Dtos;

/// <summary>Read-only Finance result. Consumers cannot select a policy or submit calculated amounts.</summary>
public sealed class FeeProrationResultDto
{
	public Guid PolicyId { get; set; }
	public Guid PolicyVersionId { get; set; }
	public string PolicyName { get; set; } = string.Empty;
	public int PolicyVersion { get; set; }
	public DateTime JoiningDate { get; set; }
	public DateTime ChargeStartDate { get; set; }
	public DateTime ChargeEndDate { get; set; }
	public decimal ScheduledAmount { get; set; }
	public decimal AdmissionReduction { get; set; }
	public decimal WithdrawalCredit { get; set; }
	public decimal PayableAmount { get; set; }
	public decimal ExcessPaidAmount { get; set; }
	public List<string> Explanations { get; set; } = [];
}
