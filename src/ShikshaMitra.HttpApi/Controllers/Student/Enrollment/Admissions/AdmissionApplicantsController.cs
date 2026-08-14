using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController, Authorize]
[Route("api/student/enrollment/admissions/applicants")]
public class AdmissionApplicantsController(IAdmissionApplicantAppService service) : AbpController
{
	[HttpGet("{id:guid}"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplicants.View)] public async Task<Result<AdmissionApplicant360Dto>> GetAsync(Guid id) => Result.Success(await service.GetAsync(id));
	[HttpGet("{id:guid}/activities"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplicants.View)] public async Task<Result<List<AdmissionActivityDto>>> GetActivitiesAsync(Guid id) => Result.Success(await service.GetActivitiesAsync(id));
	[HttpGet("{id:guid}/notes"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplicants.NotesView)] public async Task<Result<List<AdmissionNoteDto>>> GetNotesAsync(Guid id) => Result.Success(await service.GetNotesAsync(id));
	[HttpPost("{id:guid}/notes"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplicants.NotesCreate)] public async Task<Result<AdmissionNoteDto>> CreateNoteAsync(Guid id, CreateAdmissionNoteDto input) => Result.Success(await service.CreateNoteAsync(id, input));
	[HttpPut("{id:guid}/notes/{noteId:guid}"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplicants.NotesEdit)] public async Task<Result<AdmissionNoteDto>> UpdateNoteAsync(Guid id, Guid noteId, UpdateAdmissionNoteDto input) => Result.Success(await service.UpdateNoteAsync(id, noteId, input));
}
