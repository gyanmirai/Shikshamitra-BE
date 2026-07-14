using System;
using System.ComponentModel.DataAnnotations.Schema;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum.Settings;

public class Subject : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	#region Properties
	public Guid? TenantId { get; private set; }
	public string Name { get; set; }
	public string? DisplayName { get; set; }
	public string? Category { get; set; }
	public string Color { get; set; }
	public string? Icon { get; set; }
	public string? Description { get; set; }
	public SubjectMasterStatus Status { get; set; }
	public int? SortOrder { get; set; }
	public string? ArchiveReason { get; set; }
	public Guid EducationMediumId { get; set; }
	public EducationMedium EducationMedium { get; set; }
	#endregion

	#region Constructors
	private Subject()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public Subject(
		Guid id,
		Guid? tenantId,
		string name,
		string color,
		Guid educationMediumId,
		string? displayName = null,
		string? category = null,
		string? icon = null,
		string? description = null,
		SubjectMasterStatus status = SubjectMasterStatus.Active,
		int? sortOrder = null
	)
		: base(id)
	{
		TenantId = tenantId;
		SetName(name);
		SetColor(color);
		SetMasterDetails(displayName, category, icon, description, status, sortOrder);
		EducationMediumId = educationMediumId;
	}
	#endregion

	#region Methods
	public void SetName(string name)
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));
		Name = name;
	}

	public void SetColor(string color) => Color = color;

	public void SetMasterDetails(
		string? displayName,
		string? category,
		string? icon,
		string? description,
		SubjectMasterStatus status,
		int? sortOrder
	)
	{
		DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
		Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
		Icon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
		Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
		Status = status;
		SortOrder = sortOrder;
		if (status != SubjectMasterStatus.Archived)
		{
			ArchiveReason = null;
		}
	}

	public void Archive(string? reason)
	{
		Status = SubjectMasterStatus.Archived;
		ArchiveReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
	}

	public void Restore()
	{
		Status = SubjectMasterStatus.Active;
		ArchiveReason = null;
	}
	#endregion
}
