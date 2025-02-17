using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentRecords;

public class Student : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public string Name { get; set; }

	public Guid AcademicYearId { get; set; }
	public virtual AcademicYear AcademicYear { get; set; } // Navigation property

	public Guid EducationBoardId { get; set; }
	public virtual EducationBoard EducationBoard { get; set; } // Navigation property

	public Guid EducationMediumId { get; set; }
	public virtual EducationMedium EducationMedium { get; set; } // Navigation property

	public Guid EducationMediumStageId { get; set; }
	public virtual EducationMediumStage EducationMediumStage { get; set; } // Navigation property

	public Guid StandardId { get; set; }
	public virtual Standard Standard { get; set; } // Navigation property

	public Guid DivisionId { get; set; }
	public virtual Division Division { get; set; } // Navigation property
	public int GrNoRecord { get; set; }
	public int RollNo { get; set; }

	public Guid? TenantId { get; set; }

	protected Student() { }

	public Student(Guid id, Guid tenantId, string name, Guid academicYearId, Guid educationBoardId, Guid educationMediumId, Guid educationMediumStageId, Guid standardId, Guid divisionId, int grNoRecord, int rollNo) : base(id)
	{
		Name = name;
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		EducationBoardId = educationBoardId;
		EducationMediumId = educationMediumId;
		EducationMediumStageId = educationMediumStageId;
		StandardId = standardId;
		DivisionId = divisionId;
		GrNoRecord = grNoRecord;
		RollNo = rollNo;
	}
}
