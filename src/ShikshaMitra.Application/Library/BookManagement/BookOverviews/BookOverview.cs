using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.BookManagement.BookOverviews;

public class BookOverviewAppService(
	IRepository<BookOverview, Guid> bookOverviewRepository,
	IGuidGenerator guidGenerator)
	: ApplicationService, IBookOverviewAppService
{
	public async Task<BookOverviewDto> CreateAsync(CreateUpdateBookOverviewDto createBookOverviewDto)
	{
		var bookOverview = new BookOverview(
			guidGenerator.Create(),
			createBookOverviewDto.ISBN,
			createBookOverviewDto.Name,
			createBookOverviewDto.Borrowed,
			createBookOverviewDto.Overdue,
			createBookOverviewDto.Reserved,
			CurrentTenant.Id
		);

		await bookOverviewRepository.InsertAsync(bookOverview, autoSave: true);
		return ObjectMapper.Map<BookOverview, BookOverviewDto>(bookOverview);
	}

	public async Task DeleteAsync(Guid id)
	{
		await bookOverviewRepository.DeleteAsync(id);
	}

	public async Task<BookOverviewDto> GetAsync(Guid id)
	{
		var bookOverviewEntity = await bookOverviewRepository.GetAsync(id);
		return ObjectMapper.Map<BookOverview, BookOverviewDto>(bookOverviewEntity);
	}

        public async Task<PagedResultDto<BookOverviewDto>> GetListAsync(PagedAndSortedResultRequestDto bookOveriewDto)
        {
            var queryable = await bookOverviewRepository.GetQueryableAsync();

            if (CurrentTenant.Id.HasValue)
            {
                queryable = queryable.Where(b => b.TenantId == CurrentTenant.Id.Value);
            }

            var totalCount = await AsyncExecuter.CountAsync(queryable);

            // Fix: Use a lambda expression to create the required Expression<Func<T, TResult>>  
            var entities = await AsyncExecuter.ToListAsync(
                queryable
                    .OrderBy(bookOveriewDto.Sorting ?? nameof(BookOverview.Name))
                    .Skip(bookOveriewDto.SkipCount)
                    .Take(bookOveriewDto.MaxResultCount)
            );

            var dtoList = ObjectMapper.Map<List<BookOverview>, List<BookOverviewDto>>(entities);
            return new PagedResultDto<BookOverviewDto>(totalCount, dtoList);
        }

    
	public async Task<BookOverviewDto> UpdateAsync(Guid id, CreateUpdateBookOverviewDto updateBookOverviewDto)
	{
		var bookOverviewEntity = await bookOverviewRepository.FindAsync(id);
		if (bookOverviewEntity == null)
		{
			throw new EntityNotFoundException(typeof(BookOverview), id);
		}

		ObjectMapper.Map(updateBookOverviewDto, bookOverviewEntity);
		await bookOverviewRepository.UpdateAsync(bookOverviewEntity, autoSave: true);

		return ObjectMapper.Map<BookOverview, BookOverviewDto>(bookOverviewEntity);
	}
}