using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

internal static class LeavePolicyRuleSetGuard
{
	public static void EnsureRequiredActiveRules(IEnumerable<LeavePolicyRule> rules)
	{
		var activeRules = rules.Where(rule => rule.IsActive).ToList();
		var duplicate = activeRules
			.GroupBy(rule => rule.PaidType)
			.FirstOrDefault(group => group.Count() > 1);
		if (duplicate != null)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyDuplicatePaidTypeRule)
				.WithData("paidType", duplicate.Key);
		}

		if (activeRules.Count != 2 ||
		    activeRules.All(rule => rule.PaidType != LeavePaidType.Paid) ||
		    activeRules.All(rule => rule.PaidType != LeavePaidType.Unpaid))
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyPaidUnpaidRulesRequired);
		}
	}
}
