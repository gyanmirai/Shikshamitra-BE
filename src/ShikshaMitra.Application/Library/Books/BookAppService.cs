using ShikshaMitra.Library.BooksAssignements;
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
       IGuidGenerator guidGenerator
   ) : ApplicationService, IBookAppService
{
    public async Task <BookDto>AddNewBookAsync(AddNewBookDto addNewBookDto)
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

            book.AvailableCopies -= 1;
            await bookRepository.UpdateAsync(book, autoSave: true);
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
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while returning the book.", ex);
        }
    }
}
