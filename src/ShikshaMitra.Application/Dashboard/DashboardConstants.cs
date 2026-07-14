using System;
using System.Collections.Generic;

namespace ShikshaMitra.Dashboard;

internal static class DashboardTemplates
{
	public const string Leadership = "leadership";
	public const string PrincipalAdmin = "principalAdmin";
	public const string Teacher = "teacher";
	public const string FinanceHr = "financeHr";
	public const string StudentParent = "studentParent";

	public static readonly HashSet<string> ValidTemplates = new(StringComparer.Ordinal)
	{
		Leadership,
		PrincipalAdmin,
		Teacher,
		FinanceHr,
		StudentParent
	};
}

internal static class DashboardZones
{
	public const string Kpis = "kpis";
	public const string Actions = "actions";
	public const string Risks = "risks";
	public const string Modules = "modules";
	public const string Calendar = "calendar";
	public const string Announcements = "announcements";
}
