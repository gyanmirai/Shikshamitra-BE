using System;
using System.Collections.Generic;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.Onboarding.Divisions;
using ShikshaMitra.Onboarding.EducationBoards;
using ShikshaMitra.Onboarding.EducationMediums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Onboarding.Standards;

public class Standard : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid EducationBoardId { get; set; }

	public Guid EducationMediumStageId { get; set; }
	public Guid EducationMediumId { get; set; }

	public string Name { get; set; }

	// Navigation properties
	public AcademicYear AcademicYear { get; set; }
	public EducationBoard EducationBoard { get; set; }
	public EducationMediumStage EducationMediumStage { get; set; }
	public EducationMedium EducationMedium { get; set; }
	public ICollection<Division> Divisions { get; set; }
}