using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Reports;

public class HrReportTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public const string PrivateVisibility = "private";
	public const string OrganizationVisibility = "organization";
	public const string DraftStatus = "Draft";
	public const string PublishedStatus = "Published";
	public const string ArchivedStatus = "Archived";

	public Guid? TenantId { get; private set; }
	public Guid OwnerUserId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string Description { get; private set; } = string.Empty;
	public string Category { get; private set; } = string.Empty;
	public string DataSource { get; private set; } = string.Empty;
	public string Visibility { get; private set; } = PrivateVisibility;
	public string Status { get; private set; } = DraftStatus;
	public string DefinitionJson { get; private set; } = "{}";
	public int DefinitionVersion { get; private set; }
	public int Revision { get; private set; }
	public int PublishedRevision { get; private set; }
	public string? PublishedDefinitionJson { get; private set; }
	public string? PublishedName { get; private set; }
	public string? PublishedDescription { get; private set; }

	public void SaveDesign(string name, string description, string category, string dataSource, string visibility, string definitionJson, bool publish)
	{
		if (DefinitionVersion == 2 && Visibility != visibility) throw new BusinessException(HRErrorCodes.ReportDesignInvalid).WithData("errors", "Copy the template to change its visibility.");
		if (PublishedRevision > 0 && DataSource != dataSource) throw new BusinessException(HRErrorCodes.ReportDesignInvalid).WithData("errors", "Copy the template to change a published dataset.");
		Update(name, description, category, dataSource, visibility, PublishedRevision > 0 ? PublishedStatus : DraftStatus, definitionJson);
		DefinitionVersion = 2; Revision++;
		if (publish || visibility == PrivateVisibility)
		{
			PublishedRevision = Revision; PublishedDefinitionJson = definitionJson; PublishedName = Name; PublishedDescription = Description;
			Status = PublishedStatus;
		}
	}
	public void ArchiveDesign() => Status = ArchivedStatus;

	private HrReportTemplate()
	{
	}

	public HrReportTemplate(
		Guid id,
		Guid? tenantId,
		Guid ownerUserId,
		string name,
		string? description,
		string category,
		string dataSource,
		string visibility,
		string status,
		string definitionJson) : base(id)
	{
		TenantId = tenantId;
		OwnerUserId = ownerUserId;
		Update(name, description, category, dataSource, visibility, status, definitionJson);
	}

	public void Update(
		string name,
		string? description,
		string category,
		string dataSource,
		string visibility,
		string status,
		string definitionJson)
	{
		Name = RequireTrim(name, nameof(name), 128);
		Description = Trim(description, 512) ?? string.Empty;
		Category = RequireTrim(category, nameof(category), 64);
		DataSource = RequireTrim(dataSource, nameof(dataSource), 64);
		Visibility = NormalizeVisibility(visibility);
		Status = NormalizeStatus(status);
		DefinitionJson = string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson;
	}

	public HrReportTemplate Clone(Guid id, Guid ownerUserId)
	{
		return new HrReportTemplate(
			id,
			TenantId,
			ownerUserId,
			$"{Name} Copy",
			Description,
			Category,
			DataSource,
			PrivateVisibility,
			DraftStatus,
			DefinitionJson);
	}

	private static string NormalizeVisibility(string visibility)
	{
		return string.Equals(visibility, OrganizationVisibility, StringComparison.OrdinalIgnoreCase)
			? OrganizationVisibility
			: PrivateVisibility;
	}

	private static string NormalizeStatus(string status)
	{
		if (string.Equals(status, PublishedStatus, StringComparison.OrdinalIgnoreCase))
		{
			return PublishedStatus;
		}

		if (string.Equals(status, ArchivedStatus, StringComparison.OrdinalIgnoreCase))
		{
			return ArchivedStatus;
		}

		return DraftStatus;
	}

	private static string RequireTrim(string value, string name, int max)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new BusinessException(HRErrorCodes.ReportTemplateFieldRequired).WithData("field", name);
		}

		return value.Trim()[..Math.Min(value.Trim().Length, max)];
	}

	private static string? Trim(string? value, int max)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
	}
}