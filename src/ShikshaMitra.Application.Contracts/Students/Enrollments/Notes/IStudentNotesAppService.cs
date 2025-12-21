using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Enrollments.Notes;

public interface IStudentNotesAppService
{
    Task<List<StudentNoteDto>> GetAllAsync(Guid studentId);
    Task CreateAsync(Guid studentId, CreateUpdateStudentNoteDto studentNote);
    Task UpdateAsync(Guid id, CreateUpdateStudentNoteDto studentNote);
    Task DeleteAsync(Guid id);
}
