using ShikshaMitra.Library.BookManagement.IssueBook;
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

namespace ShikshaMitra.Library.BookManagement.IssueBooks
{
	public class IssueBookAppService(
	  IRepository<IssueBooks.IssueBook, Guid> issueBookRepository,
	  IGuidGenerator guidGenerator)
	  : ApplicationService, IIssueBookAppService
	{
		public async Task<IssueBookDto> CreateAsync(CreateUpdateIssueBookDto input)
		{
			try
			{
				var issueBook = new IssueBooks.IssueBook(
					guidGenerator.Create(),
					input.Standard,
					input.Division,
					input.GrNumber,
					input.StudentName,
					input.BookId,
					input.BookTitle,
					input.EditionYear,
					input.Publisher,
					input.IssueDate,
					input.DueDate,
					hasPendingCharges: false, // Assuming it is set false initially
					tenantId: CurrentTenant.Id
				);

				await issueBookRepository.InsertAsync(issueBook, autoSave: true);
				return ObjectMapper.Map<IssueBooks.IssueBook, IssueBookDto>(issueBook);
			}
			catch (Exception ex)
			{
				throw new ApplicationException("An error occurred while issuing the book.", ex);
			}
		}

		public async Task<IssueBookDto> UpdateAsync(Guid id, CreateUpdateIssueBookDto input)
		{
			try
			{
				var issueBook = await issueBookRepository.FindAsync(id)
								 ?? throw new EntityNotFoundException(typeof(IssueBooks.IssueBook), id);

				ObjectMapper.Map(input, issueBook);
				await issueBookRepository.UpdateAsync(issueBook, autoSave: true);

				return ObjectMapper.Map<IssueBooks.IssueBook, IssueBookDto>(issueBook);
			}
			catch (Exception ex)
			{
				throw new ApplicationException("An error occurred while updating the issue record.", ex);
			}
		}

		public async Task DeleteAsync(Guid id)
		{
			try
			{
				var issueBook = await issueBookRepository.FindAsync(id)
								 ?? throw new EntityNotFoundException(typeof(IssueBooks.IssueBook), id);

				await issueBookRepository.DeleteAsync(issueBook);
			}
			catch (Exception ex)
			{
				throw new ApplicationException("An error occurred while deleting the issue record.", ex);
			}
		}

		public async Task<IssueBookDto> GetAsync(Guid id)
		{
			try
			{
				var issueBook = await issueBookRepository.GetAsync(id);
				return ObjectMapper.Map<IssueBooks.IssueBook, IssueBookDto>(issueBook);
			}
			catch (Exception ex)
			{
				throw new ApplicationException("An error occurred while retrieving the issue record.", ex);
			}
		}

        public async Task<PagedResultDto<IssueBookDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            try
            {
                var queryable = await issueBookRepository.GetQueryableAsync();
                if (CurrentTenant.Id.HasValue)
                    queryable = queryable.Where(x => x.TenantId == CurrentTenant.Id);

                var totalCount = await AsyncExecuter.CountAsync(queryable);

                // Explicitly specify the type arguments for OrderBy to resolve CS0411  
                var items = await AsyncExecuter.ToListAsync(
                    queryable.OrderBy<IssueBooks.IssueBook, DateTime>(x => x.IssueDate)
                             .Skip(input.SkipCount)
                             .Take(input.MaxResultCount)
                );

                var dtoList = ObjectMapper.Map<List<IssueBooks.IssueBook>, List<IssueBookDto>>(items);

                return new PagedResultDto<IssueBookDto>(totalCount, dtoList);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An error occurred while retrieving issued books.", ex);
            }
        }
   	}
}
