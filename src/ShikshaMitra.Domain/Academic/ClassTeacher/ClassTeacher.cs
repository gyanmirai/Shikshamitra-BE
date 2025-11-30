using System;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.ClassTeacher;

/// <summary>
/// Represents the assignment of a teacher to a specific division within a standard in the school.
/// </summary>
public class ClassTeacher : FullAuditedAggregateRoot<Guid>, IMultiTenant, IUserScopedEntity
{
	#region Properties
	/// <summary>
	/// Gets or sets the identifier of the standard (grade or class level).
	/// </summary>
	public Guid StandardId { get; private set; }

	/// <summary>
	/// Gets or sets the identifier of the division (section within the standard).
	/// </summary>
	public Guid DivisionId { get; private set; }

	/// <summary>
	/// Gets or sets the identifier of the assigned teacher.
	/// </summary>
	public Guid TeacherId { get; set; }

	/// <summary>
	/// Gets or sets the identifier of the academic medium (e.g., English, Hindi).
	/// </summary>
	public Guid AcademicMediumId { get; private set; }

	/// <summary>
	/// Gets or sets the tenant identifier for multi-tenancy support.
	/// </summary>
	public virtual Guid? TenantId { get; private set; }
	
	// IUserScopedEntity implementation
	public Guid? EducationBoardId { get; private set; }
	public Guid? EducationMediumId { get; private set; }
	public Guid? EducationMediumStageId { get; private set; }
	#endregion

	#region IUserScopedEntity Explicit Implementation
	Guid? IUserScopedEntity.StandardId => StandardId;
	Guid? IUserScopedEntity.DivisionId => DivisionId;
	#endregion

	#region Constructors
	/// <summary>
	/// Initializes a new instance of the <see cref="ClassTeacher"/> class.
	/// </summary>
	protected ClassTeacher()
	{
		// Required for EF Core
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ClassTeacher"/> class with required fields.
	/// </summary>
	/// <param name="standardId">The standard identifier.</param>
	/// <param name="divisionId">The division identifier.</param>
	/// <param name="teacherId">The teacher identifier.</param>
	public ClassTeacher(
		Guid id,
		Guid? tenantId,
		Guid standardId,
		Guid divisionId,
		Guid teacherId,
		Guid academicMediumId,
		Guid? educationBoardId = null,
		Guid? educationMediumId = null,
		Guid? educationMediumStageId = null
	)
	{
		Id = id;
		TenantId = tenantId;
		StandardId = standardId;
		DivisionId = divisionId;
		TeacherId = teacherId;
		AcademicMediumId = academicMediumId;
		EducationBoardId = educationBoardId;
		EducationMediumId = educationMediumId;
		EducationMediumStageId = educationMediumStageId;
	}
	#endregion

	#region Methods
	#endregion
}
