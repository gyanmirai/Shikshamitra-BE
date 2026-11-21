using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.CampusTransfers;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Setting.Sections.Dtos;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library.Setting.Sections;

[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_SectionManagement.View)]
[RemoteService(false)]
public partial class LibrarySectionAppService(
	IRepository<LibrarySection, Guid> _sectionRepository,
	IRepository<LibraryShelf, Guid> _shelfRepository,
	IRepository<BookCopy, Guid> _bookCopyRepository,
	IUnitOfWorkManager _unitOfWorkManager,
	ILibraryAccessService libraryAccess,
	ILibraryInventoryLock inventoryLock,
	IRepository<AdmissionCampus, Guid> campuses,
	IRepository<LibraryCampusTransfer, Guid> transfers,
	IRepository<LibraryCampusTransferItem, Guid> transferItems,
	IRepository<BookReservation, Guid> reservations,
	IPermissionChecker permissions)
	: ApplicationService, ILibrarySectionAppService
{
	public async Task<PaginatedSectionResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10)
	{
		await libraryAccess.GetContextAsync(academicYearId);
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, 100);
		var query = await _sectionRepository.GetQueryableAsync();

		var totalCount = await AsyncExecuter.CountAsync(query);
		var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.CreationTime)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
		);
		var campusNames = await GetCampusNamesAsync(items);

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
				CampusId = x.CampusId,
				CampusName = x.CampusId is Guid campusId ? campusNames.GetValueOrDefault(campusId) : null,
				ShelfDetails = x.Shelves.Select(s => new ShelfDetailDto { ShelfName = s.ShelfName,Id=s.Id }).ToList()
			}).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_SectionManagement.Add)]
	public async Task<GenericCreatedResponse> CreateAsync(Guid academicYearId, SectionCreateRequest input)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		await libraryAccess.GetContextAsync(academicYearId);
		ValidateShelfPayload(input.SectionName, input.NumberOfShelf, input.ShelfItems);

		var query = await _sectionRepository.GetQueryableAsync();
		var normalizedSectionName = input.SectionName.Trim().ToLower();
		var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.SectionName!.ToLower() == normalizedSectionName));
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
		return await MapDetailsAsync(section);
	}

	private async Task<SectionDetailsResponse> MapDetailsAsync(LibrarySection section)
	{
		var campusNames = await GetCampusNamesAsync([section]);
		var shelfQuery = await _shelfRepository.GetQueryableAsync();
		var shelves = await AsyncExecuter.ToListAsync(
			shelfQuery.Where(x => x.SectionId == section.Id).OrderBy(x => x.ShelfName));

		return new SectionDetailsResponse
		{
			Id = section.Id.ToString(),
			SectionName = section.SectionName,
			NumberOfShelf = section.NumberOfShelf,
			CampusId = section.CampusId,
			CampusName = section.CampusId is Guid campusId ? campusNames.GetValueOrDefault(campusId) : null,
			ShelfDetails = shelves.Select(s => new ShelfDetailDto { ShelfName = s.ShelfName, Id = s.Id }).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_SectionManagement.Add)]
	public async Task<GenericUpdatedResponse> UpdateAsync(Guid sectionId, Guid academicYearId, SectionUpdateRequest input)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		ValidateShelfPayload(input.SectionName, input.NumberOfShelf, input.ShelfItems);
		var section = await GetSectionAsync(sectionId, academicYearId);
		var sectionQuery = await _sectionRepository.GetQueryableAsync();
		var normalizedSectionName = input.SectionName.Trim().ToLower();
		var exists = await AsyncExecuter.AnyAsync(sectionQuery.Where(x =>
			x.Id != sectionId &&
			x.SectionName!.ToLower() == normalizedSectionName));
		if (exists && !string.Equals(section.SectionName?.Trim(), input.SectionName.Trim(), StringComparison.OrdinalIgnoreCase))
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
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		await GetSectionAsync(sectionId, academicYearId);
		await EnsureNoIncomingTransferAsync(sectionId);
		var activePickup = await reservations.AnyAsync(row => row.TenantId == CurrentTenant.Id && row.PickupSectionId == sectionId &&
			(row.State == LibraryReservationState.Waiting || row.State == LibraryReservationState.Ready || row.State == LibraryReservationState.NeedsReview));
		if (activePickup) throw LibraryException(LibraryErrorCodes.SectionInUse, "Resolve the active pickup reservations before deleting this section.");
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
		await libraryAccess.GetContextAsync(academicYearId);
		var query = await _sectionRepository.GetQueryableAsync();
		var section = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == sectionId));
		if (section == null)
		{
			throw LibraryException(LibraryErrorCodes.SectionNotFound, "Library section was not found.");
		}

		return section;
	}

	private async Task EnsureShelvesCanBeReplacedAsync(Guid sectionId)
	{
		await EnsureNoIncomingTransferAsync(sectionId);
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

	private async Task EnsureNoIncomingTransferAsync(Guid sectionId)
	{
		var tenant = CurrentTenant.Id;
		var headers = await transfers.GetQueryableAsync();
		var lines = await transferItems.GetQueryableAsync();
		var incoming = from header in headers
			join line in lines on header.Id equals line.TransferId
			where header.TenantId == tenant && line.TenantId == tenant && header.DestinationSectionId == sectionId &&
				!line.ReceiptId.HasValue
			select line.Id;
		if (await AsyncExecuter.AnyAsync(incoming))
			throw LibraryException(LibraryErrorCodes.SectionInUse, "Receive the outstanding campus transfer before replacing or deleting its destination shelves.");
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
