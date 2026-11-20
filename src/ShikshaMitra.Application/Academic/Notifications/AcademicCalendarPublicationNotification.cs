using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using CalendarPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class AcademicCalendarPublicationNotification(AcademicCalendarPublicationSource source, INotificationIntentWriter writer,
	IRepository<NotificationRule, Guid> rules, IAsyncQueryableExecuter queries, IPermissionChecker permissions, ICurrentTenant tenant,
	ICurrentUser current, ICancellationTokenProvider cancellation, IUnitOfWorkManager units) : ITransientDependency
{
	internal async Task RecordAsync(AcademicCalendar calendar)
	{
		if (units.Current?.Options.IsTransactional != true || tenant.Id is not Guid school || current.TenantId != school ||
			current.Id is not Guid user || calendar.TenantId != school || !await permissions.IsGrantedAsync(CalendarPermissions.Edit))
			return;
		var token = cancellation.Token;
		var scope = await source.ReadScopeAsync(school, calendar, token);
		if (scope == null) return;
		try { await source.EnsureMediumScopeAsync(school, user, scope, token); }
		catch (AbpAuthorizationException) { return; }

		var revision = AcademicCalendarPublicationSource.Record(calendar, scope);
		var active = (await rules.GetQueryableAsync()).Where(x =>
			x.TenantId == school && !x.IsDeleted && x.IsEnabled && x.EventKey == AcademicCalendarPublicationSource.EventKey &&
			(!x.AcademicYearId.HasValue || x.AcademicYearId == scope.AcademicYearId) &&
			(!x.BoardId.HasValue || x.BoardId == scope.BoardId) && (!x.MediumId.HasValue || x.MediumId == scope.MediumId));
		if (!await queries.AnyAsync(active, token)) return;
		await writer.WriteAsync(new(AcademicCalendarPublicationSource.EventKey, AcademicCalendarPublicationSource.Type,
			calendar.Id, revision, DateTimeOffset.UtcNow, [], ActorUserId: user), token);
	}
}
