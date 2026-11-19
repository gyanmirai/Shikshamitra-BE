using System;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp;

namespace ShikshaMitra.TenantSettings.Divisions;

public partial class Division
{
	public Guid? LeaderEnrollmentId { get; private set; }
	public Guid? HouseLeaderEnrollmentId { get; private set; }
	public long LeadershipRevision { get; private set; }

	public void ReplaceLeader(StudentEnrollment? enrollment, string expectedStamp)
	{
		EnsureLeaderAppointment(enrollment, expectedStamp);
		LeaderEnrollmentId = enrollment?.Id;
		LeadershipRevision++;
	}

	public void ReplaceHouseLeader(StudentEnrollment? enrollment, string expectedStamp)
	{
		EnsureLeaderAppointment(enrollment, expectedStamp);
		if (enrollment != null && !enrollment.HouseId.HasValue)
			throw new BusinessException(HouseManagementErrorCodes.IneligibleLeader);
		HouseLeaderEnrollmentId = enrollment?.Id;
		LeadershipRevision++;
	}

	public bool HouseLeaderDeparted(Guid enrollmentId)
	{
		if (HouseLeaderEnrollmentId != enrollmentId)
			return false;
		HouseLeaderEnrollmentId = null;
		LeadershipRevision++;
		return true;
	}

	public void RosterMemberDeparted(Guid enrollmentId)
	{
		if (HouseLeaderEnrollmentId == enrollmentId)
			HouseLeaderEnrollmentId = null;
		if (LeaderEnrollmentId == enrollmentId)
			LeaderEnrollmentId = null;
		// Even a vacant slot must conflict with an appointment based on the old roster.
		LeadershipRevision++;
	}

	private void EnsureLeaderAppointment(StudentEnrollment? enrollment, string expectedStamp)
	{
		if (string.IsNullOrWhiteSpace(expectedStamp) || expectedStamp != ConcurrencyStamp)
			throw new BusinessException(HouseManagementErrorCodes.StalePreview);
		if (enrollment != null && (!enrollment.IsActive || enrollment.DivisionId != Id ||
			enrollment.StandardId != StandardId || enrollment.TenantId != TenantId))
			throw new BusinessException(HouseManagementErrorCodes.IneligibleLeader);
	}
}
