using System;
using Volo.Abp;

namespace ShikshaMitra.Students.Records.Settings;

public partial class HouseSettings
{
	public long MembershipRevision { get; private set; }

	public void TouchMembership() => MembershipRevision++;

	public void EnsureVersion(string expected)
	{
		if (string.IsNullOrWhiteSpace(expected) || expected != ConcurrencyStamp)
			throw new BusinessException(HouseManagementErrorCodes.StalePreview);
	}

	private static string ValidateName(string name)
	{
		if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
			throw new BusinessException(HouseManagementErrorCodes.InvalidInput);
		return name.Trim();
	}
}
