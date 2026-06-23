using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.Exams;

public class Exam : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid ExamTypeId { get; private set; }
    public string ExamName { get; private set; } = null!;
    public Guid StandardId { get; private set; }
    public Guid? GradingScaleId { get; private set; }
    public ExamStatus Status { get; private set; }

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
        Guid? tenantId = null,
        Guid? gradingScaleId = null) : base(id)
    {
        ExamTypeId = examTypeId;
        ExamName = Check.NotNullOrWhiteSpace(examName, nameof(examName));
        StandardId = standardId;
        TenantId = tenantId;
        GradingScaleId = gradingScaleId;
        Status = ExamStatus.Pending;

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

    public void UpdateDivisions(List<Guid> divisionIds)
    {
        var existingDivisionIds = ExamDivisions.Select(x => x.DivisionId).ToList();
        var divisionsToRemove = ExamDivisions.Where(x => !divisionIds.Contains(x.DivisionId)).ToList();
        var divisionsToAdd = divisionIds.Where(x => !existingDivisionIds.Contains(x)).ToList();

        foreach (var division in divisionsToRemove)
        {
            ExamDivisions.Remove(division);
        }

        foreach (var divisionId in divisionsToAdd)
        {
            ExamDivisions.Add(new ExamDivision(Guid.NewGuid(), Id, divisionId));
        }
    }

    public void AddSubject(ExamSubject subject)
    {
        subject.SetExamId(Id);
        ExamSubjects.Add(subject);
    }

    public void UpdateSubjects(List<Guid> subjectIds)
    {
        var existingSubjectIds = ExamSubjects.Select(x => x.SubjectId).ToList();
        var subjectsToRemove = ExamSubjects.Where(x => !subjectIds.Contains(x.SubjectId)).ToList();
        var subjectsToAdd = subjectIds.Where(x => !existingSubjectIds.Contains(x)).ToList();

        foreach (var subject in subjectsToRemove)
        {
            ExamSubjects.Remove(subject);
        }

        foreach (var subjectId in subjectsToAdd)
        {
            ExamSubjects.Add(new ExamSubject(Guid.NewGuid(), Id, subjectId));
        }
    }

    public void UpdateExamDetails(string examName, Guid examTypeId, Guid standardId, Guid? gradingScaleId)
    {
        ExamName = Check.NotNullOrWhiteSpace(examName, nameof(examName));
        ExamTypeId = examTypeId;
        StandardId = standardId;
        GradingScaleId = gradingScaleId;
    }

    public void ChangeStatus(ExamStatus newStatus)
    {
        Status = newStatus;
    }
}
