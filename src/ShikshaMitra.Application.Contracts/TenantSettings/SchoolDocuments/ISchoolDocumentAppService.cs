using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.SchoolDocuments;

public interface ISchoolDocumentAppService : IApplicationService
{
	Task<PagedResultDto<SchoolDocumentDto>> GetListAsync(SchoolDocumentListInput input);
	Task<SchoolDocumentDto> GetAsync(Guid id);
	Task<SchoolDocumentDto> CreateAsync(SchoolDocumentWriteDto input);
	Task<SchoolDocumentDto> UpdateAsync(Guid id, SchoolDocumentWriteDto input);
	Task DeleteAsync(Guid id);
	Task<SchoolDocumentDownloadDto> GetFileAsync(Guid id, Guid fileId);
}
