using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Academic.Dashboard;

public static class AcademicInsightsEntryAccess
{
	public static Task<AcademicInsightsDto> ReadAsync(IReadOnlyCollection<string> persistedRoles, bool dashboardGranted,
		Func<Task<AcademicInsightsDto>> verifiedPersonal, Func<Task<AcademicInsightsDto>> scopedStaff)
	{
		if (persistedRoles.Count == 1 && persistedRoles.Single() is "Student" or "Parent") return verifiedPersonal();
		if (!dashboardGranted) throw new AbpAuthorizationException();
		return scopedStaff();
	}
}
