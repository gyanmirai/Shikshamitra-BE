using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Books;

public interface IBookAppService : IApplicationService
{
    Task<PaginatedResponse<BookDto>> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10);
    Task<BookDetailDto> GetAsync(Guid bookId);
    Task<GenericCreatedResponse> CreateAsync(Guid? academicYearId, CreateBookRequest input);
    Task<GenericUpdatedResponse> UpdateAsync(Guid bookId, UpdateBookRequest input);
    Task DeleteAsync(Guid bookId);
    Task<GenericUpdatedResponse> PutCopiesAsync(Guid bookId, PutBookCopiesRequest input);
    Task<GenericUpdatedResponse> UpdateCopyStatusAsync(Guid bookId, string accessionNumber, UpdateBookCopyStatusRequest input);
    Task<GenericCreatedResponse> ReserveAsync(Guid bookId, CreateBookReservationRequest input);
}
