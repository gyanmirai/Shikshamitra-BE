using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Groups;
using ShikshaMitra.HR;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Application.HR.Calendar.CalendarView;

[RemoteService(false)]
public class CalendarViewVisibilityService(
	IGroupAppService groupAppService,
	IUserScopeService userScopeService,
	HrUserScopeAccessService hrScopeAccessService)
	: ApplicationService, ITransientDependency
{
	public async Task<CalendarVisibilityContext> ResolveAsync(Guid academicYearId)
	{
		var userId = CurrentUser.Id;
		if (!userId.HasValue)
		{
			return CalendarVisibilityContext.Restricted;
		}

		var scope = await userScopeService.GetUserScopeAsync(userId.Value);
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return CalendarVisibilityContext.Admin;
		}

		if (!HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
		{
			hrScopeAccessService.LogDeniedAccess(scope, "CalendarView", "CalendarAcademicYear", academicYearId);
			return CalendarVisibilityContext.Restricted;
		}

		var userGroupIds = await groupAppService.GetUserGroupIdsAsync(userId.Value, academicYearId);
		return new CalendarVisibilityContext(false, userGroupIds);
	}

	public static List<T> Apply<T>(
		List<T> items,
		CalendarVisibilityContext visibility,
		Func<T, IReadOnlyCollection<Guid>> audienceSelector)
	{
		if (visibility.IsAdmin)
		{
			return items;
		}

		if (visibility.UserGroupIds.Count == 0)
		{
			return items.Where(x => audienceSelector(x).Count == 0).ToList();
		}

		return items
			.Where(x => audienceSelector(x).Count == 0 ||
						audienceSelector(x).Any(visibility.UserGroupIds.Contains))
			.ToList();
	}
}

public sealed record CalendarVisibilityContext(bool IsAdmin, List<Guid> UserGroupIds)
{
	public static CalendarVisibilityContext Admin { get; } = new(true, []);
	public static CalendarVisibilityContext Restricted { get; } = new(false, []);
}
