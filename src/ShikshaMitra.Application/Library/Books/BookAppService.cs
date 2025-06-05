using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Books;

public class BookAppService(
		IRepository<Book, Guid> bookRepository
	) : ApplicationService, IBookAppService
{
	public async Task<BookDto> CreateAsync(BookDto input)
	{
		var book = new Book(
			GuidGenerator.Create(),
			input.BookTitle,
			input.BookAuthor,
			input.ISBN,
			input.Publisher,
			input.Year,
			input.AvailableCopies,
			input.Category,
			string.Empty, // No condition field in entity
			input.PicturePath,
			input.EditionNotes,
			CurrentTenant.Id
		);

		await bookRepository.InsertAsync(book, autoSave: true);
		return ObjectMapper.Map<Book, BookDto>(book);
	}

	public async Task<BookDto> GetAsync(Guid id)
	{
		var book = await bookRepository.GetAsync(id);
		var dto = ObjectMapper.Map<Book, BookDto>(book);

		// Simulated logic - replace with actual business logic
		dto.IsBorrowed = false;
		dto.IsReserved = false;
		dto.IsOverdue = false;

		return dto;
	}

	public async Task<PagedResultDto<BookDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		var queryable = await bookRepository.GetQueryableAsync();

		var totalCount = queryable.Count();

		var books = queryable
			.OrderBy(x => x.BookTitle)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount)
			.ToList();

		var result = books.Select(book =>
		{
			var dto = ObjectMapper.Map<Book, BookDto>(book);
			dto.IsBorrowed = false;
			dto.IsReserved = false;
			dto.IsOverdue = false;
			return dto;
		}).ToList();

		return new PagedResultDto<BookDto>(totalCount, result);
	}

	public async Task<BookDto> UpdateAsync(Guid id, BookDto input)
	{
		var book = await bookRepository.GetAsync(id);

		book.BookTitle = input.BookTitle;
		book.BookAuthor = input.BookAuthor;
		book.ISBN = input.ISBN;
		book.Publisher = input.Publisher;
		book.Year = input.Year;
		book.AvailableCopies = input.AvailableCopies;
		book.Category = input.Category;
		book.PicturePath = input.PicturePath;
		book.EditionNotes = input.EditionNotes;

		await bookRepository.UpdateAsync(book, autoSave: true);

		return ObjectMapper.Map<Book, BookDto>(book);
	}

	public async Task DeleteAsync(Guid id)
	{
		await bookRepository.DeleteAsync(id);
	}
}