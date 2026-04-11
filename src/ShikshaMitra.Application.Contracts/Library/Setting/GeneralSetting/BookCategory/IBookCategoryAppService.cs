using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;

public interface IBookCategoryAppService : IApplicationService
{
	Task<PaginatedResponse<BookCategoryDto>> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10);
	Task<BookCategoryDto> GetAsync(Guid categoryId);
	Task<GenericCreatedResponse> CreateAsync(Guid? academicYearId, BookCategoryCreateRequest input);
	Task<GenericUpdatedResponse> UpdateAsync(Guid categoryId, BookCategoryUpdateRequest input);
	Task DeleteAsync(Guid categoryId);
}
