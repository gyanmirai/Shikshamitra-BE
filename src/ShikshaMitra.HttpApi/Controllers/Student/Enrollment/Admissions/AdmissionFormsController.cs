using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController, Authorize]
[Route("api/student/enrollment/admissions/forms")]
public class AdmissionFormsController(IAdmissionFormAppService service) : AbpController
{
	[HttpGet, Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)] public async Task<Result<PagedResultDto<AdmissionFormDto>>> GetListAsync([FromQuery] AdmissionFormListInput input) => Result.Success(await service.GetListAsync(input));
	[HttpGet("{id:guid}"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)] public async Task<Result<AdmissionFormDto>> GetAsync(Guid id) => Result.Success(await service.GetAsync(id));
	[HttpGet("{id:guid}/preview"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)] public async Task<Result<AdmissionFormDto>> PreviewAsync(Guid id) => Result.Success(await service.PreviewAsync(id));
	[HttpPost, Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Manage)] public async Task<Result<AdmissionFormDto>> CreateAsync(CreateUpdateAdmissionFormDto input) => Result.Success(await service.CreateAsync(input));
	[HttpPut("{id:guid}"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Manage)] public async Task<Result<AdmissionFormDto>> UpdateAsync(Guid id, CreateUpdateAdmissionFormDto input) => Result.Success(await service.UpdateAsync(id, input));
	[HttpPost("{id:guid}/clone"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Manage)] public async Task<Result<AdmissionFormDto>> CloneAsync(Guid id) => Result.Success(await service.CloneAsync(id));
	[HttpPost("{id:guid}/validate"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)] public async Task<Result<AdmissionFormValidationResultDto>> ValidateAsync(Guid id) => Result.Success(await service.ValidateAsync(id));
	[HttpPost("{id:guid}/publish"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Publish)] public async Task<Result<AdmissionFormDto>> PublishAsync(Guid id) => Result.Success(await service.PublishAsync(id));
}
