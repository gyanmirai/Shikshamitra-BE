using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmSetupFormat
{
	internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	internal static readonly string[] SettingsKeys = ["journey", "inquiry-capture", "entrance-exams", "registration-documents", "offers-seats", "routing-scoring", "activation", "governance", "communications", "automation"];
	internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
	internal static void EnsureHostContext(Guid? tenantId)
	{
		if (tenantId is not null) throw new BusinessException("AdmissionCrm:PlatformOperatorRequiresHostContext");
	}
	internal static AdmissionCrmSupportSessionDto ToDto(AdmissionCrmPlatformSupportSession session, DateTime now) => new()
	{ Id = session.Id, TargetTenantId = session.TargetTenantId, Reason = session.Reason, Reference = session.Reference, ExpiresAt = session.ExpiresAt, EndedAt = session.EndedAt, IsActive = session.IsActive(now) };
	internal static AdmissionCrmPlatformPresetDto ToDto(AdmissionCrmPlatformPreset preset) => new()
	{ Id = preset.Id, Code = preset.Code, Name = preset.Name, Version = preset.Version, Status = preset.Status, ReleaseNotes = preset.ReleaseNotes, PublishedAt = preset.PublishedAt };
}