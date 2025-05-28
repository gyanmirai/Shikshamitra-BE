using ShikshaMitra.Library.BookManagement.ReturnBook;
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

namespace ShikshaMitra.Library.BookManagement.RetrunBooks;

public class ReturnBookAppService(
 	 IRepository<ReturnBooks.ReturnBook, Guid> returnBookRepository,
     IGuidGenerator guidGenerator)
     : ApplicationService, IReturnBookAppService
	{
	public async Task<ReturnBookDto> CreateAsync(CreateUpdateReturnBookDto createReturnBookDto)
	{
		try
		{
			var returnBook = new ReturnBooks.ReturnBook(
				guidGenerator.Create(),
				createReturnBookDto.BookId,
				createReturnBookDto.BookTitle,
				createReturnBookDto.MemberId,
				createReturnBookDto.MemberName,
				createReturnBookDto.DueDate,
				createReturnBookDto.ReturnDate,
				createReturnBookDto.Condition,
				createReturnBookDto.FineAmount,
				createReturnBookDto.FineReason,
				createReturnBookDto.ReceivedFineAmount,
				createReturnBookDto.PaymentMode,
				createReturnBookDto.NotificationSentOn,
				createReturnBookDto.NotificationStatus,
				createReturnBookDto.Action,
				CurrentTenant.Id
			);

			await returnBookRepository.InsertAsync(returnBook, autoSave: true);
			return ObjectMapper.Map<ReturnBooks.ReturnBook, ReturnBookDto>(returnBook);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while processing the return.", ex);
		}
	}

	public async Task<ReturnBookDto> UpdateAsync(Guid id, CreateUpdateReturnBookDto updateReturnBookDto)
	{
		try
		{
			var returnBook = await returnBookRepository.FindAsync(id)
							  ?? throw new EntityNotFoundException(typeof(ReturnBooks.ReturnBook), id);

			ObjectMapper.Map(updateReturnBookDto, returnBook);
			await returnBookRepository.UpdateAsync(returnBook, autoSave: true);

			return ObjectMapper.Map<ReturnBooks.ReturnBook, ReturnBookDto>(returnBook);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while updating the return record.", ex);
		}
	}

	public async Task DeleteAsync(Guid id)
	{
		try
		{
			var returnBook = await returnBookRepository.FindAsync(id)
							  ?? throw new EntityNotFoundException(typeof(ReturnBooks.ReturnBook), id);

			await returnBookRepository.DeleteAsync(returnBook);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while deleting the return record.", ex);
		}
	}

	public async Task<ReturnBookDto> GetAsync(Guid id)
	{
		try
		{
			var returnBook = await returnBookRepository.GetAsync(id);
			return ObjectMapper.Map<ReturnBooks.ReturnBook, ReturnBookDto>(returnBook);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while retrieving the return record.", ex);
		}
	}

        public async Task<PagedResultDto<ReturnBookDto>> GetListAsync(PagedAndSortedResultRequestDto returnBookDto)
        {
            try
            {
                var queryable = await returnBookRepository.GetQueryableAsync();
                if (CurrentTenant.Id.HasValue)
                    queryable = queryable.Where(x => x.TenantId == CurrentTenant.Id);

                var totalCount = await AsyncExecuter.CountAsync(queryable);

                var items = await AsyncExecuter.ToListAsync(
                    queryable.OrderBy<ReturnBooks.ReturnBook, DateTime>(x => x.ReturnDate)
                             .Skip(returnBookDto.SkipCount)
                             .Take(returnBookDto.MaxResultCount)
                );

                var dtoList = ObjectMapper.Map<List<ReturnBooks.ReturnBook>, List<ReturnBookDto>>(items);

                return new PagedResultDto<ReturnBookDto>(totalCount, dtoList);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An error occurred while retrieving returned books.", ex);
            }
        }
   	}
