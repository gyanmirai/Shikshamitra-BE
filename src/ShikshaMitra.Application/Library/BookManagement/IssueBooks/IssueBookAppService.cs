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
		public async Task<IssueBookDto> CreateAsync(CreateUpdateIssueBookDto createIssueBookDto)
		{
			try
			{
				var issueBook = new IssueBooks.IssueBook(
					guidGenerator.Create(),
					createIssueBookDto.Standard,
					createIssueBookDto.Division,
					createIssueBookDto.GrNumber,
					createIssueBookDto.StudentName,
					createIssueBookDto.BookId,
					createIssueBookDto.BookTitle,
					createIssueBookDto.EditionYear,
					createIssueBookDto.Publisher,
					createIssueBookDto.IssueDate,
					createIssueBookDto.DueDate,
					hasPendingCharges: false,
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

		public async Task<IssueBookDto> UpdateAsync(Guid id, CreateUpdateIssueBookDto updateIssueBookDto)
		{
			try
			{
				var issueBook = await issueBookRepository.FindAsync(id)
								 ?? throw new EntityNotFoundException(typeof(IssueBooks.IssueBook), id);

				ObjectMapper.Map(updateIssueBookDto, issueBook);
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

        public async Task<PagedResultDto<IssueBookDto>> GetListAsync(PagedAndSortedResultRequestDto issueBookDto)
        {
            try
            {
                var queryable = await issueBookRepository.GetQueryableAsync();
                if (CurrentTenant.Id.HasValue)
                    queryable = queryable.Where(x => x.TenantId == CurrentTenant.Id);

                var totalCount = await AsyncExecuter.CountAsync(queryable);
 
                var items = await AsyncExecuter.ToListAsync(
                    queryable.OrderBy<IssueBooks.IssueBook, DateTime>(x => x.IssueDate)
                             .Skip(issueBookDto.SkipCount)
                             .Take(issueBookDto.MaxResultCount)
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
