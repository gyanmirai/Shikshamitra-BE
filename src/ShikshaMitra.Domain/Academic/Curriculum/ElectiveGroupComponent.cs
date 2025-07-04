using System;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Represents the link between an ElectiveGroup and a constituent Subject.
/// This is a many-to-many join entity.
/// </summary>
public class ElectiveGroupComponent : Entity
{
	#region Properties
	/// <summary>
	/// Foreign key for the ElectiveGroup.
	/// </summary>
	[ForeignKey(nameof(ElectiveGroup))]
	public virtual Guid ElectiveGroupId { get; private set; }

	/// <summary>
	/// Navigation property to the ElectiveGroup.
	/// </summary>
	public virtual ElectiveGroup ElectiveGroup { get; set; }

	/// <summary>
	/// Foreign key for the Subject.
	/// </summary>
	[ForeignKey(nameof(Subject))]
	public virtual Guid SubjectId { get; private set; }

	/// <summary>
	/// Navigation property to the Subject.
	/// </summary>
	public virtual Subject Subject { get; set; }
	#endregion

	#region Constructors
	private ElectiveGroupComponent() { }

	internal ElectiveGroupComponent(Guid ElectiveGroupId, Guid subjectId)
	{
		ElectiveGroupId = ElectiveGroupId;
		SubjectId = subjectId;
	}
	#endregion

	#region Methods
	public override object?[] GetKeys()
	{
		return new object?[] { ElectiveGroupId, SubjectId };
	}
	#endregion
}
