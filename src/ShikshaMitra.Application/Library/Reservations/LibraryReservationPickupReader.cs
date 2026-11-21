using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library.Reservations;

public class LibraryReservationPickupReader(IRepository<LibrarySection, Guid> sections,
	IRepository<AdmissionCampus, Guid> campuses, IAsyncQueryableExecuter queries) : ITransientDependency
{
	public async Task<LibraryReservationPickup?> FindAsync(Guid tenant, Guid id)
	{
		var row = await queries.FirstOrDefaultAsync((await QueryAsync(tenant)).Where(row => row.SectionId == id));
		return row == null ? null : new(row.SectionId, row.CampusId, row.Label);
	}

	public async Task<List<LibraryReservationPickupDto>> GetOptionsAsync(Guid tenant)
	{
		var rows = await queries.ToListAsync((await QueryAsync(tenant)).OrderBy(row => row.Label).ThenBy(row => row.SectionId));
		return rows.Select(row => new LibraryReservationPickupDto(row.SectionId, row.CampusId, row.Label)).ToList();
	}

	private async Task<IQueryable<Location>> QueryAsync(Guid tenant)
	{
		var siteSections = await sections.GetQueryableAsync();
		var schoolCampuses = await campuses.GetQueryableAsync();
		return from section in siteSections
			join campus in schoolCampuses on section.CampusId equals (Guid?)campus.Id
			where section.TenantId == tenant && campus.TenantId == tenant && campus.IsActive
			select new Location { SectionId = section.Id, CampusId = campus.Id, Label = campus.Name + " / " + section.SectionName };
	}

	private sealed class Location
	{
		public Guid SectionId { get; init; }
		public Guid CampusId { get; init; }
		public string Label { get; init; } = string.Empty;
	}
}
