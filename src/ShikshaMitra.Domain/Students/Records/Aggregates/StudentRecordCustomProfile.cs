using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

/// <summary>Current custom answers only. Admission definitions and submitted evidence remain immutable.</summary>
public class StudentRecordCustomProfile : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid BaselineSubmissionId { get; private set; }
	public string SourcesJson { get; private set; } = "[]";
	public string SchemaJson { get; private set; } = "[]";
	public string ValuesJson { get; private set; } = "{}";
	protected StudentRecordCustomProfile() { }
	public StudentRecordCustomProfile(Guid id, Guid? tenantId, Guid studentId, Guid baselineSubmissionId,
		string sourcesJson, string schemaJson, string valuesJson) : base(id)
	{
		TenantId = tenantId;
		StudentId = Check.NotDefaultOrNull<Guid>(studentId, nameof(studentId));
		BaselineSubmissionId = Check.NotDefaultOrNull<Guid>(baselineSubmissionId, nameof(baselineSubmissionId));
		UpdateCurrent(sourcesJson, schemaJson, valuesJson);
	}
	public void UpdateCurrent(string sourcesJson, string schemaJson, string valuesJson)
	{
		SourcesJson = Check.NotNullOrWhiteSpace(sourcesJson, nameof(sourcesJson));
		SchemaJson = Check.NotNullOrWhiteSpace(schemaJson, nameof(schemaJson));
		ValuesJson = Check.NotNullOrWhiteSpace(valuesJson, nameof(valuesJson));
	}
}
