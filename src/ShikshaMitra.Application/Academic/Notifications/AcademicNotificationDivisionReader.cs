using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record AcademicNotificationDivision(NotificationSourceScopeDto Scope, string Name);

[RemoteService(false)]
public sealed class AcademicNotificationDivisionReader(IRepository<Division, Guid> divisions, IRepository<Standard, Guid> standards,
	IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<AcademicNotificationDivision?> FindAsync(Guid school, Guid divisionId, CancellationToken token)
	{
		var placement = from division in await divisions.GetQueryableAsync()
			join standard in await standards.GetQueryableAsync() on division.StandardId equals standard.Id
			let stage = standard.EducationMediumStage
			let medium = stage.EducationMedium
			let board = medium.EducationBoard
			where division.Id == divisionId && division.TenantId == school && !division.IsDeleted &&
				standard.TenantId == school && !standard.IsDeleted && stage.TenantId == school && !stage.IsDeleted &&
				medium.TenantId == school && !medium.IsDeleted && board.TenantId == school && !board.IsDeleted &&
				board.AcademicYear.TenantId == school && !board.AcademicYear.IsDeleted
			select new AcademicNotificationDivision(new(board.AcademicYearId, board.Id, medium.Id, standard.Id, division.Id),
				standard.Name + " - " + division.Name);
		return await queries.SingleOrDefaultAsync(placement, token);
	}
}
