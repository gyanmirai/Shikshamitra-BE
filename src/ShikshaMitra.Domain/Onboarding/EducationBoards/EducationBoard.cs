using System;
using System.Collections.Generic;
using ShikshaMitra.Onboarding.EducationalMediums;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Onboarding.EducationBoards;

public class EducationBoard : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; set; }
    public ICollection<EducationalMedium> EducationalMediums { get; set; }

    internal EducationBoard AddEducationalMedium(EducationalMedium educationalMedium)
    {
        EducationalMediums.Add(educationalMedium);
        return this;
    }
}