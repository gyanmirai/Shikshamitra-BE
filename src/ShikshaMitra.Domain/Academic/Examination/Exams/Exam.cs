using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.Exams;

public class Exam : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid ExamTypeId { get; private set; }
    public string ExamName { get; private set; } = null!;
    public Guid StandardId { get; private set; }
    
    public ICollection<ExamDivision> ExamDivisions { get; private set; }
    public ICollection<ExamSubject> ExamSubjects { get; private set; }

    private Exam()
    {
        ExamDivisions = new Collection<ExamDivision>();
        ExamSubjects = new Collection<ExamSubject>();
    }

    public Exam(
        Guid id,
        Guid examTypeId,
        string examName,
        Guid standardId,
        Guid? tenantId = null) : base(id)
    {
        ExamTypeId = examTypeId;
        ExamName = Check.NotNullOrWhiteSpace(examName, nameof(examName));
        StandardId = standardId;
        TenantId = tenantId;

        ExamDivisions = new Collection<ExamDivision>();
        ExamSubjects = new Collection<ExamSubject>();
    }

    public void AddDivision(Guid id, Guid divisionId)
    {
        if (ExamDivisions.Any(d => d.DivisionId == divisionId))
        {
            return;
        }

        ExamDivisions.Add(new ExamDivision(id, Id, divisionId));
    }

    public void RemoveDivision(Guid divisionId)
    {
        var division = ExamDivisions.FirstOrDefault(d => d.DivisionId == divisionId);
        if (division != null)
        {
            ExamDivisions.Remove(division);
        }
    }

    public void AddSubject(ExamSubject subject)
    {
        subject.SetExamId(Id);
        ExamSubjects.Add(subject);
    }
    
    public void UpdateExamDetails(string examName, Guid examTypeId, Guid standardId) {
        ExamName = Check.NotNullOrWhiteSpace(examName, nameof(examName));
        ExamTypeId = examTypeId;
        StandardId = standardId;
    }
}
