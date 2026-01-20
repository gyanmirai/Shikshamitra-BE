using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.Quizzes;

public class Quiz : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public string Title { get; private set; } = null!;
    public Guid ClassId { get; private set; }
    public Guid SubjectId { get; private set; }
    public int Duration { get; private set; }
    public int TotalMarks { get; private set; }
    public int PassingMarks { get; private set; }
    public DateTime StartDateTime { get; private set; }
    public DateTime EndDateTime { get; private set; }
    public QuizTakenType TakenType { get; private set; }
    public QuizStatus Status { get; private set; }
    public bool IsRandomOrder { get; private set; }

    public ICollection<QuizDivision> Divisions { get; private set; }
    public ICollection<QuizChapter> Chapters { get; private set; }
    public ICollection<QuizQuestion> Questions { get; private set; }

    private Quiz()
    {
        Divisions = new Collection<QuizDivision>();
        Chapters = new Collection<QuizChapter>();
        Questions = new Collection<QuizQuestion>();
    }

    public Quiz(
        Guid id,
        string title,
        Guid classId,
        Guid subjectId,
        int duration,
        int totalMarks,
        int passingMarks,
        DateTime startDateTime,
        DateTime endDateTime,
        QuizTakenType takenType,
        Guid? tenantId = null) : base(id)
    {
        Title = Check.NotNullOrWhiteSpace(title, nameof(title));
        ClassId = classId;
        SubjectId = subjectId;
        Duration = duration;
        TotalMarks = totalMarks;
        PassingMarks = passingMarks;
        StartDateTime = startDateTime;
        EndDateTime = endDateTime;
        TakenType = takenType;
        TenantId = tenantId;
        Status = QuizStatus.Pending;
        IsRandomOrder = false;

        Divisions = new Collection<QuizDivision>();
        Chapters = new Collection<QuizChapter>();
        Questions = new Collection<QuizQuestion>();
    }

    public void UpdateDetails(
        string title,
        Guid classId,
        Guid subjectId,
        int duration,
        int totalMarks,
        int passingMarks,
        DateTime startDateTime,
        DateTime endDateTime,
        QuizTakenType takenType)
    {
        Title = Check.NotNullOrWhiteSpace(title, nameof(title));
        ClassId = classId;
        SubjectId = subjectId;
        Duration = duration;
        TotalMarks = totalMarks;
        PassingMarks = passingMarks;
        StartDateTime = startDateTime;
        EndDateTime = endDateTime;
        TakenType = takenType;
    }

    public void UpdateDivisions(List<Guid> divisionIds)
    {
        var existingDivisionIds = Divisions.Select(x => x.DivisionId).ToList();
        var divisionsToRemove = Divisions.Where(x => !divisionIds.Contains(x.DivisionId)).ToList();
        var divisionsToAdd = divisionIds.Where(x => !existingDivisionIds.Contains(x)).ToList();

        foreach (var division in divisionsToRemove)
        {
            Divisions.Remove(division);
        }

        foreach (var divisionId in divisionsToAdd)
        {
            Divisions.Add(new QuizDivision(Guid.NewGuid(), Id, divisionId));
        }
    }

    public void UpdateChapters(List<Guid> chapterIds)
    {
        var existingChapterIds = Chapters.Select(x => x.ChapterId).ToList();
        var chaptersToRemove = Chapters.Where(x => !chapterIds.Contains(x.ChapterId)).ToList();
        var chaptersToAdd = chapterIds.Where(x => !existingChapterIds.Contains(x)).ToList();

        foreach (var chapter in chaptersToRemove)
        {
            Chapters.Remove(chapter);
        }

        foreach (var chapterId in chaptersToAdd)
        {
            Chapters.Add(new QuizChapter(Guid.NewGuid(), Id, chapterId));
        }
    }

    public void SetRandomOrder(bool isRandomOrder)
    {
        IsRandomOrder = isRandomOrder;
    }

    public void AddQuestion(QuizQuestion question)
    {
        Questions.Add(question);
    }
    
    public void RemoveQuestion(Guid questionId)
    {
        var question = Questions.FirstOrDefault(q => q.Id == questionId);
        if (question != null)
        {
            Questions.Remove(question);
        }
    }

    // Since questions are complex, we might want to clear and re-add or handle externally.
    // For now, exposing Add/Remove is sufficient, and the AppService can handle list logic.
}
