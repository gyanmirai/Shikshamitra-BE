using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Books;

public interface IBookAppService : IApplicationService
{
    Task<PaginatedResponse<BookDto>> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10);
    Task<BookDto> GetAsync(Guid bookId);
    Task<GenericCreatedResponse> CreateAsync(Guid? academicYearId, CreateBookRequest input);
    Task<GenericUpdatedResponse> UpdateAsync(Guid bookId, UpdateBookRequest input);
    Task DeleteAsync(Guid bookId);
}
