using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.Enrollments.Notes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.Enrollments;

[ApiController]
[Authorize]
[Route("api/student/enrollment")]
public class NotesController(IStudentNotesAppService notesAppService) : AbpController
{
    [HttpGet("{studentId}/notes")]
    public async Task<List<StudentNoteDto>> GetAllAsync(Guid studentId)
        => await notesAppService.GetAllAsync(studentId);

    [HttpPost("{studentId}/notes")]
    public async Task<IActionResult> CreateAsync(Guid studentId, [FromBody] CreateUpdateStudentNoteDto input)
    {
        await notesAppService.CreateAsync(studentId, input);
        return NoContent();
    }

    [HttpPut("notes/{id}")]
    public async Task UpdateAsync(Guid id, [FromBody] CreateUpdateStudentNoteDto input)
        => await notesAppService.UpdateAsync(id, input);

    [HttpDelete("notes/{id}")]
    public async Task DeleteAsync(Guid id)
        => await notesAppService.DeleteAsync(id);
}
