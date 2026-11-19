using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionIntakeProfile : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public AdmissionIntakeProfileKind Kind { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public string ConfigurationJson { get; private set; } = "{}";

	private AdmissionIntakeProfile() { }

	public AdmissionIntakeProfile(Guid id, Guid? tenantId, AdmissionIntakeProfileKind kind,
		string code, string name, string configurationJson) : base(id)
	{
		TenantId = tenantId;
		if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
		Kind = kind;
		Update(code, name, configurationJson);
	}

	public void Update(string code, string name, string configurationJson)
	{
		Code = Check.NotNullOrWhiteSpace(code, nameof(code), 64).Trim().ToUpperInvariant();
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), 128).Trim();
		ConfigurationJson = Check.NotNullOrWhiteSpace(configurationJson,
			nameof(configurationJson), 8192).Trim();
	}
}
