using ShikshaMitra.Library.BooksAssignments;
using ShikshaMitra.Library.BooksAssignments;
using ShikshaMitra.Library.BooksReservations;
using ShikshaMitra.Library.Reservations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

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

	public async Task IssueBookAsync(IssueBookDto input)
	{
		try
		{
			var book = await bookRepository.GetAsync(input.BookId);

			if (book.AvailableCopies <= 0)
				throw new BusinessException("No copies available to issue.");

			var alreadyIssued = await bookAssignmentRepository.FirstOrDefaultAsync(x =>
				x.BookId == input.BookId &&
				x.MemberId == input.MemberId &&
				x.ReturnDate == null);

			if (alreadyIssued != null)
				throw new BusinessException("This book is already issued to the student.");

			book.AvailableCopies -= 1;
			await bookRepository.UpdateAsync(book, autoSave: true);

			var assignment = new BooksAssignment(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			bookId: input.BookId,
			memberId: input.MemberId,
			issueDate: Clock.Now,
			dueDate: input.DueDate,
			returnDate: null,
			fineAmount: 0,
			penaltyAmount: 0,
			penaltyReason: null,
			paymentMode: PaymentMode.Cash,
			bankAccountNumber: null,
			ifscCode: null,
			accountHolderName: null,
			notes: null,
			status: "Issued"
		);

			await bookAssignmentRepository.InsertAsync(assignment, autoSave: true);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while issuing the book.", ex);
		}
	}


	public async Task ReturnBookAsync(ReturnBookDto input)
	{
		try
		{
			var book = await bookRepository.GetAsync(input.BookId);
			book.AvailableCopies += 1;
			await bookRepository.UpdateAsync(book, autoSave: true);

			var assignment = await bookAssignmentRepository.FirstOrDefaultAsync(x =>
				x.BookId == input.BookId &&
				x.MemberId == input.MemberId &&
				x.ReturnDate == null);

			if (assignment == null)
				throw new BusinessException("No active assignment found for this member and book.");

			assignment.ReturnDate = Clock.Now;
			assignment.Status = "Returned";
			assignment.Notes = input.Notes;
			assignment.FineAmount = input.FineAmount;
			assignment.PenaltyAmount = input.PenaltyAmount;
			assignment.PenaltyReason = input.PenaltyReason;
			assignment.PaymentMode = input.PaymentMode;
			assignment.BankAccountNumber = input.BankAccountNumber;
			assignment.IFSCCode = input.IFSCCode;
			assignment.AccountHolderName = input.AccountHolderName;

			await bookAssignmentRepository.UpdateAsync(assignment, autoSave: true);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while returning the book.", ex);
		}
	}

	public async Task<List<BookOverviewDto>> GetBookOverviewAsync()
	{
		var books = await bookRepository.GetListAsync();
		var assignments = await bookAssignmentRepository.GetListAsync();
		var reservations = await bookReservationRepository.GetListAsync();

		var overview = books.Select(book =>
		{
			var borrowed = assignments.Count(a => a.BookId == book.Id && a.ReturnDate == null && a.Status != "Returned");
			var overdue = assignments.Count(a => a.BookId == book.Id && a.ReturnDate == null && a.DueDate < Clock.Now);
			var reserved = reservations.Count(r => r.BookId == book.Id &&
			   (r.Status == ReservationStatus.Pending ||
				r.Status == ReservationStatus.Ready ||
				r.Status == ReservationStatus.Collected));

			return new BookOverviewDto
			{
				Id = book.Id,
				Isbn = book.ISBN,
				Name = book.BookTitle,
				BorrowedCount = borrowed,
				OverdueCount = overdue,
				ReservedCount = reserved
			};
		}).ToList();

		return overview;
	}
}
