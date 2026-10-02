using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Attendance;

public sealed class StudentAttendanceConfiguration : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public string DraftPolicyJson { get; private set; } = "{}";
	public int PublishedVersionNumber { get; private set; }
	private StudentAttendanceConfiguration() { }
	public StudentAttendanceConfiguration(Guid id, Guid tenantId, Guid? academicYearId, string policyJson) : base(id)
	{
		if (tenantId == Guid.Empty || academicYearId == Guid.Empty) throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy);
		TenantId = tenantId; AcademicYearId = academicYearId; SaveDraft(policyJson);
	}
	public void SaveDraft(string policyJson)
	{
		Check.NotNullOrWhiteSpace(policyJson, nameof(policyJson), 100000);
		var policy = AttendancePolicy.FromJson(policyJson); AttendancePolicy.Validate(policy); DraftPolicyJson = policy.ToJson();
	}
	public void Publish() => PublishedVersionNumber++;
}
