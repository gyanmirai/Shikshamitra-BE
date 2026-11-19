using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.Sections.Dtos;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library.Setting.Sections;

[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_SectionManagement.View)]
[RemoteService(false)]
public class LibrarySectionAppService(
	IRepository<LibrarySection, Guid> _sectionRepository,
	IRepository<LibraryShelf, Guid> _shelfRepository,
	IRepository<BookCopy, Guid> _bookCopyRepository,
	IUnitOfWorkManager _unitOfWorkManager)
	: ApplicationService, ILibrarySectionAppService
{
	public async Task<PaginatedSectionResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10)
	{
		var query = await _sectionRepository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == academicYearId);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.CreationTime)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
		);

		return new PaginatedSectionResponse
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = items.Select(x => new SectionDto
			{
				Id = x.Id,
				SectionName = x.SectionName,
				NumberOfShelf = x.NumberOfShelf,
				ShelfDetails = x.Shelves.Select(s => new ShelfDetailDto { ShelfName = s.ShelfName,Id=s.Id }).ToList()
			}).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_SectionManagement.Add)]
	public async Task<GenericCreatedResponse> CreateAsync(Guid academicYearId, SectionCreateRequest input)
	{
		ValidateShelfPayload(input.SectionName, input.NumberOfShelf, input.ShelfItems);

		var query = await _sectionRepository.GetQueryableAsync();
		var normalizedSectionName = input.SectionName.Trim().ToLower();
		var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.SectionName!.ToLower() == normalizedSectionName && x.AcademicYearId == academicYearId));
		if (exists)
		{
			throw LibraryException(LibraryErrorCodes.SectionAlreadyExists, "Section already exists.");
		}

		var section = new LibrarySection(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.SectionName.Trim(),
			input.NumberOfShelf
		);

		await _sectionRepository.InsertAsync(section, autoSave: true);

		// Insert shelves
		foreach (var shelfName in input.ShelfItems.Select(x => x.Trim()))
		{
			var shelf = new LibraryShelf(GuidGenerator.Create(), section.Id, shelfName);
			await _shelfRepository.InsertAsync(shelf);
		}

		await _unitOfWorkManager.Current!.SaveChangesAsync();

		return new GenericCreatedResponse
		{
			Id = section.Id.ToString(),
			Message = "Section created successfully."
		};
	}

	public async Task<SectionDetailsResponse> GetAsync(Guid sectionId, Guid academicYearId)
	{
		var section = await GetSectionAsync(sectionId, academicYearId);
		var shelfQuery = await _shelfRepository.GetQueryableAsync();
		var shelves = await AsyncExecuter.ToListAsync(
			shelfQuery.Where(x => x.SectionId == sectionId).OrderBy(x => x.ShelfName));

		return new SectionDetailsResponse
		{
			Id = section.Id.ToString(),
			SectionName = section.SectionName,
			NumberOfShelf = section.NumberOfShelf,
			ShelfDetails = shelves.Select(s => new ShelfDetailDto { ShelfName = s.ShelfName, Id = s.Id }).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_SectionManagement.Add)]
	public async Task<GenericUpdatedResponse> UpdateAsync(Guid sectionId, Guid academicYearId, SectionUpdateRequest input)
	{
		ValidateShelfPayload(input.SectionName, input.NumberOfShelf, input.ShelfItems);
		var section = await GetSectionAsync(sectionId, academicYearId);
		var sectionQuery = await _sectionRepository.GetQueryableAsync();
		var normalizedSectionName = input.SectionName.Trim().ToLower();
		var exists = await AsyncExecuter.AnyAsync(sectionQuery.Where(x =>
			x.Id != sectionId &&
			x.AcademicYearId == academicYearId &&
			x.SectionName!.ToLower() == normalizedSectionName));
		if (exists)
		{
			throw LibraryException(LibraryErrorCodes.SectionAlreadyExists, "Section already exists.");
		}

		await EnsureShelvesCanBeReplacedAsync(sectionId);

		section.SectionName = input.SectionName.Trim();
		section.NumberOfShelf = input.NumberOfShelf;

		await _sectionRepository.UpdateAsync(section, autoSave: true);

		// Replace shelves
		var shelfQuery = await _shelfRepository.GetQueryableAsync();
		var existingShelves = await AsyncExecuter.ToListAsync(
			shelfQuery.Where(x => x.SectionId == sectionId)
		);

		foreach (var shelf in existingShelves)
		{
			await _shelfRepository.DeleteAsync(shelf);
		}

		foreach (var shelfName in input.ShelfItems.Select(x => x.Trim()))
		{
			var shelf = new LibraryShelf(GuidGenerator.Create(), sectionId, shelfName);
			await _shelfRepository.InsertAsync(shelf);
		}

		await _unitOfWorkManager.Current!.SaveChangesAsync();

		return new GenericUpdatedResponse
		{
			Id = section.Id.ToString(),
			Message = "Section updated successfully."
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_SectionManagement.Delete)]
	public async Task DeleteAsync(Guid sectionId, Guid academicYearId)
	{
		await GetSectionAsync(sectionId, academicYearId);
		var bookCopyQuery = await _bookCopyRepository.GetQueryableAsync();
		var isInUse = await AsyncExecuter.AnyAsync(bookCopyQuery.Where(x => x.SectionId == sectionId));
		if (isInUse)
		{
			throw LibraryException(LibraryErrorCodes.SectionInUse, "Cannot delete a section that contains book copies.");
		}

		var shelfQuery = await _shelfRepository.GetQueryableAsync();
		var shelves = await AsyncExecuter.ToListAsync(
			shelfQuery.Where(x => x.SectionId == sectionId)
		);

		foreach (var shelf in shelves)
		{
			await _shelfRepository.DeleteAsync(shelf);
		}

		await _sectionRepository.DeleteAsync(sectionId, autoSave: true);
	}

	private async Task<LibrarySection> GetSectionAsync(Guid sectionId, Guid academicYearId)
	{
		var query = await _sectionRepository.GetQueryableAsync();
		var section = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == sectionId && x.AcademicYearId == academicYearId));
		if (section == null)
		{
			throw LibraryException(LibraryErrorCodes.SectionNotFound, "Library section was not found.");
		}

		return section;
	}

	private async Task EnsureShelvesCanBeReplacedAsync(Guid sectionId)
	{
		var existingShelfQuery = await _shelfRepository.GetQueryableAsync();
		var existingShelves = await AsyncExecuter.ToListAsync(existingShelfQuery.Where(x => x.SectionId == sectionId));
		var existingShelfIds = existingShelves.Select(x => x.Id).ToList();

		if (existingShelfIds.Count == 0)
		{
			return;
		}

		var bookCopyQuery = await _bookCopyRepository.GetQueryableAsync();
		var shelfInUse = await AsyncExecuter.AnyAsync(bookCopyQuery.Where(x => existingShelfIds.Contains(x.ShelfId)));
		if (shelfInUse)
		{
			throw LibraryException(LibraryErrorCodes.SectionInUse, "Cannot replace shelves while they contain book copies.");
		}
	}

	private static void ValidateShelfPayload([System.Diagnostics.CodeAnalysis.NotNull] string? sectionName, int numberOfShelf, IReadOnlyCollection<string> shelfItems)
	{
		if (string.IsNullOrWhiteSpace(sectionName))
		{
			throw LibraryException(LibraryErrorCodes.SectionNotFound, "Section name is required.");
		}

		if (numberOfShelf <= 0 || shelfItems.Count != numberOfShelf)
		{
			throw LibraryException(LibraryErrorCodes.InvalidShelfConfiguration, "Number of shelves must match the shelf payload.");
		}

		if (shelfItems.Any(string.IsNullOrWhiteSpace))
		{
			throw LibraryException(LibraryErrorCodes.InvalidShelfConfiguration, "Shelf names are required.");
		}

		var duplicateShelf = shelfItems
			.GroupBy(x => x.Trim(), StringComparer.OrdinalIgnoreCase)
			.FirstOrDefault(x => x.Count() > 1);

		if (duplicateShelf != null)
		{
			throw LibraryException(LibraryErrorCodes.InvalidShelfConfiguration, $"Duplicate shelf name '{duplicateShelf.Key}'.");
		}
	}

	private static BusinessException LibraryException(string code, string message)
	{
		return new BusinessException(code: code, message: message);
	}
}
