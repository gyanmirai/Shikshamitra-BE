using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Enrollments.Notes;

public class StudentNoteDto : EntityDto<Guid>
{
    public Guid StudentId { get; set; }
    public string Note { get; set; }
    public DateTimeOffset NextReminderDate { get; set; }
    public NoteStatusEnum Status { get; set; }
    public Guid? CreatorId { get; set; }
    public string CreatorName { get; set; }
}
