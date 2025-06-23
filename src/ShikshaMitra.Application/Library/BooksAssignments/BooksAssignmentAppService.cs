using ShikshaMitra.Library.Books;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.BooksAssignments;

public class BooksAssignmentAppService(
    IRepository<BooksAssignment, Guid> bookAssignmentRepository,
	IRepository<Book, Guid> bookRepository,
	IGuidGenerator guidGenerator,
	ICurrentTenant currentTenant
) : ApplicationService, IBooksAssignmentAppService
{
	public async Task<BooksAssignmentDto> CreateAsync(CreateUpdateBooksAssignmentDto createDto)
	{
		var id = guidGenerator.Create();

		var book = await bookRepository.GetAsync(createDto.BookId);
		if (book == null)
		{
			throw new BusinessException("The specified BookId does not exist.");
		}

		var assignment = new BooksAssignment(
			id,
			currentTenant.Id,
			createDto.BookId,
			createDto.MemberId,
			createDto.BookTitle,
			createDto.IssueDate,
			createDto.DueDate,
			createDto.ReturnDate
		);

		await bookAssignmentRepository.InsertAsync(assignment, autoSave: true);

		return MapToDto(assignment);
	}

	public async Task<List<BooksAssignmentDto>> GetListAsync()
	{
		var assignments = await bookAssignmentRepository.GetListAsync();
		return assignments.Select(MapToDto).ToList();
	}

	public async Task<BooksAssignmentDto> GetAsync(Guid id)
	{
		var assignment = await bookAssignmentRepository.GetAsync(id);
		return MapToDto(assignment);
	}

	public async Task<BooksAssignmentDto> UpdateAsync(Guid id, CreateUpdateBooksAssignmentDto updateDto)
	{
		var assignment = await bookAssignmentRepository.GetAsync(id);

		if (assignment == null)
		{
			throw new BusinessException("assignment not found");
		}

		var bookExists = await bookRepository.AnyAsync(x => x.Id == updateDto.BookId);
		if (!bookExists)
		{
			throw new BusinessException("The specified BookId does not exist.");
		}

		assignment.BookId = updateDto.BookId;
		assignment.MemberId = updateDto.MemberId;
		assignment.BookTitle = updateDto.BookTitle;
		assignment.IssueDate = updateDto.IssueDate;
		assignment.DueDate = updateDto.DueDate;
		assignment.ReturnDate = updateDto.ReturnDate;

		await bookAssignmentRepository.UpdateAsync(assignment, autoSave: true);
		return MapToDto(assignment);
	}

    public async Task<string> IssueBookAsync(IssueBookDto issueBookDto)
    {
        try
        {
            var book = await bookRepository.GetAsync(issueBookDto.BookId);

            if (book.AvailableCopies <= 0)
                throw new BusinessException("No copies available to issue.");

            var alreadyIssued = await bookAssignmentRepository.FirstOrDefaultAsync(x =>
                x.BookId == issueBookDto.BookId &&
                x.MemberId == issueBookDto.MemberId &&
                x.ReturnDate == null);

            if (alreadyIssued != null)
                throw new BusinessException("This book is already issued to the student.");

            book.AvailableCopies -= 1;
            await bookRepository.UpdateAsync(book, autoSave: true);

            var assignment = new BooksAssignment(
                id: guidGenerator.Create(),
                tenantId: CurrentTenant.Id,
                bookId: issueBookDto.BookId,
                memberId: issueBookDto.MemberId,
			    bookTitle: book.BookTitle,
				issueDate: Clock.Now,
                dueDate: issueBookDto.DueDate,
                returnDate: null
            );

            await bookAssignmentRepository.InsertAsync(assignment, autoSave: true);

            return "Book issued successfully.";
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while issuing the book.", ex);
        }
    }

	public async Task<string> ReturnBookAsync(ReturnBookDto returnBookDto)
	{
		try
		{
			var book = await bookRepository.GetAsync(returnBookDto.BookId);
			book.AvailableCopies += 1;
			await bookRepository.UpdateAsync(book, autoSave: true);

			var assignment = await bookAssignmentRepository.FirstOrDefaultAsync(x =>
				x.BookId == returnBookDto.BookId &&
				x.MemberId == returnBookDto.MemberId &&
				x.ReturnDate == null);

			if (assignment == null)
				throw new BusinessException("No active assignment found for this member and book.");

			assignment.ReturnDate = Clock.Now;
			await bookAssignmentRepository.UpdateAsync(assignment, autoSave: true);

			return "Book returned successfully.";
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while returning the book.", ex);
		}
	}

	private static BooksAssignmentDto MapToDto(BooksAssignment x) => new BooksAssignmentDto
    {
        Id = x.Id,
        TenantId = x.TenantId,
        BookId = x.BookId,
        MemberId = x.MemberId,
		BookTitle = x.BookTitle,
		IssueDate = x.IssueDate,
        DueDate = x.DueDate,
        ReturnDate = x.ReturnDate,
    };
}
