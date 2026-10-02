using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Attendance;

public sealed class StudentAttendancePolicyVersion : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ConfigurationId { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public int VersionNumber { get; private set; }
	public DateTime EffectiveFrom { get; private set; }
	public string PolicyJson { get; private set; } = "{}";
	private StudentAttendancePolicyVersion() { }
	public StudentAttendancePolicyVersion(Guid id, Guid tenantId, Guid configurationId, Guid? yearId, int version,
		DateTime effectiveFrom, string policyJson) : base(id)
	{
		if (tenantId == Guid.Empty || configurationId == Guid.Empty || version < 1) throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy);
		var policy = AttendancePolicy.FromJson(policyJson); AttendancePolicy.Validate(policy);
		TenantId = tenantId; ConfigurationId = configurationId; AcademicYearId = yearId; VersionNumber = version;
		EffectiveFrom = effectiveFrom.Date; PolicyJson = policy.ToJson();
	}
}
