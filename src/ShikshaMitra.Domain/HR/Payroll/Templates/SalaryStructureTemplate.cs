using System;
using System.Collections.Generic;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Templates;

public class SalaryStructureTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string? Description { get; private set; }
	public SalaryTemplateStatus Status { get; private set; }
	public bool IsDefault { get; private set; }
	public int LatestVersion { get; private set; }
	public int? PublishedVersion { get; private set; }

	protected SalaryStructureTemplate()
	{
	}

	public SalaryStructureTemplate(Guid id, Guid? tenantId, string name, string? description) : base(id)
	{
		TenantId = tenantId;
		SetDetails(name, description);
		Status = SalaryTemplateStatus.Draft;
		LatestVersion = 1;
	}

	public void SetDetails(string name, string? description)
	{
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), 160).Trim();
		Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
	}

	public int ReserveNextVersion() => ++LatestVersion;

	public void MarkPublished(int versionNumber)
	{
		if (versionNumber <= 0 || versionNumber > LatestVersion)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateVersionInvalid");
		}

		Status = SalaryTemplateStatus.Published;
		PublishedVersion = versionNumber;
	}

	public void SetDefault(bool value)
	{
		if (value && (Status != SalaryTemplateStatus.Published || !PublishedVersion.HasValue))
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateMustBePublished");
		}

		IsDefault = value;
	}

	public void Archive()
	{
		Status = SalaryTemplateStatus.Archived;
		IsDefault = false;
	}
}

public class SalaryStructureTemplateVersion : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid TemplateId { get; private set; }
	public int VersionNumber { get; private set; }
	public SalaryTemplateStatus Status { get; private set; }
	public string RulesJson { get; private set; } = "[]";
	public SalaryInputBasis DefaultSalaryBasis { get; private set; }
	public CompensationProtectionPolicy DefaultProtectionPolicy { get; private set; }
	public DateTime? PublishedAt { get; private set; }

	protected SalaryStructureTemplateVersion()
	{
	}

	public SalaryStructureTemplateVersion(
		Guid id,
		Guid? tenantId,
		Guid templateId,
		int versionNumber,
		IReadOnlyList<SalaryTemplateRule> rules,
		SalaryInputBasis defaultSalaryBasis = SalaryInputBasis.Gross,
		CompensationProtectionPolicy defaultProtectionPolicy = CompensationProtectionPolicy.ProtectGross) : base(id)
	{
		TenantId = tenantId;
		TemplateId = templateId;
		VersionNumber = versionNumber;
		Status = SalaryTemplateStatus.Draft;
		DefaultSalaryBasis = defaultSalaryBasis;
		DefaultProtectionPolicy = defaultProtectionPolicy;
		SetRules(rules);
	}

	public IReadOnlyList<SalaryTemplateRule> GetRules() =>
		JsonSerializer.Deserialize<List<SalaryTemplateRule>>(RulesJson) ?? [];

	public void SetRules(IReadOnlyList<SalaryTemplateRule> rules)
	{
		if (Status != SalaryTemplateStatus.Draft)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateVersionImmutable");
		}

		RulesJson = JsonSerializer.Serialize(rules ?? []);
	}

	public void SetQuoteDefaults(
		SalaryInputBasis defaultSalaryBasis,
		CompensationProtectionPolicy defaultProtectionPolicy)
	{
		if (Status != SalaryTemplateStatus.Draft)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateVersionImmutable");
		}

		DefaultSalaryBasis = defaultSalaryBasis;
		DefaultProtectionPolicy = defaultProtectionPolicy;
	}

	public void Publish(DateTime publishedAt)
	{
		if (Status != SalaryTemplateStatus.Draft)
		{
			throw new BusinessException("HR:Payroll:SalaryTemplateVersionImmutable");
		}

		Status = SalaryTemplateStatus.Published;
		PublishedAt = publishedAt;
	}
}

public sealed record SalaryTemplateRule(
	string Code,
	Guid ComponentId,
	SalaryTemplateLineKind LineKind,
	SalaryTemplateFormulaType FormulaType,
	PayrollComponentSemanticType SemanticType,
	decimal? Value,
	string? BasisCode,
	int DisplayOrder);
