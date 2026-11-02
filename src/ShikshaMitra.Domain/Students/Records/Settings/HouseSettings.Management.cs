using System;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp;

namespace ShikshaMitra.Students.Records.Settings;

public partial class HouseSettings
{
	public Guid? LeaderEnrollmentId { get; private set; }
	public long MembershipRevision { get; private set; }

	public void TouchMembership() => MembershipRevision++;
	public void ReplaceLeader(StudentEnrollment? enrollment)
	{
		if (enrollment != null && (!enrollment.IsActive || enrollment.HouseId != Id || enrollment.TenantId != TenantId))
			throw new BusinessException(HouseManagementErrorCodes.IneligibleLeader);
		LeaderEnrollmentId = enrollment?.Id;
		TouchMembership();
	}

	public void MemberDeparted(Guid enrollmentId)
	{
		if (LeaderEnrollmentId == enrollmentId)
			LeaderEnrollmentId = null;
		TouchMembership();
	}

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
