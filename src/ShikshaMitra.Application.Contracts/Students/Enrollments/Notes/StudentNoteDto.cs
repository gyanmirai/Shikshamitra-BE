using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Enrollments.Notes;

public class StudentNoteDto : CreationAuditedEntityDto<Guid>
{
    public Guid StudentId { get; set; }
    public string Note { get; set; } = null!; // The response mapper populates these members after construction.
    public DateTimeOffset NextReminderDate { get; set; }
    public NoteStatusEnum Status { get; set; }
    public new Guid? CreatorId { get; set; }
    public string? CreatorName { get; set; }
}
