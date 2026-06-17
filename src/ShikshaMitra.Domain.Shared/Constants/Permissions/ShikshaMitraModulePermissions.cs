using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static class ShikshaMitraModulePermissions
{
	public const string GroupName = "ShikshaMitra.Module";

	public const string Dashboard = $"{GroupName}.Dashboard";
	public const string Students = $"{GroupName}.Students";
	public const string Academics = $"{GroupName}.Academics";
	public const string ClassManagement = $"{GroupName}.ClassManagement";
 	public const string HR = $"{GroupName}.HR";
	public const string HR_PerformanceManagement_Settings = $"{HR}.PerformanceManagement.Settings";
	public const string Finance = $"{GroupName}.Finance";
	public const string Communication = $"{GroupName}.Communication";
	public const string Calendar = $"{GroupName}.Calendar";
	public const string Library = $"{GroupName}.Library";
	public const string Transport = $"{GroupName}.Transport";
	public const string ResourceManagement = $"{GroupName}.ResourceManagement";
	public const string Hostel = $"{GroupName}.Hostel";
	public const string Security = $"{GroupName}.Security";
	public const string SecurityGuard = $"{GroupName}.SecurityGuard";
	public const string InstituteSetup = $"{GroupName}.InstituteSetup";
 
 	public static readonly string[] All =
 	{
 		Dashboard,
 		Students,
 		Academics,
 		ClassManagement,
 		HR,
		HR_PerformanceManagement_Settings,
 		Finance,
 		Communication,
 		Calendar,
 		Library,
 		Transport,
 		ResourceManagement,
 		Hostel,
 		Security,
 		SecurityGuard,
 		InstituteSetup
 	};
 
 	private const string DisplayNamePrefix = "Permission:Module";
 
 	public static readonly Dictionary<string, string> DisplayNames = new()
 	{
 		{ Dashboard, $"{DisplayNamePrefix}.Dashboard" },
 		{ Students, $"{DisplayNamePrefix}.Students" },
 		{ Academics, $"{DisplayNamePrefix}.Academics" },
 		{ ClassManagement, $"{DisplayNamePrefix}.ClassManagement" },
 		{ HR, $"{DisplayNamePrefix}.HR" },
		{ HR_PerformanceManagement_Settings, $"{DisplayNamePrefix}.HR_PerformanceManagement_Settings" },
 		{ Finance, $"{DisplayNamePrefix}.Finance" },
		{ Communication, $"{DisplayNamePrefix}.Communication" },
		{ Calendar, $"{DisplayNamePrefix}.Calendar" },
		{ Library, $"{DisplayNamePrefix}.Library" },
		{ Transport, $"{DisplayNamePrefix}.Transport" },
		{ ResourceManagement, $"{DisplayNamePrefix}.ResourceManagement" },
		{ Hostel, $"{DisplayNamePrefix}.Hostel" },
		{ Security, $"{DisplayNamePrefix}.Security" },
		{ SecurityGuard, $"{DisplayNamePrefix}.SecurityGuard" },
		{ InstituteSetup, $"{DisplayNamePrefix}.InstituteSetup" }
	};
}
