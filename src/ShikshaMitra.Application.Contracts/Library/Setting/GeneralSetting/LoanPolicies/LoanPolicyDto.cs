using System;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LoanPolicies;

public class LoanPolicyDto
{
	public Guid Id { get; set; }
	public Guid RoleId { get; set; }
	public int LoanDurationInDays { get; set; }
	public int MaxBooksAllowed { get; set; }
	public decimal LateReturnFinePerDay { get; set; }
}
