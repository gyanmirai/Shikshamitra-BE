using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
public class BookCategoryAppService(IRepository<BookCategory, Guid> _bookCategoryRepository)
	: ApplicationService, IBookCategoryAppService
{
	public async Task<BookCategoryDto> CreateAsync(CreateUpdateBookCategoryDto input)
	{
		try
		{
			var entity = ObjectMapper.Map<CreateUpdateBookCategoryDto, BookCategory>(input);
			await _bookCategoryRepository.InsertAsync(entity, autoSave: true);
			return ObjectMapper.Map<BookCategory, BookCategoryDto>(entity);
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
			var entity = await _bookCategoryRepository.GetAsync(id);
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
			await _bookCategoryRepository.DeleteAsync(id);
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
			var queryable = await _bookCategoryRepository.GetQueryableAsync();

			var totalCount = await AsyncExecuter.CountAsync(queryable);

			var items = await AsyncExecuter.ToListAsync(
				queryable
					.Skip(input.SkipCount)
					.Take(input.MaxResultCount)
			);

			var dtoList = ObjectMapper.Map<List<BookCategory>, List<BookCategoryDto>>(items);
			return new PagedResultDto<BookCategoryDto>(totalCount, dtoList);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while retrieving book categories.", ex);
		}
	}
}
