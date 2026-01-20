using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.Academic.Examination.Quizzes;

namespace ShikshaMitra.Academic.Examination.Submissions;

public class QuizSubmission : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid QuizId { get; private set; }
    public Guid StudentId { get; private set; }
    public int TotalMarksObtained { get; private set; }
    public StudentResultStatus Status { get; private set; }

    public ICollection<QuizSubmissionAnswer> Answers { get; private set; }

    private QuizSubmission()
    {
        Answers = new Collection<QuizSubmissionAnswer>();
    }

    public QuizSubmission(
        Guid id,
        Guid quizId,
        Guid studentId,
        Guid? tenantId = null) : base(id)
    {
        QuizId = quizId;
        StudentId = studentId;
        TenantId = tenantId;
        Status = StudentResultStatus.Pending;
        TotalMarksObtained = 0;
        
        Answers = new Collection<QuizSubmissionAnswer>();
    }

    public void AddAnswer(QuizSubmissionAnswer answer)
    {
        Answers.Add(answer);
    }
    
    public void UpdateMarks(int totalMarks, StudentResultStatus status)
    {
        TotalMarksObtained = totalMarks;
        Status = status;
    }
}
