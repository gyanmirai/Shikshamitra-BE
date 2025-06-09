using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Books;

public interface IBookAppService : IApplicationService
{
	Task <BookDto>AddNewBookAsync(AddNewBookDto input);
    Task<ListResultDto<BookDto>> GetAllAsync();
	Task<BookDto> GetByIdAsync(Guid id);
	Task IssueBookAsync(IssueBookDto input);
	Task ReturnBookAsync(ReturnBookDto input);
}
