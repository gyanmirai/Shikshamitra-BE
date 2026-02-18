using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Setting.Sections.Dtos;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Setting.Sections;

public interface ILibrarySectionAppService : IApplicationService
{
	Task<PaginatedSectionResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10);
	Task<GenericCreatedResponse> CreateAsync(Guid academicYearId, SectionCreateRequest input);
	Task<SectionDetailsResponse> GetAsync(Guid sectionId, Guid academicYearId);
	Task<GenericUpdatedResponse> UpdateAsync(Guid sectionId, Guid academicYearId, SectionUpdateRequest input);
	Task DeleteAsync(Guid sectionId, Guid academicYearId);
}
