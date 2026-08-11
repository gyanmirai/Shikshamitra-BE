using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records.Notes;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.Records;

[ApiController]
[Authorize]
[Route("api/students/records")]
public class StudentNotesController(IStudentNotesAppService notesAppService) : AbpController
{
	[HttpGet("{studentId:guid}/notes")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
	public Task<List<StudentNoteDto>> GetAllAsync(Guid studentId) =>
		notesAppService.GetAllAsync(studentId);

	[HttpPost("{studentId:guid}/notes")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add)]
	public async Task<IActionResult> CreateAsync(Guid studentId, [FromBody] CreateUpdateStudentNoteDto input)
	{
		await notesAppService.CreateAsync(studentId, input);
		return NoContent();
	}

	[HttpPut("notes/{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add)]
	public Task UpdateAsync(Guid id, [FromBody] CreateUpdateStudentNoteDto input) =>
		notesAppService.UpdateAsync(id, input);

	[HttpDelete("notes/{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Delete)]
	public Task DeleteAsync(Guid id) => notesAppService.DeleteAsync(id);
}
