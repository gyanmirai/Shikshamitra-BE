using System;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Represents the link between an ElectiveSubject and a constituent Subject.
/// This is a many-to-many join entity.
/// </summary>
public class ElectiveSubjectComponent : Entity
{
	/// <summary>
	/// Foreign key for the ElectiveSubject.
	/// </summary>
	[ForeignKey(nameof(ElectiveSubject))]
	public virtual Guid ElectiveSubjectId { get; private set; }

	/// <summary>
	/// Navigation property to the ElectiveSubject.
	/// </summary>
	public virtual ElectiveSubject ElectiveSubject { get; set; }

	/// <summary>
	/// Foreign key for the Subject.
	/// </summary>
	[ForeignKey(nameof(Subject))]
	public virtual Guid SubjectId { get; private set; }

	/// <summary>
	/// Navigation property to the Subject.
	/// </summary>
	public virtual Subject Subject { get; set; }

	private ElectiveSubjectComponent() { }

	internal ElectiveSubjectComponent(Guid electiveSubjectId, Guid subjectId)
	{
		ElectiveSubjectId = electiveSubjectId;
		SubjectId = subjectId;
	}

	public override object?[] GetKeys()
	{
		return new object?[] { ElectiveSubjectId, SubjectId };
	}
}
