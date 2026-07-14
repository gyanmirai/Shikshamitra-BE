using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPlaybook : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public int Version { get; private set; }
	public AdmissionCrmPlaybookStatus Status { get; private set; }
	public bool IsFallback { get; private set; }
	public AdmissionCrmRegistrationPath RegistrationPath { get; private set; }
	public string ApplicabilityJson { get; private set; } = "{}";
	public string StagesJson { get; private set; } = "[]";
	public string RoundsJson { get; private set; } = "[]";
	public string SeatPlansJson { get; private set; } = "[]";
	public string ExamDefinitionsJson { get; private set; } = "[]";
	public string SlotTemplatesJson { get; private set; } = "[]";
	public string AssignmentRulesJson { get; private set; } = "[]";
	public string? Notes { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public DateTime? RetiredAt { get; private set; }

	protected AdmissionCrmPlaybook()
	{
	}

	public AdmissionCrmPlaybook(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string name,
		int version,
		bool isFallback,
		AdmissionCrmRegistrationPath registrationPath,
		string applicabilityJson,
		string stagesJson,
		string roundsJson,
		string seatPlansJson,
		string examDefinitionsJson,
		string slotTemplatesJson,
		string assignmentRulesJson,
		string? notes)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		if (version <= 0)
		{
			throw new BusinessException("AdmissionCrm:InvalidPlaybookVersion");
		}

		Version = version;
		Status = AdmissionCrmPlaybookStatus.Draft;
		Update(
			name,
			isFallback,
			registrationPath,
			applicabilityJson,
			stagesJson,
			roundsJson,
			seatPlansJson,
			examDefinitionsJson,
			slotTemplatesJson,
			assignmentRulesJson,
			notes);
	}

	public void Update(
		string name,
		bool isFallback,
		AdmissionCrmRegistrationPath registrationPath,
		string applicabilityJson,
		string stagesJson,
		string roundsJson,
		string seatPlansJson,
		string examDefinitionsJson,
		string slotTemplatesJson,
		string assignmentRulesJson,
		string? notes)
	{
		EnsureDraft();
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCrmConsts.MaxTemplateNameLength);
		IsFallback = isFallback;
		RegistrationPath = registrationPath;
		ApplicabilityJson = ValidateJson(applicabilityJson, nameof(applicabilityJson), JsonValueKind.Object);
		StagesJson = ValidateJson(stagesJson, nameof(stagesJson), JsonValueKind.Array);
		RoundsJson = ValidateJson(roundsJson, nameof(roundsJson), JsonValueKind.Array);
		SeatPlansJson = ValidateJson(seatPlansJson, nameof(seatPlansJson), JsonValueKind.Array);
		ExamDefinitionsJson = ValidateJson(examDefinitionsJson, nameof(examDefinitionsJson), JsonValueKind.Array);
		SlotTemplatesJson = ValidateJson(slotTemplatesJson, nameof(slotTemplatesJson), JsonValueKind.Array);
		AssignmentRulesJson = ValidateJson(assignmentRulesJson, nameof(assignmentRulesJson), JsonValueKind.Array);
		Notes = NormalizeOptional(notes, nameof(notes), AdmissionCrmConsts.MaxNotesLength);
	}

	public void Publish(DateTime publishedAt)
	{
		EnsureDraft();
		Status = AdmissionCrmPlaybookStatus.Published;
		PublishedAt = publishedAt;
		RetiredAt = null;
	}

	public void Retire(DateTime retiredAt)
	{
		if (Status != AdmissionCrmPlaybookStatus.Published)
		{
			throw new BusinessException("AdmissionCrm:OnlyPublishedPlaybookCanBeRetired");
		}

		Status = AdmissionCrmPlaybookStatus.Retired;
		RetiredAt = retiredAt;
	}

	private void EnsureDraft()
	{
		if (Status != AdmissionCrmPlaybookStatus.Draft)
		{
			throw new BusinessException("AdmissionCrm:PublishedPlaybookIsImmutable")
				.WithData("PlaybookId", Id)
				.WithData("Status", Status);
		}
	}

	private static string ValidateJson(string json, string name, JsonValueKind expectedKind)
	{
		var normalized = Check.NotNullOrWhiteSpace(
			json,
			name,
			maxLength: AdmissionCrmConsts.MaxPlaybookJsonLength);
		using var document = JsonDocument.Parse(normalized);
		if (document.RootElement.ValueKind != expectedKind)
		{
			throw new BusinessException("AdmissionCrm:InvalidPlaybookJson")
				.WithData("Field", name);
		}

		return normalized;
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}
}
