using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

public class StandardSubjectAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	#region Properties
	public Guid? TenantId { get; private set; }

	[ForeignKey(nameof(Standard))]
	public Guid StandardId { get; set; }
	public virtual Standard Standard { get; set; }

	[ForeignKey(nameof(Subject))]
	public Guid SubjectId { get; set; }
	public virtual Subject Subject { get; set; }

	public string SubjectCode { get; set; }
	public ICollection<SubjectTeacher> Teachers { get; set; } = new HashSet<SubjectTeacher>();
	public string? LearningGoals { get; set; }
	public string? Prerequisites { get; set; }
	public string? Description { get; set; }
	public string? UploadedDocument { get; set; }
	public SubjectStatus Status { get; set; } = SubjectStatus.Draft;
	#endregion

	#region Constructors
	private StandardSubjectAssignment()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public StandardSubjectAssignment(
	   Guid id,
	   Guid? tenantId,
	   Guid standardId,
	   Guid subjectId,
	   string subjectCode,
	   SubjectStatus status = SubjectStatus.Draft,
	   string? learningGoals = null,
	   string? prerequisites = null,
	   string? description = null,
	   string? uploadedDocument = null
	) : base(id)
	{
		TenantId = tenantId;
		StandardId = standardId;
		SubjectId = subjectId;
		SubjectCode = Check.NotNullOrWhiteSpace(subjectCode, nameof(subjectCode));
		Status = status;

		LearningGoals = learningGoals;
		Prerequisites = prerequisites;
		Description = description;
		UploadedDocument = uploadedDocument;

		Teachers = new HashSet<SubjectTeacher>();
	}

	#endregion

	#region Methods
	public void ChangeStatus(SubjectStatus status)
	{
		Status = status;
	}

	public void SetPrimaryTeacher(Guid teacherId)
	{
		//Teachers.RemoveWhere(t => t.IsPrimary);
		//Teachers.Add(new SubjectTeacher(Id, teacherId, true));
	}

	public void SetSecondaryTeachers(IEnumerable<Guid> teacherIds)
	{
		//var currentSecondary = Teachers.Where(t => !t.IsPrimary).ToList();
		//foreach (var t in currentSecondary)
		//	Teachers.Remove(t);

		//foreach (var id in teacherIds)
		//	Teachers.Add(new SubjectTeacher(Id, id, false));
	}
	#endregion
}
