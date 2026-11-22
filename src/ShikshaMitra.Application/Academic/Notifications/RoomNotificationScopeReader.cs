using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class RoomNotificationScopeReader(IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions,
	IRepository<EducationMedium, Guid> mediums, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<NotificationSourceScopeDto?> ReadAsync(Guid tenantId, RoomReservation reservation, CancellationToken token)
	{
		var classId = reservation.StandardId;
		var divisionId = reservation.DivisionId;
		var calendarMediumId = RoomAvailabilityAppService.ReservationCalendarMedium(reservation);
		if (divisionId.HasValue)
		{
			var owner = await queries.SingleOrDefaultAsync((await divisions.GetQueryableAsync()).Where(x =>
				x.TenantId == tenantId && !x.IsDeleted && x.Id == divisionId).Select(x => (Guid?)x.StandardId), token);
			if (!owner.HasValue || classId.HasValue && owner != classId) return null;
			classId = owner;
		}
		if (classId.HasValue)
		{
			var placement = from standard in await standards.GetQueryableAsync()
				let stage = standard.EducationMediumStage
				let medium = stage.EducationMedium
				let board = medium.EducationBoard
				where standard.Id == classId && standard.TenantId == tenantId && stage.TenantId == tenantId &&
					medium.TenantId == tenantId && board.TenantId == tenantId && board.AcademicYear.TenantId == tenantId &&
					(!calendarMediumId.HasValue || calendarMediumId == medium.Id)
				select new NotificationSourceScopeDto(board.AcademicYearId, board.Id, medium.Id, standard.Id, divisionId);
			return await queries.SingleOrDefaultAsync(placement, token);
		}
		if (!calendarMediumId.HasValue) return new();
		return await queries.SingleOrDefaultAsync((await mediums.GetQueryableAsync()).Where(x =>
			x.Id == calendarMediumId && x.TenantId == tenantId && x.EducationBoard.TenantId == tenantId &&
			x.EducationBoard.AcademicYear.TenantId == tenantId)
			.Select(x => new NotificationSourceScopeDto(x.EducationBoard.AcademicYearId, x.EducationBoardId, x.Id, null, null)), token);
	}
}
