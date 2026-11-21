using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.Sections;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryReceivingLocationReader(IRepository<LibrarySection, Guid> sections,
	IRepository<LibraryShelf, Guid> shelves, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<List<LibraryCopyMoveSectionDto>> ReadAsync(Guid tenantId)
	{
		var sectionRows = await queries.ToListAsync((await sections.GetQueryableAsync())
			.Where(section => section.TenantId == tenantId).OrderBy(section => section.SectionName).ThenBy(section => section.Id)
			.Select(section => new { section.Id, section.SectionName }));
		var ids = sectionRows.Select(section => section.Id).ToArray();
		var shelfRows = await queries.ToListAsync((await shelves.GetQueryableAsync())
			.Where(shelf => Enumerable.Contains(ids, shelf.SectionId)).OrderBy(shelf => shelf.ShelfName).ThenBy(shelf => shelf.Id)
			.Select(shelf => new { shelf.Id, shelf.SectionId, shelf.ShelfName }));
		var grouped = shelfRows.GroupBy(shelf => shelf.SectionId).ToDictionary(group => group.Key, group => group.ToList());
		return sectionRows.Where(section => grouped.ContainsKey(section.Id)).Select(section => new LibraryCopyMoveSectionDto
		{
			Id = section.Id, SectionName = section.SectionName ?? string.Empty,
			ShelfDetails = grouped[section.Id].Select(shelf => new LibraryCopyMoveShelfDto
				{ Id = shelf.Id, ShelfName = shelf.ShelfName ?? string.Empty }).ToList()
		}).ToList();
	}
}
