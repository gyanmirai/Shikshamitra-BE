using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

public class ElectiveSubject : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	#region Properties
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; }
	public string Color { get; private set; }
	public Guid EducationMediumId { get; private set; }
	public virtual ICollection<ElectiveSubjectComponent> Subjects { get; set; }
	#endregion

	#region Constructors
	private ElectiveSubject()
	{
		/* This constructor is for deserialization / ORM purpose */
		Subjects = new HashSet<ElectiveSubjectComponent>();
	}

	public ElectiveSubject(
		Guid id,
		Guid? tenantId,
		string name,
		string color,
		Guid educationMediumId
	)
		: base(id)
	{
		TenantId = tenantId;
		SetName(name);
		SetColor(color);
		SetEducationMedium(educationMediumId);
		Subjects = new HashSet<ElectiveSubjectComponent>();
	}
	#endregion

	#region Methods
	public void SetName(string name)
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));
		Name = name;
	}

	public void SetColor(string color)
	{
		Check.NotNullOrWhiteSpace(color, nameof(color));
		Color = color;
	}

	public void SetEducationMedium(Guid educationMediumId)
	{
		EducationMediumId = educationMediumId;
	}

	public void AddSubject(Guid subjectId)
	{
		if (IsInSubjects(subjectId))
		{
			return;
		}
		Subjects.Add(new ElectiveSubjectComponent(Id, subjectId));
	}

	public void RemoveSubject(Guid subjectId)
	{
		var subjectToRemove = Subjects.FirstOrDefault(s => s.SubjectId == subjectId);
		if (subjectToRemove != null)
		{
			Subjects.Remove(subjectToRemove);
		}
	}

	public void ReplaceSubjects(IEnumerable<Guid> subjectIds)
	{
		Subjects.Clear();
		if (subjectIds == null)
		{
			return;
		}

		foreach (var subjectId in subjectIds.Distinct())
		{
			AddSubject(subjectId);
		}
	}

	private bool IsInSubjects(Guid subjectId) => Subjects.Any(s => s.SubjectId == subjectId);
	#endregion
}
