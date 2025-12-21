using ShikshaMitra.Students.Enrollments.Notes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments;

[RemoteService(false)]
public class StudentNotesAppService(
		IRepository<StudentNote, Guid> noteRepository,
		IRepository<Student, Guid> studentRepository
) : ApplicationService, IStudentNotesAppService
{
    public async Task<List<StudentNoteDto>> GetAllAsync(Guid studentId)
    {
        // Validate student exists
        var student = await studentRepository.FindAsync(studentId) ?? throw new UserFriendlyException($"Student not found: {studentId}");
		var notes = await noteRepository.GetListAsync(x => x.StudentId == studentId);
        return ObjectMapper.Map<List<StudentNote>, List<StudentNoteDto>>(notes.OrderByDescending(x => x.CreationTime).ToList());
    }

    public async Task CreateAsync(Guid studentId, CreateUpdateStudentNoteDto input)
    {
        // Validate student exists
        var student = await studentRepository.FindAsync(studentId) ?? throw new UserFriendlyException($"Student not found: {studentId}");
		var note = new StudentNote(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            studentId,
            input.Note,
            input.NextReminderDate,
            input.Status
        );
        await noteRepository.InsertAsync(note, autoSave: true);
    }

    public async Task UpdateAsync(Guid id, CreateUpdateStudentNoteDto input)
    {
        var note = await noteRepository.GetAsync(id);
        // Only allow update if note exists
        note.Update(input.Note, input.NextReminderDate, input.Status);
        await noteRepository.UpdateAsync(note, autoSave: true);
    }

    public async Task DeleteAsync(Guid id)
    {
        await noteRepository.DeleteAsync(id);
    }
}
