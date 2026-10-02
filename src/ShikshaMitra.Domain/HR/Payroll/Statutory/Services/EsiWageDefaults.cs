using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.Statutory;

// Exact built-in earning identities only. Custom names must be explicitly reviewed.
public static class EsiWageDefaults
{
	public static readonly IReadOnlyDictionary<string, StatutoryWageTreatment> ByName =
		new Dictionary<string, StatutoryWageTreatment>(StringComparer.OrdinalIgnoreCase)
		{
			["Basic Salary"] = StatutoryWageTreatment.Included,
			["Dearness Allowance (DA)"] = StatutoryWageTreatment.Included,
			["House Rent Allowance (HRA)"] = StatutoryWageTreatment.SpecifiedExclusion,
			["Transport Allowance"] = StatutoryWageTreatment.SpecifiedExclusion,
			["Medical Allowance"] = StatutoryWageTreatment.Included,
			["Special Allowance"] = StatutoryWageTreatment.Included,
			["Performance Bonus"] = StatutoryWageTreatment.Included,
			["Incentive"] = StatutoryWageTreatment.Included
		};
}
