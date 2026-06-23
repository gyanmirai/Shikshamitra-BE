using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

/// <summary>
/// Represents an inquiry preference (e.g., Online, Offline, Facebook, Walk-in)
/// This is a tenant-specific configuration entity
/// </summary>
public class InquiryPreference : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	/// <summary>
	/// Tenant ID for multi-tenancy support
	/// </summary>
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// The academic year this preference belongs to
	/// </summary>
	public Guid AcademicYearId { get; private set; }

	/// <summary>
	/// Name of the preference (e.g., "Online", "Offline", "Facebook")
	/// </summary>
	public string PreferenceName { get; private set; }

	/// <summary>
	/// Optional description for staff-facing context.
	/// </summary>
	public string? Description { get; private set; }

	/// <summary>
	/// Display order for settings and dropdowns.
	/// </summary>
	public int DisplayOrder { get; private set; }

	/// <summary>
	/// Indicates whether this source can be selected for new or changed inquiries.
	/// </summary>
	public bool IsActive { get; private set; }

	/// <summary>
	/// Optional reason captured when staff deactivate this source.
	/// </summary>
	public string? DeactivationReason { get; private set; }

	/// <summary>
	/// ORM constructor
	/// </summary>
	protected InquiryPreference() { }

	/// <summary>
	/// Creates a new inquiry preference
	/// </summary>
	internal InquiryPreference(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string preferenceName,
		string? description,
		int displayOrder) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		SetPreferenceName(preferenceName);
		SetDescription(description);
		SetDisplayOrder(displayOrder);
		IsActive = true;
	}

	/// <summary>
	/// Updates the inquiry preference
	/// </summary>
	public InquiryPreference Update(string preferenceName, string? description, int displayOrder)
	{
		SetPreferenceName(preferenceName);
		SetDescription(description);
		SetDisplayOrder(displayOrder);
		return this;
	}

	public void Activate()
	{
		IsActive = true;
		DeactivationReason = null;
	}

	public void Deactivate(string? deactivationReason)
	{
		IsActive = false;
		SetDeactivationReason(deactivationReason);
	}

	/// <summary>
	/// Sets the preference name with validation
	/// </summary>
	private InquiryPreference SetPreferenceName(string preferenceName)
	{
		PreferenceName = Check.NotNullOrWhiteSpace(
			preferenceName,
			nameof(preferenceName),
			maxLength: InquiryPreferenceConsts.MaxPreferenceNameLength
		);
		return this;
	}

	private InquiryPreference SetDescription(string? description)
	{
		Description = string.IsNullOrWhiteSpace(description)
			? null
			: Check.Length(
				description.Trim(),
				nameof(description),
				InquiryPreferenceConsts.MaxDescriptionLength
			);
		return this;
	}

	private InquiryPreference SetDisplayOrder(int displayOrder)
	{
		if (displayOrder < 0)
		{
			throw new BusinessException("InquiryPreference:InvalidDisplayOrder")
				.WithData("DisplayOrder", displayOrder);
		}

		DisplayOrder = displayOrder;
		return this;
	}

	private InquiryPreference SetDeactivationReason(string? deactivationReason)
	{
		DeactivationReason = string.IsNullOrWhiteSpace(deactivationReason)
			? null
			: Check.Length(
				deactivationReason.Trim(),
				nameof(deactivationReason),
				InquiryPreferenceConsts.MaxDeactivationReasonLength
			);
		return this;
	}
}
