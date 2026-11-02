using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Academic.Syllabus;

/// <summary>
/// Immutable audit log entry created every time chapter progress is updated for a division.
/// </summary>
public class ChapterProgressLog : CreationAuditedEntity<Guid>
{
    /// <summary>The chapter whose progress was changed.</summary>
    public Guid ChapterId { get; private set; }

    /// <summary>The division for which the change was recorded.</summary>
    public Guid DivisionId { get; private set; }

    /// <summary>Progress value (0-100) at the time of the change.</summary>
    public int Progress { get; private set; }

    /// <summary>Denormalized display name of the user who made the change.</summary>
    public string ChangedByName { get; private set; }

    private ChapterProgressLog()
    {
        // EF materializes these required columns after this ORM constructor.
        ChangedByName = null!;
    } // Required for EF Core

    public ChapterProgressLog(Guid id, Guid chapterId, Guid divisionId, int progress, string changedByName)
        : base(id)
    {
        ChapterId = chapterId;
        DivisionId = divisionId;
        Progress = progress;
        ChangedByName = changedByName;
    }
}
