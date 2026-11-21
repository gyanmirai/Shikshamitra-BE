using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Library.Books;

public partial class BookAppService
{
	public async Task<LibraryBookEditorOptionsDto> GetEditorOptionsAsync(Guid academicYearId)
	{
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View) ||
			(!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Add) &&
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update)))
			throw new AbpAuthorizationException();
		await libraryAccess.GetContextAsync(academicYearId);

		var categories = await AsyncExecuter.ToListAsync((await categoryRepository.GetQueryableAsync())
			.Where(category => category.TenantId == CurrentTenant.Id)
			.OrderBy(category => category.Name).ThenBy(category => category.Id)
			.Select(category => new NamedIdDto { Id = category.Id, Name = category.Name }));
		var sections = await AsyncExecuter.ToListAsync((await sectionRepository.GetQueryableAsync())
			.Where(section => section.TenantId == CurrentTenant.Id)
			.OrderBy(section => section.SectionName).ThenBy(section => section.Id)
			.Select(section => new { section.Id, section.SectionName }));
		var sectionIds = sections.Select(section => section.Id).ToArray();
		var shelves = await AsyncExecuter.ToListAsync((await shelfRepository.GetQueryableAsync())
			.Where(shelf => Enumerable.Contains(sectionIds, shelf.SectionId))
			.OrderBy(shelf => shelf.ShelfName).ThenBy(shelf => shelf.Id)
			.Select(shelf => new { shelf.Id, shelf.SectionId, shelf.ShelfName }));
		var shelvesBySection = shelves.ToLookup(shelf => shelf.SectionId);
		return new()
		{
			CatalogueConfiguration = await catalogueConfiguration.ReadAsync(),
			BookCategories = categories,
			Sections = sections.Select(section => new LibraryCopyMoveSectionDto
			{
				Id = section.Id, SectionName = section.SectionName ?? string.Empty,
				ShelfDetails = shelvesBySection[section.Id].Select(shelf => new LibraryCopyMoveShelfDto
				{
					Id = shelf.Id, ShelfName = shelf.ShelfName ?? string.Empty
				}).ToList()
			}).ToList()
		};
	}
}
