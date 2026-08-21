using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
namespace ShikshaMitra.Students.StudentEnrollments.Admissions;
public interface IAdmissionFormAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionFormDto>> GetListAsync(AdmissionFormListInput input);
	Task<AdmissionFormDto> GetAsync(Guid id);
	Task<AdmissionFormDto> PreviewAsync(Guid id);
	Task<AdmissionFormDto> CreateAsync(CreateUpdateAdmissionFormDto input);
	Task<AdmissionFormDto> UpdateAsync(Guid id, CreateUpdateAdmissionFormDto input);
	Task<AdmissionFormDto> CloneAsync(Guid id, CloneAdmissionFormDto input);
	Task<AdmissionFormDto> CreateNewVersionAsync(Guid id);
	Task DeleteAsync(Guid id);
	Task<AdmissionFormDto> ArchiveAsync(Guid id);
	Task<AdmissionFormValidationResultDto> ValidateAsync(Guid id);
	Task<AdmissionFormDto> PublishAsync(Guid id, string? concurrencyStamp = null);
	Task<AdmissionFormUsageDto> GetUsageAsync(Guid id);
	Task<IReadOnlyList<AdmissionFormFieldMetadataDto>> GetFieldTypesAsync();
	Task<IReadOnlyList<AdmissionFormSystemFieldMetadataDto>> GetSystemFieldsAsync();
	Task<IReadOnlyList<AdmissionFormDataSourceMetadataDto>> GetDataSourcesAsync();
	Task<IReadOnlyList<AdmissionFormOptionDto>> GetOptionsAsync(string sourceKey, Guid? parentId = null, Guid? academicYearId = null);
}
