using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Books;

[RemoteService(false)]
public class BookAppService(
    IRepository<Book, Guid> _bookRepository,
    IRepository<BookCopy, Guid> _bookCopyRepository)
    : ApplicationService, IBookAppService
{
    public async Task<PaginatedResponse<BookDto>> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10)
    {
        var query = await _bookRepository.GetQueryableAsync();
        query = query.Where(x => x.AcademicYearId == academicYearId);

        var totalCount = await AsyncExecuter.CountAsync(query);
        var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

        var items = await AsyncExecuter.ToListAsync(
            query
                .OrderByDescending(x => x.CreationTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
        );

        return new PaginatedResponse<BookDto>
        {
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Items = ObjectMapper.Map<List<Book>, List<BookDto>>(items)
        };
    }

    public async Task<BookDto> GetAsync(Guid bookId)
    {
        var entity = await _bookRepository.GetAsync(bookId);
        return ObjectMapper.Map<Book, BookDto>(entity);
    }

    public async Task<GenericCreatedResponse> CreateAsync(Guid? academicYearId, CreateBookRequest input)
    {
        var query = await _bookRepository.GetQueryableAsync();
        var exists = query.Any(x => x.Isbn == input.Isbn && x.Edition == input.Edition);
        if (exists)
        {
            throw new UserFriendlyException("A book with the same ISBN and edition already exists.", code: "409");
        }

        var entity = new Book(
            input.Title,
            input.Isbn,
            input.Author,
            input.Publisher,
            input.Edition,
            input.Subject,
            input.CategoryId,
            input.Language,
            input.ApplicableAge,
            input.Price,
            input.Pages,
            input.NumberOfCopies,
            academicYearId,
            CurrentTenant.Id
        );

        await _bookRepository.InsertAsync(entity, autoSave: true);

        return new GenericCreatedResponse
        {
            Id = entity.Id.ToString(),
            Message = "Book created successfully."
        };
    }

    public async Task<GenericUpdatedResponse> UpdateAsync(Guid bookId, UpdateBookRequest input)
    {
        var entity = await _bookRepository.GetAsync(bookId);

        entity.Title = input.Title;
        entity.Isbn = input.Isbn;
        entity.Author = input.Author;
        entity.Publisher = input.Publisher;
        entity.Edition = input.Edition;
        entity.Subject = input.Subject;
        entity.CategoryId = input.CategoryId;
        entity.Language = input.Language;
        entity.ApplicableAge = input.ApplicableAge;
        entity.Price = input.Price;
        entity.Pages = input.Pages;
        entity.NumberOfCopies = input.NumberOfCopies;

        await _bookRepository.UpdateAsync(entity, autoSave: true);

        return new GenericUpdatedResponse
        {
            Id = entity.Id.ToString(),
            Message = "Book updated successfully."
        };
    }

    public async Task DeleteAsync(Guid bookId)
    {
        await _bookRepository.DeleteAsync(bookId, autoSave: true);
    }

    public async Task<GenericUpdatedResponse> PutCopiesAsync(Guid bookId, PutBookCopiesRequest input)
    {
        var book = await _bookRepository.GetAsync(bookId);

        // Delete existing copies
        await _bookCopyRepository.DeleteDirectAsync(x => x.BookId == bookId);

        // Insert new copies
        var copies = input.Copies.Select(c => new BookCopy(
            GuidGenerator.Create(),
            bookId,
            c.CopyNumber,
            c.AccessionNumber,
            c.SectionId,
            c.ShelfId
        )).ToList();

        await _bookCopyRepository.InsertManyAsync(copies, autoSave: true);

        return new GenericUpdatedResponse
        {
            Id = book.Id.ToString(),
            Message = "Book copies updated successfully."
        };
    }
}
