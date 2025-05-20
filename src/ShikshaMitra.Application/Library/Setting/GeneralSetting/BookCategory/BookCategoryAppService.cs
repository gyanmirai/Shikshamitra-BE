using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
public class BookCategoryAppService(
	IRepository<BookCategory, Guid> _bookCategoryRepository,
	IGuidGenerator guidGenerator
) : ApplicationService, IBookCategoryAppService
{
    
public async Task<BookCategoryDto> CreateAsync(CreateUpdateBookCategoryDto input)
    {
        try
        {
            // Use the constructor to set the Id since the property setter is inaccessible  
            var BookCategoryentity = new BookCategory(
                guidGenerator.Create(), // Pass the required 'Id' parameter  
                input.Name,             // Pass the required 'Name' parameter  
                input.Description,      // Pass the required 'Description' parameter  
                CurrentTenant.Id ?? Guid.Empty // Pass the required 'TenantId' parameter  
            );

            await _bookCategoryRepository.InsertAsync(BookCategoryentity, autoSave: true);
            return ObjectMapper.Map<BookCategory, BookCategoryDto>(BookCategoryentity);
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while creating the book category.", ex);
        }
    }
    public async Task<BookCategoryDto> UpdateAsync(Guid id, CreateUpdateBookCategoryDto input)
	{
		try
		{
			var entity = await _bookCategoryRepository.FindAsync(id);
			if (entity == null)
			{
				throw new EntityNotFoundException(typeof(BookCategory), id);
			}

			ObjectMapper.Map(input, entity);

			await _bookCategoryRepository.UpdateAsync(entity, autoSave: true);
			return ObjectMapper.Map<BookCategory, BookCategoryDto>(entity);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while updating the book category.", ex);
		}
	}

	public async Task DeleteAsync(Guid id)
	{
		try
		{
			var entity = await _bookCategoryRepository.FindAsync(id);
			if (entity == null)
			{
				throw new EntityNotFoundException(typeof(BookCategory), id);
			}

			await _bookCategoryRepository.DeleteAsync(entity);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while deleting the book category.", ex);
		}
	}

	public async Task<BookCategoryDto> GetAsync(Guid id)
	{
		try
		{
			var entity = await _bookCategoryRepository.GetAsync(id);
			return ObjectMapper.Map<BookCategory, BookCategoryDto>(entity);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while retrieving the book category.", ex);
		}
	}

    public async Task<PagedResultDto<BookCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        try
        {
            var tenantId = CurrentTenant.Id;
            var queryable = await _bookCategoryRepository.GetQueryableAsync();

            if (tenantId.HasValue)
            {
                queryable = queryable.Where(x => x.TenantId == tenantId.Value);
            }

            var totalCount = await AsyncExecuter.CountAsync(queryable);

            // Fix: Explicitly specify the type arguments for OrderBy  
            var sortedQuery = queryable
                .OrderBy<BookCategory, string>(x => input.Sorting == nameof(BookCategory.Name) ? x.Name : string.Empty)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount);

            var items = await AsyncExecuter.ToListAsync(sortedQuery);
            var dtoList = ObjectMapper.Map<List<BookCategory>, List<BookCategoryDto>>(items);

            return new PagedResultDto<BookCategoryDto>(totalCount, dtoList);
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while retrieving book categories.", ex);
        }
    }
}