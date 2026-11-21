using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Catalogue;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryStockImportLocations(IRepository<LibrarySection, Guid> sections, IRepository<LibraryShelf, Guid> shelves,
	IRepository<BookCategory, Guid> categories, IRepository<AcademicYear, Guid> years, IAsyncQueryableExecuter queries,
	LibraryCatalogueConfigurationReader catalogue) : ITransientDependency
{
	internal async Task<LibraryStockImportOptionsDto> ReadAsync(Guid tenant, Guid year, bool legacy, bool createTitles, Guid[] allowedYears)
	{
		var availableSections = await queries.ToListAsync((await SectionsAsync(tenant, year, legacy))
			.OrderBy(section => section.SectionName).ThenBy(section => section.Id)
			.Select(section => new { section.Id, section.SectionName }));
		var ids = availableSections.Select(section => section.Id).ToArray();
		var positions = await queries.ToListAsync((await shelves.GetQueryableAsync())
			.Where(shelf => Enumerable.Contains(ids, shelf.SectionId)).OrderBy(shelf => shelf.ShelfName).ThenBy(shelf => shelf.Id)
			.Select(shelf => new { shelf.Id, shelf.SectionId, shelf.ShelfName }));
		var bySection = positions.ToLookup(shelf => shelf.SectionId);
		var locations = availableSections.Where(section => bySection.Contains(section.Id)).Select(section => new LibraryCopyMoveSectionDto
		{
			Id = section.Id,
			SectionName = section.SectionName ?? string.Empty,
			ShelfDetails = bySection[section.Id].Select(shelf => new LibraryCopyMoveShelfDto
				{ Id = shelf.Id, ShelfName = shelf.ShelfName ?? string.Empty }).ToList()
		}).ToList();
		var options = !createTitles ? [] : await queries.ToListAsync((await CategoriesAsync(tenant, year, legacy))
			.OrderBy(row => row.Name).ThenBy(row => row.Id).Select(row => new LibraryStockImportCategoryDto(row.Id, row.Name)));
		var allowed = await queries.ToListAsync((await years.GetQueryableAsync())
			.Where(row => row.TenantId == tenant && Enumerable.Contains(allowedYears, row.Id))
			.OrderByDescending(row => row.StartDate).ThenBy(row => row.Id)
			.Select(row => new LibraryStockImportYearDto(row.Id, row.Name)));
		return new(createTitles, locations, options, allowed, createTitles ? await catalogue.ReadAsync() : null);
	}

	internal async Task<(string Section, string Shelf, string? Revision)?> FindAsync(Guid tenant, LibraryStockImportInput input, bool legacy)
	{
		var section = await queries.FirstOrDefaultAsync((await SectionsAsync(tenant, input.AcademicYearId, legacy))
			.Where(row => row.Id == input.SectionId).Select(row => new { row.SectionName, row.ConcurrencyStamp }));
		if (section == null) return null;
		var position = await queries.FirstOrDefaultAsync((await shelves.GetQueryableAsync())
			.Where(row => row.SectionId == input.SectionId && row.Id == input.ShelfId).Select(row => new { row.ShelfName }));
		return position == null ? null : (section.SectionName ?? string.Empty, position.ShelfName ?? string.Empty, section.ConcurrencyStamp);
	}

	internal async Task<LibraryStockImportCategoryDto?> FindCategoryAsync(Guid tenant, Guid year, bool legacy, Guid? categoryId) =>
		categoryId.HasValue ? await queries.FirstOrDefaultAsync((await CategoriesAsync(tenant, year, legacy))
			.Where(row => row.Id == categoryId).Select(row => new LibraryStockImportCategoryDto(row.Id, row.Name))) : null;

	private async Task<IQueryable<LibrarySection>> SectionsAsync(Guid tenant, Guid year, bool legacy) =>
		(await sections.GetQueryableAsync()).Where(row => row.TenantId == tenant &&
			(row.AcademicYearId == year || legacy && row.AcademicYearId == null));

	private async Task<IQueryable<BookCategory>> CategoriesAsync(Guid tenant, Guid year, bool legacy) =>
		(await categories.GetQueryableAsync()).Where(row => row.TenantId == tenant &&
			(row.AcademicYearId == year || legacy && row.AcademicYearId == null));
}
