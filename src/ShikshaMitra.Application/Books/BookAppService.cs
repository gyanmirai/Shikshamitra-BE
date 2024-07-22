using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Authors;
using ShikshaMitra.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Features;

namespace ShikshaMitra.Books;

[Authorize(ShikshaMitraPermissions.Books.Default)]
//[RequiresFeature("ShikshaMitra.PdfReporting")]
public class BookAppService :
    CrudAppService<
        Book, // The Book entity
        BookDto, // Used to show books
        Guid, // Primary key of the book entity
        PagedAndSortedResultRequestDto, // Used for paging/sorting
        CreateUpdateBookDto>, // Used to create/update a book
    IBookAppService // Implement the IBookAppService
{
    private readonly IAuthorRepository _authorRepository;

    public BookAppService(
        IRepository<Book, Guid> repository,
        IAuthorRepository authorRepository)
        : base(repository)
    {
        _authorRepository = authorRepository;
        GetPolicyName = ShikshaMitraPermissions.Books.Default;
        GetListPolicyName = ShikshaMitraPermissions.Books.Default;
        CreatePolicyName = ShikshaMitraPermissions.Books.Create;
        UpdatePolicyName = ShikshaMitraPermissions.Books.Edit;
        DeletePolicyName = ShikshaMitraPermissions.Books.Delete;
    }

    public override async Task<BookDto> GetAsync(Guid id)
    {
        // Get the IQueryable<Book> from the repository
        IQueryable<Book> queryable = await Repository.GetQueryableAsync();

        // Prepare a query to join books and authors using LINQ
        var query = queryable
            .Join(await _authorRepository.GetQueryableAsync(),
                book => book.AuthorId,
                author => author.Id,
                (book, author) => new { book, author })
            .Where(x => x.book.Id == id)
            .Select(x => new { x.book, x.author });

        // Execute the query and get the book with author
        var queryResult = await AsyncExecuter.FirstOrDefaultAsync(query) ?? throw new EntityNotFoundException(typeof(Book), id);
        var bookDto = ObjectMapper.Map<Book, BookDto>(queryResult.book);
        bookDto.AuthorName = queryResult.author.Name;
        return bookDto;
    }

    public override async Task<PagedResultDto<BookDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        // Get the IQueryable<Book> from the repository
        var queryable = await Repository.GetQueryableAsync();

        // Prepare a query to join books and authors using LINQ
        var query = queryable
            .Join(await _authorRepository.GetQueryableAsync(),
                book => book.AuthorId,
                author => author.Id,
                (book, author) => new { book, author });

        // Apply sorting, paging, and execute the query
        var queryResult = await AsyncExecuter.ToListAsync(query
            .OrderBy(NormalizeSorting(input.Sorting))
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount));


        // Convert the query result to a list of BookDto objects
        var bookDtos = queryResult.Select(x =>
        {
            var bookDto = ObjectMapper.Map<Book, BookDto>(x.book);
            bookDto.AuthorName = x.author.Name;
            return bookDto;
        }).ToList();

        // Get the total count with another query
        var totalCount = await Repository.GetCountAsync();

        return new PagedResultDto<BookDto>(
            totalCount,
            bookDtos
        );
    }

    public async Task<ListResultDto<AuthorLookupDto>> GetAuthorLookupAsync()
    {
        var authors = await _authorRepository.GetListAsync();

        return new ListResultDto<AuthorLookupDto>(
            ObjectMapper.Map<List<Author>, List<AuthorLookupDto>>(authors)
        );
    }

    private static string NormalizeSorting(string sorting)
    {
        if (sorting.IsNullOrEmpty())
        {
            return $"book.{nameof(Book.Name)}";
        }

        if (sorting.Contains("authorName", StringComparison.OrdinalIgnoreCase))
        {
            return sorting.Replace(
                "authorName",
                "author.Name",
                StringComparison.OrdinalIgnoreCase
            );
        }

        return $"book.{sorting}";
    }
}