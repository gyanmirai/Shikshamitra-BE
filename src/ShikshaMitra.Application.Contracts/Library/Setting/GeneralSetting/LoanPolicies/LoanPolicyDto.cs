using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LoanPolicies;

public class LoanPolicyDto
{
	public Guid Id { get; set; }
	public Guid RoleId { get; set; }
	public int LoanDurationInDays { get; set; }
	public int MaxBooksAllowed { get; set; }
	public decimal LateReturnFinePerDay { get; set; }
}
