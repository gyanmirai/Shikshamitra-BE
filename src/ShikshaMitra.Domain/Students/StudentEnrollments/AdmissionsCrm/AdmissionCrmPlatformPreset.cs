using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPlatformPreset : FullAuditedAggregateRoot<Guid>
{
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public int Version { get; private set; }
	public AdmissionCrmPlatformPresetStatus Status { get; private set; }
	public string ConfigurationJson { get; private set; } = "{}";
	public string AccessPolicyJson { get; private set; } = "[]";
	public string? ReleaseNotes { get; private set; }
	public DateTime? PublishedAt { get; private set; }

	protected AdmissionCrmPlatformPreset() { }

	public AdmissionCrmPlatformPreset(Guid id, string code, string name, int version, string configurationJson, string accessPolicyJson, string? releaseNotes) : base(id)
	{
		if (version <= 0) throw new BusinessException("AdmissionCrm:InvalidPlatformPresetVersion");
		Code = Check.NotNullOrWhiteSpace(code, nameof(code), 64);
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), 160);
		Version = version;
		ConfigurationJson = Check.NotNullOrWhiteSpace(configurationJson, nameof(configurationJson), 1000000);
		AccessPolicyJson = Check.NotNullOrWhiteSpace(accessPolicyJson, nameof(accessPolicyJson), 100000);
		ReleaseNotes = string.IsNullOrWhiteSpace(releaseNotes) ? null : Check.Length(releaseNotes.Trim(), nameof(releaseNotes), 2000);
		Status = AdmissionCrmPlatformPresetStatus.Draft;
	}

	public void Publish(DateTime now)
	{
		if (Status != AdmissionCrmPlatformPresetStatus.Draft) throw new BusinessException("AdmissionCrm:PlatformPresetNotDraft");
		Status = AdmissionCrmPlatformPresetStatus.Published;
		PublishedAt = now;
	}

	public void Retire()
	{
		if (Status != AdmissionCrmPlatformPresetStatus.Published) throw new BusinessException("AdmissionCrm:PlatformPresetNotPublished");
		Status = AdmissionCrmPlatformPresetStatus.Retired;
	}
}
