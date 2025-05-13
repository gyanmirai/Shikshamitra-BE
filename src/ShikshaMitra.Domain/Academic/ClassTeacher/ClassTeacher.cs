using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Academic.ClassTeacher;

/// <summary>
/// Represents the assignment of a teacher to a specific division within a standard in the school.
/// </summary>
public class ClassTeacher : FullAuditedAggregateRoot<Guid>
{
	#region Properties
	/// <summary>
	/// Gets or sets the identifier of the standard (grade or class level).
	/// </summary>
	public Guid StandardId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the division (section within the standard).
    /// </summary>
    public Guid DivisionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the assigned teacher.
    /// </summary>
    public Guid TeacherId { get; set; }
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
	public ClassTeacher(Guid standardId, Guid divisionId, Guid teacherId)
	{
		StandardId = standardId;
		DivisionId = divisionId;
		TeacherId = teacherId;
	}
	#endregion

	#region Methods
	#endregion
}