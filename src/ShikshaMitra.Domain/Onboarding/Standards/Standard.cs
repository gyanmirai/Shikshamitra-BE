using System;
using System.Collections.Generic;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.Onboarding.Divisions;
using ShikshaMitra.Onboarding.EducationalMediums;
using ShikshaMitra.Onboarding.EducationBoards;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Onboarding.Standards;

public class Standard : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid EducationBoardId { get; set; }
    public Guid EducationBoardStageId { get; set; }
    public Guid EducationalMediumId { get; set; }

    public string Name { get; set; }

    // Navigation properties
    public AcademicYear AcademicYear { get; set; }
    public EducationBoard EducationBoard { get; set; }
    public EducationBoardStage EducationBoardStage { get; set; }
    public EducationalMedium EducationalMedium { get; set; }
    public ICollection<Division> Divisions { get; set; }

}