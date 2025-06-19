using ShikshaMitra.Library.BooksAssignments;
using ShikshaMitra.Library.BooksReservations;
using ShikshaMitra.Library.Reservations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using static ShikshaMitra.Security.Permissions.ShikshaMitraPermissions;

namespace ShikshaMitra.Library.Books;

public class BookAppService(
	  IRepository<Book, Guid> bookRepository,
	  IRepository<BooksAssignment, Guid> bookAssignmentRepository,
	  IRepository<BooksReservation, Guid> bookReservationRepository,
	  IGuidGenerator guidGenerator
  ) : ApplicationService, IBookAppService
{
	public async Task<BookDto> AddNewBookAsync(AddNewBookDto addNewBookDto)
	{
		var book = new Book(
			guidGenerator.Create(),
			addNewBookDto.BookTitle,
			addNewBookDto.BookAuthor,
			addNewBookDto.ISBN,
			addNewBookDto.Publisher,
			addNewBookDto.Year,
			addNewBookDto.AvailableCopies,
			addNewBookDto.BookCategoryId,
			addNewBookDto.PicturePath,
			addNewBookDto.EditionNotes,
			CurrentTenant.Id
		);

		await bookRepository.InsertAsync(book, autoSave: true);
		return ObjectMapper.Map<Book, BookDto>(book);
	}
    public async Task<BookDto> GetByIdAsync(Guid id)
	{
		try
		{
			var book = await bookRepository.GetAsync(id);
			return ObjectMapper.Map<Book, BookDto>(book);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while retrieving the book.", ex);
		}
	}
    public async Task<ListResultDto<BookDto>> GetAllAsync()
	{
		try
		{
			var books = await bookRepository.GetListAsync();
			var mapped = ObjectMapper.Map<List<Book>, List<BookDto>>(books);
			return new ListResultDto<BookDto>(mapped);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while retrieving all books.", ex);
		}
	}
	public async Task<List<BookOverviewDto>> GetBookOverviewAsync()
	{
		var books = await bookRepository.GetListAsync();
		var assignments = await bookAssignmentRepository.GetListAsync();
		var reservations = await bookReservationRepository.GetListAsync();

		var overview = books.ConvertAll(book =>
		{
			var borrowed = assignments.Count(a => a.BookId == book.Id && a.ReturnDate == null);
			var overdue = assignments.Count(a => a.BookId == book.Id && a.ReturnDate == null && a.DueDate < Clock.Now);

			var reserved = reservations.Count(r =>
				r.BookId == book.Id &&
				(r.Status == ReservationStatus.Pending ||
				 r.Status == ReservationStatus.Ready ||
				 r.Status == ReservationStatus.Collected)
			);

			return new BookOverviewDto
			{
				Id = book.Id,
				Isbn = book.ISBN,
				Name = book.BookTitle,
				Borrowed = borrowed,
				Overdue = overdue,
				Reserved = reserved
			};
		});

		return overview;
	}
}
