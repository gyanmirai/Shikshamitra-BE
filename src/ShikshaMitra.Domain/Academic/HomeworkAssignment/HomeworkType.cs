using System;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Represents a homework type (e.g., Written Assignment, Practical Work) 
/// associated with an academic medium
/// </summary>
public class HomeworkType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	/// <summary>
	/// Gets the tenant ID for multi-tenancy support
	/// </summary>
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// Gets the name of the homework type
	/// </summary>
	public string Name { get; private set; }

	/// <summary>
	/// Gets the associated academic medium ID
	/// </summary>
	public Guid AcademicMediumId { get; private set; }

	/// <summary>
	/// Navigation property to EducationMedium
	/// </summary>
	public virtual EducationMedium AcademicMedium { get; }

	/// <summary>
	/// Private constructor for EF Core
	/// </summary>
	private HomeworkType()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	/// <summary>
	/// Internal constructor for creating homework type through domain manager
	/// </summary>
	/// <param name="id">Unique identifier</param>
	/// <param name="name">Name of the homework type</param>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="tenantId">Tenant ID for multi-tenancy</param>
	internal HomeworkType(
		Guid id,
		string name,
		Guid academicMediumId,
		Guid? tenantId = null
	)
		: base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = Check.NotNull(academicMediumId, nameof(academicMediumId));
		SetName(name);
	}

	/// <summary>
	/// Sets the name with validation
	/// </summary>
	/// <param name="name">New name for the homework type</param>
	/// <returns>Current instance for method chaining</returns>
	internal HomeworkType SetName(string name)
	{
		Name = Check.NotNullOrWhiteSpace(
			name,
			nameof(name),
			maxLength: HomeworkTypeConsts.MaxNameLength
		);
		return this;
	}
}
