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

namespace ShikshaMitra.Library.BookManagement.RetrunBooks
{
	public class ReturnBookAppService(
	 	 IRepository<ReturnBooks.ReturnBook, Guid> returnBookRepository,
	     IGuidGenerator guidGenerator)
	     : ApplicationService, IReturnBookAppService
		{
		public async Task<ReturnBookDto> CreateAsync(CreateUpdateReturnBookDto input)
		{
			try
			{
				var returnBook = new ReturnBooks.ReturnBook(
					guidGenerator.Create(),
					input.BookId,
					input.BookTitle,
					input.MemberId,
					input.MemberName,
					input.DueDate,
					input.ReturnDate,
					input.Condition,
					input.FineAmount,
					input.FineReason,
					input.ReceivedFineAmount,
					input.PaymentMode,
					input.NotificationSentOn,
					input.NotificationStatus,
					input.Action,
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

		public async Task<ReturnBookDto> UpdateAsync(Guid id, CreateUpdateReturnBookDto input)
		{
			try
			{
				var returnBook = await returnBookRepository.FindAsync(id)
								  ?? throw new EntityNotFoundException(typeof(ReturnBooks.ReturnBook), id);

				ObjectMapper.Map(input, returnBook);
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

        public async Task<PagedResultDto<ReturnBookDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            try
            {
                var queryable = await returnBookRepository.GetQueryableAsync();
                if (CurrentTenant.Id.HasValue)
                    queryable = queryable.Where(x => x.TenantId == CurrentTenant.Id);

                var totalCount = await AsyncExecuter.CountAsync(queryable);

                // Explicitly specify the type arguments for OrderBy to resolve CS0411  
                var items = await AsyncExecuter.ToListAsync(
                    queryable.OrderBy<ReturnBooks.ReturnBook, DateTime>(x => x.ReturnDate)
                             .Skip(input.SkipCount)
                             .Take(input.MaxResultCount)
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
}
