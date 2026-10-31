using System;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp;

namespace ShikshaMitra.TenantSettings.Divisions;

public partial class Division
{
	public Guid? LeaderEnrollmentId { get; private set; }
	public long LeadershipRevision { get; private set; }

	public void ReplaceLeader(StudentEnrollment? enrollment, string expectedStamp)
	{
		if (string.IsNullOrWhiteSpace(expectedStamp) || expectedStamp != ConcurrencyStamp)
			throw new BusinessException(HouseManagementErrorCodes.StalePreview);
		if (enrollment != null && (!enrollment.IsActive || enrollment.DivisionId != Id || enrollment.TenantId != TenantId))
			throw new BusinessException(HouseManagementErrorCodes.IneligibleLeader);
		LeaderEnrollmentId = enrollment?.Id;
		LeadershipRevision++;
	}
}
