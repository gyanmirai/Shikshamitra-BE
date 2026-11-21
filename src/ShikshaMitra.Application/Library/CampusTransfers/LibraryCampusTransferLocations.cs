using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library.CampusTransfers;

public class LibraryCampusTransferLocations(IRepository<AdmissionCampus, Guid> campuses, IRepository<LibrarySection, Guid> sections,
	IRepository<LibraryShelf, Guid> shelves, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<List<LibraryCampusTransferSection>> ListAsync(Guid tenant)
	{
		var locations = await queries.ToListAsync((await ActiveSectionsAsync(tenant))
			.OrderBy(row => row.Campus).ThenBy(row => row.Section).ThenBy(row => row.SectionId));
		var ids = locations.Select(row => row.SectionId).ToArray();
		var shelfRows = (await queries.ToListAsync((await shelves.GetQueryableAsync()).Where(row =>
			Enumerable.Contains(ids, row.SectionId)).OrderBy(row => row.ShelfName).ThenBy(row => row.Id)
			.Select(row => new { row.Id, row.ShelfName, row.SectionId }))).ToLookup(row => row.SectionId);
		return locations.Select(row => new LibraryCampusTransferSection(Map(row),
			shelfRows[row.SectionId].Select(shelf => new LibraryCampusTransferShelf(shelf.Id, shelf.ShelfName ?? "")).ToList()))
			.Where(row => row.Shelves.Count != 0).ToList();
	}

	internal async Task<bool> CanDispatchFromAsync(Guid tenant, Guid section) =>
		await queries.AnyAsync((await ActiveSectionsAsync(tenant)).Where(row => row.SectionId == section));

	internal async Task<LibraryCampusTransferRoute?> ForDispatchAsync(Guid tenant, LibraryCampusDispatchInput input)
	{
		var locations = await queries.ToListAsync((await ActiveSectionsAsync(tenant)).Where(row =>
			row.SectionId == input.SourceSectionId || row.SectionId == input.DestinationSectionId));
		var source = locations.SingleOrDefault(row => row.SectionId == input.SourceSectionId);
		var destination = locations.SingleOrDefault(row => row.SectionId == input.DestinationSectionId);
		if (source == null || destination == null || source.CampusId == destination.CampusId) return null;
		var shelf = await shelves.FindAsync(row => row.Id == input.DestinationShelfId && row.SectionId == destination.SectionId);
		return shelf == null ? null : new(source, destination, shelf.Id, shelf.ShelfName ?? "");
	}

	internal async Task<bool> CanReceiveAsync(Guid tenant, LibraryCampusTransfer transfer) =>
		await queries.AnyAsync((await sections.GetQueryableAsync()).Where(row => row.TenantId == tenant &&
			row.Id == transfer.DestinationSectionId && row.CampusId == transfer.DestinationCampusId)) &&
		await queries.AnyAsync((await shelves.GetQueryableAsync()).Where(row =>
			row.Id == transfer.DestinationShelfId && row.SectionId == transfer.DestinationSectionId));

	private async Task<IQueryable<LocationRow>> ActiveSectionsAsync(Guid tenant)
	{
		var sectionRows = await sections.GetQueryableAsync();
		var campusRows = await campuses.GetQueryableAsync();
		return from section in sectionRows
			join campus in campusRows on section.CampusId equals (Guid?)campus.Id
			where section.TenantId == tenant && campus.TenantId == tenant && campus.IsActive
			select new LocationRow { CampusId = campus.Id, Campus = campus.Name, SectionId = section.Id, Section = section.SectionName ?? "" };
	}

	private static LibraryCampusTransferScope Map(LocationRow row) => new(row.CampusId, row.Campus, row.SectionId, row.Section);

	internal sealed class LocationRow
	{
		public Guid CampusId { get; init; }
		public string Campus { get; init; } = string.Empty;
		public Guid SectionId { get; init; }
		public string Section { get; init; } = string.Empty;
		public LibraryCampusTransferLocation Snapshot() => new(CampusId, Campus, SectionId, Section);
	}

	internal sealed record LibraryCampusTransferRoute(LocationRow Source, LocationRow Destination, Guid ShelfId, string Shelf);
}
