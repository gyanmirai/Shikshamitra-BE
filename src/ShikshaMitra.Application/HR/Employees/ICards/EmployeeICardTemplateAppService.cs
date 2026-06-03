using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.ICards;

[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_ICard.View)]
[RemoteService(false)]
public class EmployeeICardTemplateAppService(
	EmployeeICardTemplateReadService readService,
	EmployeeICardTemplateMutationService mutationService,
	EmployeeICardAssetService assetService,
	EmployeeICardPdfWorkflow pdfWorkflow) : ApplicationService, IEmployeeICardTemplateAppService
{
	public Task<EmployeeICardTemplateListResponse> GetListAsync(
		Guid? academicYearId = null,
		Guid? mediumStageId = null,
		int page = 1,
		int pageSize = 20,
		string? search = null) =>
		readService.GetListAsync(academicYearId, mediumStageId, page, pageSize, search);

	public Task<EmployeeICardTemplateDto> GetAsync(Guid id) =>
		readService.GetAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_ICard.Add)]
	public Task<EmployeeICardTemplateDto> CreateAsync(EmployeeICardTemplateRequest input) =>
		mutationService.CreateAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_ICard.Add)]
	public Task<EmployeeICardTemplateDto> UpdateAsync(Guid id, EmployeeICardTemplateRequest input) =>
		mutationService.UpdateAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_ICard.Add)]
	public Task<EmployeeICardAssetUploadResult> UploadAssetAsync(string fileName, string contentType, byte[] content) =>
		assetService.UploadAssetAsync(fileName, contentType, content);

	public Task<EmployeeICardAssetDownloadResult> DownloadAssetAsync(string blobName) =>
		assetService.DownloadAssetAsync(blobName);

	public Task<byte[]> GenerateEmployeeICardsPdfAsync(Guid templateId, EmployeeICardPdfRequest input) =>
		pdfWorkflow.GenerateEmployeeICardsPdfAsync(templateId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_ICard.Delete)]
	public Task DeleteAsync(Guid id) =>
		mutationService.DeleteAsync(id);
}
