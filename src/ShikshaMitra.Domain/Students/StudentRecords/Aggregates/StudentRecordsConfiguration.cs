using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentRecords;

public sealed class StudentRecordsConfiguration : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public string DraftPolicyJson { get; private set; } = "{}";
	public Guid? PublishedVersionId { get; private set; }
	public int PublishedVersionNumber { get; private set; }
	private StudentRecordsConfiguration() { }
	public StudentRecordsConfiguration(Guid id, Guid tenantId, Guid? academicYearId, string policyJson) : base(id)
	{
		if (tenantId == Guid.Empty || academicYearId == Guid.Empty) throw new BusinessException(StudentRecordsErrorCodes.InvalidConfigurationScope);
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		SaveDraft(policyJson);
	}
	public void SaveDraft(string policyJson)
	{
		Check.NotNullOrWhiteSpace(policyJson, nameof(policyJson), 100000);
		DraftPolicyJson = StudentRecordsPolicyValidator.ParseAndValidate(policyJson).ToJson();
	}
	public void Publish(Guid versionId)
	{
		if (versionId == Guid.Empty) throw new BusinessException(StudentRecordsErrorCodes.InvalidPublishedVersion);
		PublishedVersionId = versionId;
		PublishedVersionNumber++;
	}
}
