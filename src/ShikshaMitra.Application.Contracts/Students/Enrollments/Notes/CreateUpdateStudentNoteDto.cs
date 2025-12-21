using System;

namespace ShikshaMitra.Students.Enrollments.Notes;

public class CreateUpdateStudentNoteDto
{
    public required string Note { get; set; }
    public DateTimeOffset NextReminderDate { get; set; }
    public NoteStatusEnum Status { get; set; } = NoteStatusEnum.Pending;
}
