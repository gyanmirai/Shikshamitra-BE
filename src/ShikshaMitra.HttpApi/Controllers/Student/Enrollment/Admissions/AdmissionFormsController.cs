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
	[HttpGet("metadata/field-types"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)] public async Task<Result<IReadOnlyList<AdmissionFormFieldMetadataDto>>> GetFieldTypesAsync() => Result.Success(await service.GetFieldTypesAsync());
	[HttpGet("metadata/system-fields"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)] public async Task<Result<IReadOnlyList<AdmissionFormSystemFieldMetadataDto>>> GetSystemFieldsAsync() => Result.Success(await service.GetSystemFieldsAsync());
	[HttpGet("metadata/data-sources"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)] public async Task<Result<IReadOnlyList<AdmissionFormDataSourceMetadataDto>>> GetDataSourcesAsync() => Result.Success(await service.GetDataSourcesAsync());
	[HttpGet("metadata/options"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)] public async Task<Result<IReadOnlyList<AdmissionFormOptionDto>>> GetOptionsAsync([FromQuery] string sourceKey, [FromQuery] Guid? parentId = null, [FromQuery] Guid? academicYearId = null) => Result.Success(await service.GetOptionsAsync(sourceKey, parentId, academicYearId));
	[HttpPost, Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Manage)] public async Task<Result<AdmissionFormDto>> CreateAsync(CreateUpdateAdmissionFormDto input) => Result.Success(await service.CreateAsync(input));
	[HttpPut("{id:guid}"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Manage)] public async Task<Result<AdmissionFormDto>> UpdateAsync(Guid id, CreateUpdateAdmissionFormDto input) => Result.Success(await service.UpdateAsync(id, input));
	[HttpPost("{id:guid}/clone"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Manage)] public async Task<Result<AdmissionFormDto>> CloneAsync(Guid id, CloneAdmissionFormDto input) => Result.Success(await service.CloneAsync(id, input));
	[HttpPost("{id:guid}/versions"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.CreateVersion)] public async Task<Result<AdmissionFormDto>> CreateNewVersionAsync(Guid id) => Result.Success(await service.CreateNewVersionAsync(id));
	[HttpDelete("{id:guid}"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Manage)] public async Task<Result> DeleteAsync(Guid id) { await service.DeleteAsync(id); return Result.Success(); }
	[HttpPost("{id:guid}/archive"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Archive)] public async Task<Result<AdmissionFormDto>> ArchiveAsync(Guid id, [FromQuery] string? concurrencyStamp = null) => Result.Success(await service.ArchiveAsync(id, concurrencyStamp));
	[HttpPost("{id:guid}/validate"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)] public async Task<Result<AdmissionFormValidationResultDto>> ValidateAsync(Guid id) => Result.Success(await service.ValidateAsync(id));
	[HttpPost("{id:guid}/publish"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Publish)] public async Task<Result<AdmissionFormDto>> PublishAsync(Guid id, [FromQuery] string? concurrencyStamp = null) => Result.Success(await service.PublishAsync(id, concurrencyStamp));
	[HttpGet("{id:guid}/usage"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.ViewUsage)] public async Task<Result<AdmissionFormUsageDto>> GetUsageAsync(Guid id) => Result.Success(await service.GetUsageAsync(id));
}
