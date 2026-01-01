using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.Settings;

public class GradingScale : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; }
    public string TotalMarks { get; private set; }
    public Guid AcademicMediumId { get; private set; }
    public ICollection<GradeRange> GradeRanges { get; private set; }

    private GradingScale()
    {
        /* This constructor is for deserialization / ORM purpose */
        GradeRanges = new List<GradeRange>();
    }

    public GradingScale(Guid id, Guid? tenantId, string name, string totalMarks, Guid academicMediumId)
        : base(id)
    {
        TenantId = tenantId;
        SetName(name);
        SetTotalMarks(totalMarks);
        AcademicMediumId = academicMediumId;
        GradeRanges = new List<GradeRange>();
    }

    public void SetName(string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Name = name;
    }

    public void SetTotalMarks(string totalMarks)
    {
        Check.NotNullOrWhiteSpace(totalMarks, nameof(totalMarks));
        TotalMarks = totalMarks;
    }

    public void AddGradeRange(Guid id, int minMarks, int maxMarks, string grade)
    {
        GradeRanges.Add(new GradeRange(id, Id, minMarks, maxMarks, grade));
    }

    public void ClearGradeRanges()
    {
        GradeRanges.Clear();
    }

    public void UpdateGradeRanges(List<(Guid id, int minMarks, int maxMarks, string grade)> gradeRanges)
    {
        ClearGradeRanges();
        foreach (var (id, minMarks, maxMarks, grade) in gradeRanges)
        {
            AddGradeRange(id, minMarks, maxMarks, grade);
        }
    }
}
