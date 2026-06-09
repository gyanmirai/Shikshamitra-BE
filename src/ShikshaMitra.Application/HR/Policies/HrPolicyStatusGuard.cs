using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

internal static class HrPolicyStatusGuard
{
	public static void EnsureEditable(HrPolicyStatus status)
	{
		if (status == HrPolicyStatus.Archived)
		{
			throw new BusinessException(HRErrorCodes.ArchivedPolicyCannotBeEdited);
		}
	}

	public static void EnsureArchiveUsesArchiveWorkflow(HrPolicyStatus status)
	{
		if (status == HrPolicyStatus.Archived)
		{
			throw new BusinessException(HRErrorCodes.PolicyArchiveRequiresArchiveWorkflow);
		}
	}

	public static void EnsureDefaultIsActive(HrPolicyStatus status)
	{
		if (status != HrPolicyStatus.Active)
		{
			throw new BusinessException(HRErrorCodes.DefaultPolicyMustBeActive);
		}
	}
}
