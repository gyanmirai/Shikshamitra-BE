using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Setting.Sections.Dtos;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library.Setting.Sections;

[RemoteService(false)]
public class LibrarySectionAppService(
	IRepository<LibrarySection, Guid> _sectionRepository,
	IRepository<LibraryShelf, Guid> _shelfRepository,
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

	public async Task<GenericCreatedResponse> CreateAsync(Guid academicYearId, SectionCreateRequest input)
	{
		var query = await _sectionRepository.GetQueryableAsync();
		var exists = query.Any(x => x.SectionName == input.SectionName && x.AcademicYearId == academicYearId);
		if (exists)
		{
			throw new UserFriendlyException("Section already exists.", code: "409");
		}

		var section = new LibrarySection(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.SectionName,
			input.NumberOfShelf
		);

		await _sectionRepository.InsertAsync(section, autoSave: true);

		// Insert shelves
		foreach (var shelfName in input.ShelfItems)
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
		var section = await _sectionRepository.GetAsync(sectionId);

		var shelfQuery = await _shelfRepository.GetQueryableAsync();
		

		return new SectionDetailsResponse
		{
			Id = section.Id.ToString(),
			SectionName = section.SectionName,
			NumberOfShelf = section.NumberOfShelf,
			ShelfDetails = section?.Shelves?.Select(s => new ShelfDetailDto { ShelfName = s.ShelfName, Id = s.Id }).ToList()
		};
	}

	public async Task<GenericUpdatedResponse> UpdateAsync(Guid sectionId, Guid academicYearId, SectionUpdateRequest input)
	{
		var section = await _sectionRepository.GetAsync(sectionId);

		section.SectionName = input.SectionName;
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

		foreach (var shelfName in input.ShelfItems)
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

	public async Task DeleteAsync(Guid sectionId, Guid academicYearId)
	{
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
}
