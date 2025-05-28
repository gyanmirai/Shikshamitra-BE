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
   IRepository<BookCategory, Guid> bookCategoryRepository,
   IGuidGenerator guidGenerator
) : ApplicationService, IBookCategoryAppService
{
    public async Task<BookCategoryDto> CreateAsync(CreateUpdateBookCategoryDto createBookCategoryDto)
    {
        try
        {
            var bookCategoryEntity = new BookCategory(
                guidGenerator.Create(),
                createBookCategoryDto.Name,
                createBookCategoryDto.Description,
                CurrentTenant.Id ?? Guid.Empty
            );

            await bookCategoryRepository.InsertAsync(bookCategoryEntity, autoSave: true);
            return ObjectMapper.Map<BookCategory, BookCategoryDto>(bookCategoryEntity);
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while creating the book category.", ex);
        }
    }

    public async Task<BookCategoryDto> UpdateAsync(Guid id, CreateUpdateBookCategoryDto updateBookCategoryDto)
    {
        try
        {
            var bookCategoryEntity = await bookCategoryRepository.FindAsync(id) ?? throw new EntityNotFoundException(typeof(BookCategory), id);

            ObjectMapper.Map(updateBookCategoryDto, bookCategoryEntity);

            await bookCategoryRepository.UpdateAsync(bookCategoryEntity, autoSave: true);
            return ObjectMapper.Map<BookCategory, BookCategoryDto>(bookCategoryEntity);
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
            var bookCategoryEntity = await bookCategoryRepository.FindAsync(id) ?? throw new EntityNotFoundException(typeof(BookCategory), id);

            await bookCategoryRepository.DeleteAsync(bookCategoryEntity);
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
            var bookCategoryEntity = await bookCategoryRepository.GetAsync(id);
            return ObjectMapper.Map<BookCategory, BookCategoryDto>(bookCategoryEntity);
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while retrieving the book category.", ex);
        }
    }

    public async Task<PagedResultDto<BookCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto bookCategoryDto)
    {
        try
        {
            var tenantId = CurrentTenant.Id;
            var queryable = await bookCategoryRepository.GetQueryableAsync();

            if (tenantId.HasValue)
            {
                queryable = queryable.Where(x => x.TenantId == tenantId.Value);
            }

            var totalCount = await AsyncExecuter.CountAsync(queryable);

            var sortedQuery = queryable
                .OrderBy<BookCategory, string>(x => bookCategoryDto.Sorting == nameof(BookCategory.Name) ? x.Name : string.Empty)
                .Skip(bookCategoryDto.SkipCount)
                .Take(bookCategoryDto.MaxResultCount);

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
