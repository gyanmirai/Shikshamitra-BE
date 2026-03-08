using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Books;

[RemoteService(false)]
public class BookAppService(
    IRepository<Book, Guid> _bookRepository,
    IRepository<BookCopy, Guid> _bookCopyRepository,
    IRepository<BookCategory, Guid> _categoryRepository,
    IRepository<LibrarySection, Guid> _sectionRepository,
    IRepository<LibraryShelf, Guid> _shelfRepository,
    IRepository<BookReservation, Guid> _reservationRepository)
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

    public async Task<BookDetailDto> GetAsync(Guid bookId)
    {
        var book = await _bookRepository.GetAsync(bookId);

        // Load copies for this book
        var copiesQuery = await _bookCopyRepository.GetQueryableAsync();
        var copies = await AsyncExecuter.ToListAsync(
            copiesQuery.Where(c => c.BookId == bookId));

        // Resolve category name
        var category = await _categoryRepository.FindAsync(book.CategoryId);

        // Batch-resolve section and shelf names from copies
        var sectionIds = copies.Select(c => c.SectionId).Distinct().ToList();
        var shelfIds = copies.Select(c => c.ShelfId).Distinct().ToList();

        var sectionsQuery = await _sectionRepository.GetQueryableAsync();
        var sections = await AsyncExecuter.ToListAsync(
            sectionsQuery.Where(s => sectionIds.Contains(s.Id)));
        var sectionLookup = sections.ToDictionary(s => s.Id, s => s.SectionName ?? string.Empty);

        var shelvesQuery = await _shelfRepository.GetQueryableAsync();
        var shelves = await AsyncExecuter.ToListAsync(
            shelvesQuery.Where(s => shelfIds.Contains(s.Id)));
        var shelfLookup = shelves.ToDictionary(s => s.Id, s => s.ShelfName ?? string.Empty);

        return new BookDetailDto
        {
            Id = book.Id,
            Title = book.Title,
            Author = book.Author,
            Publisher = book.Publisher,
            Subject = book.Subject,
            Isbn = book.Isbn,
            Language = book.Language.ToString(),
            Edition = book.Edition,
            Category = category != null
                ? new NamedIdDto { Id = category.Id, Name = category.Name }
                : new NamedIdDto { Id = book.CategoryId, Name = string.Empty },
            Pages = book.Pages,
            Price = book.Price,
            AddedOn = book.CreationTime,
            Copies = copies.Select(c => new BookDetailCopyDto
            {
                CopyId = c.Id,
                AccessionNo = c.AccessionNumber,
                Status = (int) c.Status,
                Section = new NamedIdDto
                {
                    Id = c.SectionId,
                    Name = sectionLookup.GetValueOrDefault(c.SectionId, string.Empty)
                },
                Shelf = new NamedIdDto
                {
                    Id = c.ShelfId,
                    Name = shelfLookup.GetValueOrDefault(c.ShelfId, string.Empty)
                },
                Remarks = c.Remarks
            }).ToList()
        };
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

    public async Task<GenericUpdatedResponse> UpdateCopyStatusAsync(Guid bookId, string accessionNumber, UpdateBookCopyStatusRequest input)
    {
        var query = await _bookCopyRepository.GetQueryableAsync();
        var copy = await AsyncExecuter.FirstOrDefaultAsync(
            query.Where(x => x.BookId == bookId && x.AccessionNumber == accessionNumber));

        if (copy == null)
        {
            throw new UserFriendlyException($"Book copy with accession number '{accessionNumber}' not found.", code: "404");
        }

        copy.Status = input.Status;
        copy.Remarks = input.Remarks;

        await _bookCopyRepository.UpdateAsync(copy, autoSave: true);

        return new GenericUpdatedResponse
        {
            Id = copy.Id.ToString(),
            Message = "Book copy status updated successfully."
        };
    }

    public async Task<GenericCreatedResponse> ReserveAsync(Guid bookId, CreateBookReservationRequest input)
    {
        // Verify book exists
        await _bookRepository.GetAsync(bookId);

        var reservation = new BookReservation(
            GuidGenerator.Create(),
            bookId,
            input.MemberId,
            input.RequestedDate,
            input.ReserveDueDate,
            CurrentTenant.Id
        );

        await _reservationRepository.InsertAsync(reservation, autoSave: true);

        return new GenericCreatedResponse
        {
            Id = reservation.Id.ToString(),
            Message = "Book reservation created successfully."
        };
    }
}
