using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionAssessmentCalendarScopeService(
	IRepository<AdmissionCycle, Guid> cycles,
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionAssessmentSession, Guid> sessions,
	IRepository<EducationMedium, Guid> mediums,
	IRepository<EducationBoard, Guid> boards) : ApplicationService, ITransientDependency
{
	internal async Task<List<RoomConflictAcademicScope>> GetAsync(IReadOnlyCollection<AdmissionOffering> selectedOfferings)
	{
		var cycleIds = selectedOfferings.Select(row => row.AdmissionCycleId).Distinct().ToArray();
		var years = (await AsyncExecuter.ToListAsync((await cycles.GetQueryableAsync())
			.Where(row => Enumerable.Contains(cycleIds, row.Id)).Select(row => new { row.Id, row.AcademicYearId })))
			.ToDictionary(row => row.Id, row => row.AcademicYearId);
		if (selectedOfferings.Count == 0 || years.Count != cycleIds.Length)
			throw AdmissionAssessmentSchedulingService.Invalid("The assessment offering's admission period is no longer available.");
		var mediumIds = selectedOfferings.Select(row => row.MediumId).Distinct().ToArray();
		var mediumYears = (await AsyncExecuter.ToListAsync(
			from medium in await mediums.GetQueryableAsync()
			join board in await boards.GetQueryableAsync() on medium.EducationBoardId equals board.Id
			where Enumerable.Contains(mediumIds, medium.Id)
			select new { medium.Id, board.AcademicYearId })).ToDictionary(row => row.Id, row => row.AcademicYearId);
		if (mediumYears.Count != mediumIds.Length)
			throw AdmissionAssessmentSchedulingService.Invalid("The assessment offering's academic medium is no longer available.");
		return selectedOfferings.Select(row => new RoomConflictAcademicScope(row.MediumId, years[row.AdmissionCycleId],
			row.StandardId, mediumYears[row.MediumId])).Distinct().ToList();
	}

	internal async Task<List<RoomConflictAcademicScope>?> GetForReservationAsync(Guid reservationId)
	{
		var session = await AsyncExecuter.FirstOrDefaultAsync((await sessions.WithDetailsAsync(row => row.Scopes))
			.Where(row => row.RoomReservationId == reservationId && !row.IsCancelled));
		if (session == null) return null;
		var offeringIds = session.Scopes.Count > 0
			? session.Scopes.Select(row => row.AdmissionOfferingId).Distinct().ToArray() : [session.AdmissionOfferingId];
		var rows = await offerings.GetListAsync(row => Enumerable.Contains(offeringIds, row.Id));
		if (rows.Count != offeringIds.Length)
			throw AdmissionAssessmentSchedulingService.Invalid("The assessment offering is no longer available.");
		return await GetAsync(rows);
	}
}
