using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentRecords;

/// <summary>Published versions cannot be edited; requests retain the exact published rules.</summary>
public sealed class StudentRecordsPolicyVersion : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ConfigurationId { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public int VersionNumber { get; private set; }
	public string PolicyJson { get; private set; } = "{}";
	private StudentRecordsPolicyVersion() { }
	public StudentRecordsPolicyVersion(Guid id, Guid tenantId, Guid configurationId, Guid? academicYearId, int versionNumber, string policyJson) : base(id)
	{
		if (tenantId == Guid.Empty || configurationId == Guid.Empty || academicYearId == Guid.Empty || versionNumber < 1)
			throw new BusinessException(StudentRecordsErrorCodes.InvalidPublishedVersion);
		TenantId = tenantId;
		ConfigurationId = configurationId;
		AcademicYearId = academicYearId;
		VersionNumber = versionNumber;
		Check.NotNullOrWhiteSpace(policyJson, nameof(policyJson), 100000);
		PolicyJson = StudentRecordsPolicyValidator.ParseAndValidate(policyJson).ToJson();
	}
}
