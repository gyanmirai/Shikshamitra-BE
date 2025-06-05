using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Books
{
	public interface IBookAppService : IApplicationService
	{
		Task<PagedResultDto<BookDto>> GetListAsync(PagedAndSortedResultRequestDto input);
		Task<BookDto> GetAsync(Guid id);
		Task<BookDto> CreateAsync(BookDto input);
		Task<BookDto> UpdateAsync(Guid id, BookDto input);
		Task DeleteAsync(Guid id);
	}
}
