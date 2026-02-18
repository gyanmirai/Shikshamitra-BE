using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;

[RemoteService(false)]
public class BookCategoryAppService(IRepository<BookCategory, Guid> _bookCategoryRepository)
	: ApplicationService, IBookCategoryAppService
{
	public async Task<PaginatedResponse<BookCategoryDto>> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10)
	{
		var query = await _bookCategoryRepository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == academicYearId);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.CreationTime)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
		);

		return new PaginatedResponse<BookCategoryDto>
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = ObjectMapper.Map<List<BookCategory>, List<BookCategoryDto>>(items)
		};
	}

	public async Task<BookCategoryDto> GetAsync(Guid categoryId)
	{
		var entity = await _bookCategoryRepository.GetAsync(categoryId);
		return ObjectMapper.Map<BookCategory, BookCategoryDto>(entity);
	}

	public async Task<GenericCreatedResponse> CreateAsync(Guid? academicYearId, BookCategoryCreateRequest input)
	{
		var query = await _bookCategoryRepository.GetQueryableAsync();
		var exists = query.Any(x => x.Name == input.CategoryName && x.AcademicYearId == academicYearId);
		if (exists)
		{
			throw new UserFriendlyException("Category name already exists.", code: "409");
		}

		var entity = new BookCategory(
			input.CategoryName,
			input.Description,
			academicYearId,
			CurrentTenant.Id
		);

		await _bookCategoryRepository.InsertAsync(entity, autoSave: true);

		return new GenericCreatedResponse
		{
			Id = entity.Id.ToString(),
			Message = "Book category created successfully."
		};
	}

	public async Task<GenericUpdatedResponse> UpdateAsync(Guid categoryId, BookCategoryUpdateRequest input)
	{
		var entity = await _bookCategoryRepository.GetAsync(categoryId);

		entity.Name = input.CategoryName;
		entity.Description = input.Description;

		await _bookCategoryRepository.UpdateAsync(entity, autoSave: true);

		return new GenericUpdatedResponse
		{
			Id = entity.Id.ToString(),
			Message = "Book category updated successfully."
		};
	}

	public async Task DeleteAsync(Guid categoryId)
	{
		await _bookCategoryRepository.DeleteAsync(categoryId, autoSave: true);
	}
}
