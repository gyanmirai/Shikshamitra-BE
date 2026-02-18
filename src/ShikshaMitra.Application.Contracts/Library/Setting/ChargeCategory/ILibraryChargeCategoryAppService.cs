using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Setting.ChargeCategory.Dtos;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Setting.ChargeCategory;

public interface ILibraryChargeCategoryAppService : IApplicationService
{
	Task<PaginatedLibraryChargeCategoryResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10);
	Task<LibraryChargeCategoryDto> GetAsync(Guid categoryId);
	Task<GenericCreatedResponse> CreateAsync(Guid academicYearId, LibraryChargeCategoryCreateRequest input);
	Task<GenericUpdatedResponse> UpdateAsync(Guid categoryId, LibraryChargeCategoryUpdateRequest input);
	Task DeleteAsync(Guid categoryId);
}
