using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;

[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BookCategories.View)]
[RemoteService(false)]
public class BookCategoryAppService(
	IRepository<BookCategory, Guid> _bookCategoryRepository,
	IRepository<Book, Guid> _bookRepository,
	ILibraryAccessService libraryAccess,
	ILibraryInventoryLock inventoryLock)
	: ApplicationService, IBookCategoryAppService
{
	public async Task<PaginatedResponse<BookCategoryDto>> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10)
	{
		await libraryAccess.GetContextAsync(academicYearId);
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, 100);
		var query = await _bookCategoryRepository.GetQueryableAsync();

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

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BookCategories.Add)]
	public async Task<GenericCreatedResponse> CreateAsync(Guid? academicYearId, BookCategoryCreateRequest input)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		if (!academicYearId.HasValue || academicYearId == Guid.Empty) throw LibraryException(LibraryErrorCodes.AcademicYearRequired, "Academic year context is required.");
		await libraryAccess.GetContextAsync(academicYearId.Value);
		var query = await _bookCategoryRepository.GetQueryableAsync();
		var normalizedName = input.CategoryName.Trim().ToLower();
		var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.Name.ToLower() == normalizedName));
		if (exists)
		{
			throw LibraryException(LibraryErrorCodes.BookCategoryAlreadyExists, "Category name already exists.");
		}

		var entity = new BookCategory(
			input.CategoryName.Trim(),
			input.Description?.Trim() ?? string.Empty,
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

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BookCategories.Add)]
	public async Task<GenericUpdatedResponse> UpdateAsync(Guid categoryId, BookCategoryUpdateRequest input)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var entity = await _bookCategoryRepository.GetAsync(categoryId);
		var query = await _bookCategoryRepository.GetQueryableAsync();
		var normalizedName = input.CategoryName.Trim().ToLower();
		var exists = await AsyncExecuter.AnyAsync(query.Where(x =>
			x.Id != categoryId &&
			x.Name.ToLower() == normalizedName));
		if (exists && !string.Equals(entity.Name.Trim(), input.CategoryName.Trim(), StringComparison.OrdinalIgnoreCase))
		{
			throw LibraryException(LibraryErrorCodes.BookCategoryAlreadyExists, "Category name already exists.");
		}

		entity.Name = input.CategoryName.Trim();
		entity.Description = input.Description?.Trim() ?? string.Empty;

		await _bookCategoryRepository.UpdateAsync(entity, autoSave: true);

		return new GenericUpdatedResponse
		{
			Id = entity.Id.ToString(),
			Message = "Book category updated successfully."
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BookCategories.Delete)]
	public async Task DeleteAsync(Guid categoryId)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		await _bookCategoryRepository.GetAsync(categoryId);
		var bookQuery = await _bookRepository.GetQueryableAsync();
		var isInUse = await AsyncExecuter.AnyAsync(bookQuery.Where(x => x.CategoryId == categoryId));
		if (isInUse)
		{
			throw LibraryException(LibraryErrorCodes.BookCategoryInUse, "Cannot delete a category that is used by books.");
		}

		await _bookCategoryRepository.DeleteAsync(categoryId, autoSave: true);
	}

	private static BusinessException LibraryException(string code, string message)
	{
		return new BusinessException(code: code, message: message);
	}
}
