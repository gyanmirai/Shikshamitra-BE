using System;
using System.Collections.Generic;
using ShikshaMitra.Onboarding.EducationBoards;
using ShikshaMitra.Onboarding.Standards;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Onboarding.EducationalMediums;

public class EducationalMedium : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; set; }
    public ICollection<Standard> Standards { get; set; }

    // Foreign key
    public Guid EducationBoardId { get; set; }

    // Navigation property
    public EducationBoard EducationBoard { get; set; }


    internal EducationalMedium AddStandard(Standard standard)
    {
        Standards.Add(standard);
        return this;
    }
}