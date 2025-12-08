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
	/// Name of the preference (e.g., "Online", "Offline", "Facebook")
	/// </summary>
	public string PreferenceName { get; private set; }

	/// <summary>
	/// ORM constructor
	/// </summary>
	protected InquiryPreference() { }

	/// <summary>
	/// Creates a new inquiry preference
	/// </summary>
	internal InquiryPreference(Guid id, Guid? tenantId, string preferenceName) : base(id)
	{
		TenantId = tenantId;
		SetPreferenceName(preferenceName);
	}

	/// <summary>
	/// Updates the inquiry preference
	/// </summary>
	public InquiryPreference Update(string preferenceName)
	{
		SetPreferenceName(preferenceName);
		return this;
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
}