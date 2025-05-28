using ShikshaMitra.Library.BookManagement.AddNewBook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Library.BookManagement.AddNewBooks;

public class AddNewBookAppService(
	 IRepository<AddNewBook, Guid> bookRepository,
     IGuidGenerator guidGenerator)
	: ApplicationService, IAddNewBookAppService
{
	public async Task<AddNewBookDto> CreateAsync(CreateUpdateAddNewBookDto createAddNewBookDto)
	{
		try
		{
			var book = new AddNewBook(
				guidGenerator.Create(),
				createAddNewBookDto.BookTitle,
				createAddNewBookDto.BookAuthor,
				createAddNewBookDto.ISBN,
				createAddNewBookDto.Publisher,
				createAddNewBookDto.Year,
				createAddNewBookDto.AvailableCopies,
				createAddNewBookDto.Category,
				createAddNewBookDto.Condition,
				createAddNewBookDto.PicturePath,
				createAddNewBookDto.EditionNotes,
				CurrentTenant.Id
			);

			await bookRepository.InsertAsync(book, autoSave: true);
			return ObjectMapper.Map<AddNewBook, AddNewBookDto>(book);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while creating the book.", ex);
		}
	}

	public async Task<AddNewBookDto> UpdateAsync(Guid id, CreateUpdateAddNewBookDto updateAddNewBookDto)
	{
		try
		{
			var book = await bookRepository.FindAsync(id);
			if (book == null)
			{
				throw new EntityNotFoundException(typeof(AddNewBook), id);
			}

			ObjectMapper.Map(updateAddNewBookDto, book);
			await bookRepository.UpdateAsync(book, autoSave: true);

			return ObjectMapper.Map<AddNewBook, AddNewBookDto>(book);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while updating the book.", ex);
		}
	}

	public async Task DeleteAsync(Guid id)
	{
		try
		{
			var book = await bookRepository.FindAsync(id);
			if (book == null)
			{
				throw new EntityNotFoundException(typeof(AddNewBook), id);
			}

			await bookRepository.DeleteAsync(book);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while deleting the book.", ex);
		}
	}

	public async Task<AddNewBookDto> GetAsync(Guid id)
	{
		try
		{
			var book = await bookRepository.GetAsync(id);
			return ObjectMapper.Map<AddNewBook, AddNewBookDto>(book);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while retrieving the book.", ex);
		}
	}

        public async Task<PagedResultDto<AddNewBookDto>> GetListAsync(PagedAndSortedResultRequestDto addNewBook)
        {
            try
            {
                var tenantId = CurrentTenant.Id;
                var queryable = await bookRepository.GetQueryableAsync();

                if (tenantId.HasValue)
                {
                    queryable = queryable.Where(b => b.TenantId == tenantId.Value);
                }

                var totalCount = await AsyncExecuter.CountAsync(queryable);

                var books = await AsyncExecuter.ToListAsync(
                    queryable
                        .OrderBy<AddNewBook, string>(b => addNewBook.Sorting ?? b.BookTitle)
                        .Skip(addNewBook.SkipCount)
                        .Take(addNewBook.MaxResultCount)
                );

                var dtoList = ObjectMapper.Map<List<AddNewBook>, List<AddNewBookDto>>(books);

                return new PagedResultDto<AddNewBookDto>(totalCount, dtoList);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An error occurred while retrieving books.", ex);
            }
        }
   	}
