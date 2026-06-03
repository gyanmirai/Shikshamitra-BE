using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.ICards;

public interface IEmployeeICardTemplateAppService : IApplicationService
{
	Task<EmployeeICardTemplateListResponse> GetListAsync(
		Guid? academicYearId = null,
		Guid? mediumStageId = null,
		int page = 1,
		int pageSize = 20,
		string? search = null);

	Task<EmployeeICardTemplateDto> GetAsync(Guid id);
	Task<EmployeeICardTemplateDto> CreateAsync(EmployeeICardTemplateRequest input);
	Task<EmployeeICardTemplateDto> UpdateAsync(Guid id, EmployeeICardTemplateRequest input);
	Task<EmployeeICardAssetUploadResult> UploadAssetAsync(string fileName, string contentType, byte[] content);
	Task<EmployeeICardAssetDownloadResult> DownloadAssetAsync(string blobName);
	Task<byte[]> GenerateEmployeeICardsPdfAsync(Guid templateId, EmployeeICardPdfRequest input);
	Task DeleteAsync(Guid id);
}
