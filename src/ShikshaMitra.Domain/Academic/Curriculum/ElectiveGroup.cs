using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

public class ElectiveGroup : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	#region Properties
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; }
	public string Color { get; private set; }
	public virtual ICollection<ElectiveGroupComponent> Subjects { get; set; }
	public Guid EducationMediumId { get; set; }
	public EducationMedium EducationMedium { get; set; }
	#endregion

	#region Constructors
	private ElectiveGroup()
	{
		/* This constructor is for deserialization / ORM purpose */
		Subjects = new HashSet<ElectiveGroupComponent>();
	}

	public ElectiveGroup(Guid id, Guid? tenantId, string name, string color, Guid educationMediumId)
		: base(id)
	{
		TenantId = tenantId;
		SetName(name);
		SetColor(color);
		EducationMediumId = educationMediumId;
		Subjects = new HashSet<ElectiveGroupComponent>();
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

	public void AddSubject(Guid subjectId)
	{
		if (IsInSubjects(subjectId))
		{
			return;
		}
		Subjects.Add(new ElectiveGroupComponent(Id, subjectId));
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
