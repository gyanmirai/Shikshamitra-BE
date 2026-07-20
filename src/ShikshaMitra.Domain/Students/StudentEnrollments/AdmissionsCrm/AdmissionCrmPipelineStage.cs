using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPipelineStage : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public AdmissionInquiryStage SystemStage { get; private set; }
	public string StageKey { get; private set; } = string.Empty;
	public AdmissionInquiryStage BaseSystemStage { get; private set; }
	public string StageName { get; private set; } = string.Empty;
	public int DisplayOrder { get; private set; }
	public bool IsEnabled { get; private set; }
	public bool IsSystem { get; private set; }
	public string? ColorHex { get; private set; }
	public int? SlaTargetHours { get; private set; }
	public string? OwnerResponsibility { get; private set; }
	public string? AllowedNextStagesJson { get; private set; }

	protected AdmissionCrmPipelineStage()
	{
	}

	public AdmissionCrmPipelineStage(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		AdmissionInquiryStage systemStage,
		string stageName,
		int displayOrder,
		string? colorHex = null,
		int? slaTargetHours = null,
		string? ownerResponsibility = null,
		string? allowedNextStagesJson = null,
		bool isSystem = true)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		SystemStage = systemStage;
		StageKey = systemStage.ToString();
		BaseSystemStage = systemStage;
		IsSystem = isSystem;
		IsEnabled = true;
		Update(
			stageName,
			displayOrder,
			isEnabled: true,
			colorHex,
			slaTargetHours,
			ownerResponsibility,
			allowedNextStagesJson);
	}

	public static AdmissionCrmPipelineStage CreateCustom(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string stageKey,
		AdmissionInquiryStage baseSystemStage,
		string stageName,
		int displayOrder,
		string? colorHex = null,
		int? slaTargetHours = null,
		string? ownerResponsibility = null,
		string? allowedNextStagesJson = null)
	{
		var stage = new AdmissionCrmPipelineStage(
			id,
			tenantId,
			academicYearId,
			baseSystemStage,
			stageName,
			displayOrder,
			colorHex,
			slaTargetHours,
			ownerResponsibility,
			allowedNextStagesJson,
			isSystem: false);
		stage.StageKey = NormalizeStageKey(stageKey);
		stage.BaseSystemStage = baseSystemStage;
		return stage;
	}

	public void Update(
		string stageName,
		int displayOrder,
		bool isEnabled,
		string? colorHex = null,
		int? slaTargetHours = null,
		string? ownerResponsibility = null,
		string? allowedNextStagesJson = null)
	{
		StageName = Check.NotNullOrWhiteSpace(
			stageName,
			nameof(stageName),
			maxLength: AdmissionCrmConsts.MaxStageNameLength);

		if (displayOrder < 0)
		{
			throw new BusinessException("AdmissionCrm:InvalidPipelineStageOrder")
				.WithData("DisplayOrder", displayOrder);
		}

		DisplayOrder = displayOrder;
		IsEnabled = isEnabled;
		ColorHex = NormalizeColorHex(colorHex);
		SlaTargetHours = NormalizeSlaTargetHours(slaTargetHours);
		OwnerResponsibility = NormalizeOptionalText(
			ownerResponsibility,
			nameof(ownerResponsibility),
			AdmissionCrmConsts.MaxStageOwnerResponsibilityLength);
		AllowedNextStagesJson = NormalizeOptionalText(
			allowedNextStagesJson,
			nameof(allowedNextStagesJson),
			AdmissionCrmConsts.MaxPipelineAllowedNextStagesJsonLength);
	}

	private static string NormalizeStageKey(string stageKey)
	{
		var normalized = Check.NotNullOrWhiteSpace(stageKey, nameof(stageKey), AdmissionCrmConsts.MaxPlaybookCodeLength)
			.Trim()
			.ToUpperInvariant();
		foreach (var character in normalized)
		{
			if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
			{
				throw new BusinessException("AdmissionCrm:InvalidPipelineStageKey")
					.WithData("StageKey", normalized);
			}
		}

		return normalized;
	}

	private static string? NormalizeColorHex(string? colorHex)
	{
		var normalized = colorHex?.Trim();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return null;
		}

		if (normalized.Length != 4 && normalized.Length != 7)
		{
			throw new BusinessException("AdmissionCrm:InvalidPipelineStageColor")
				.WithData("ColorHex", normalized);
		}

		if (normalized[0] != '#')
		{
			throw new BusinessException("AdmissionCrm:InvalidPipelineStageColor")
				.WithData("ColorHex", normalized);
		}

		for (var index = 1; index < normalized.Length; index++)
		{
			if (!Uri.IsHexDigit(normalized[index]))
			{
				throw new BusinessException("AdmissionCrm:InvalidPipelineStageColor")
					.WithData("ColorHex", normalized);
			}
		}

		return normalized.ToUpperInvariant();
	}

	private static int? NormalizeSlaTargetHours(int? slaTargetHours)
	{
		if (!slaTargetHours.HasValue || slaTargetHours.Value == 0)
		{
			return null;
		}

		if (slaTargetHours.Value < 0 || slaTargetHours.Value > 720)
		{
			throw new BusinessException("AdmissionCrm:InvalidPipelineStageSlaTarget")
				.WithData("SlaTargetHours", slaTargetHours.Value);
		}

		return slaTargetHours.Value;
	}

	private static string? NormalizeOptionalText(string? value, string parameterName, int maxLength)
	{
		var normalized = value?.Trim();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return null;
		}

		return Check.Length(normalized, parameterName, maxLength);
	}
}
