using System;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.Statutory;

public static class EsiManagement
{
	public const string InPlatform = "IN_PLATFORM";
	public const string External = "EXTERNAL";
	public static void Validate(string mode, Guid? policyId, string? reason, bool requirePolicy = true)
	{
		if (mode is not (InPlatform or External) || reason?.Length > 500 ||
			(mode == InPlatform && !string.IsNullOrWhiteSpace(reason)) ||
			(mode == InPlatform && requirePolicy && (policyId == null || policyId == Guid.Empty)) ||
			(mode == External && (policyId.HasValue || string.IsNullOrWhiteSpace(reason))))
			throw new BusinessException(HRErrorCodes.EsiPolicySelectionInvalid);
	}
}
