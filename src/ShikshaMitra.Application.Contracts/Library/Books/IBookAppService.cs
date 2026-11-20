using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Books;

public interface IBookAppService : IApplicationService
{
    Task<PaginatedResponse<BookDto>> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null, string? alphabet = null);
    Task<BookDetailDto> GetAsync(Guid bookId);
    Task<LibraryBookEditorOptionsDto> GetEditorOptionsAsync(Guid academicYearId);
    Task<LibraryBookCreatedResponse> CreateAsync(Guid? academicYearId, CreateBookRequest input);
    Task<LibraryBookUpdatedResponse> UpdateAsync(Guid bookId, UpdateBookRequest input);
    Task DeleteAsync(Guid bookId);
    Task<PaginatedResponse<BookCopyDto>> GetCopiesAsync(Guid bookId, int page = 1, int pageSize = 20);
    Task<LibraryCopyLabelBatchDto> GetCopyLabelsAsync(Guid bookId);
    Task<LibraryBookUpdatedResponse> PutCopiesAsync(Guid bookId, PutBookCopiesRequest input);
    Task<GenericUpdatedResponse> UpdateCopyStatusAsync(Guid bookId, string accessionNumber, UpdateBookCopyStatusRequest input);
    Task<PaginatedResponse<BookReservationDto>> GetReservationsAsync(Guid bookId, int page = 1, int pageSize = 20);
    Task<GenericCreatedResponse> ReserveAsync(Guid bookId, CreateBookReservationRequest input);
    Task DeleteReservationAsync(Guid bookId, Guid reservationId);
}
