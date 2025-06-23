using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;

public interface IBookCategoryAppService : IApplicationService
{
	Task<BookCategoryDto> GetAsync(Guid id);
	Task<PagedResultDto<BookCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input);

	Task<BookCategoryDto> CreateAsync(CreateUpdateBookCategoryDto input);

	Task<BookCategoryDto> UpdateAsync(Guid id, CreateUpdateBookCategoryDto input);
    Task DeleteAsync(Guid id);

}
