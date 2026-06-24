using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Transport module permissions and submodules.
/// This file is split into multiple partial files for better maintainability:
/// - ShikshaMitraSubmodulePermissions.Transport.cs (Main module definition)
/// - ShikshaMitraSubmodulePermissions.Transport.Settings.cs
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// Transport module permissions
	/// </summary>
	public static partial class Transport
	{
		/// <summary>
		/// Permission group name for Transport module.
		/// </summary>
		public const string GroupName = "Transport";

		/// <summary>
		/// Display name for Transport module permission group.
		/// </summary>
		public const string GroupDisplayName = "Permission:TransportModule";

		private const string ModuleName = GroupName;
		private const string DisplayName = $"{DisplayNamePrefix}:{GroupName}";

		public const string Default = ModuleName;

		public static readonly Dictionary<string, string> DisplayNames = new()
		{
			{ Default, DisplayName }
		};

		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			// Core transport submodules
			{ "Transport.Dashboard", new SubmoduleInfo(Dashboard.All, Dashboard.DisplayNames) },
			{ "Transport.StopManagement", new SubmoduleInfo(StopManagement.All, StopManagement.DisplayNames) },
			{ "Transport.RouteManagement", new SubmoduleInfo(RouteManagement.All, RouteManagement.DisplayNames) },
			{ "Transport.VehicleManagement", new SubmoduleInfo(VehicleManagement.All, VehicleManagement.DisplayNames) },
			{ "Transport.TransportStaffManagement", new SubmoduleInfo(TransportStaffManagement.All, TransportStaffManagement.DisplayNames) },
			{ "Transport.ApplicationManagement", new SubmoduleInfo(ApplicationManagement.All, ApplicationManagement.DisplayNames) },
			{ "Transport.Reports", new SubmoduleInfo(Reports.All, Reports.DisplayNames) },
			{ "Transport.Notification", new SubmoduleInfo(Notification.All, Notification.DisplayNames) },

			// Settings submodules
			{ "Transport.Settings.VehicleType", new SubmoduleInfo(Settings_VehicleType.All, Settings_VehicleType.DisplayNames) },
			{ "Transport.Settings.VehicleDocument", new SubmoduleInfo(Settings_VehicleDocument.All, Settings_VehicleDocument.DisplayNames) },
			{ "Transport.Settings.ServicingType", new SubmoduleInfo(Settings_ServicingType.All, Settings_ServicingType.DisplayNames) },
			{ "Transport.Settings.FuelType", new SubmoduleInfo(Settings_FuelType.All, Settings_FuelType.DisplayNames) },
			{ "Transport.Settings.FeeConfiguration", new SubmoduleInfo(Settings_FeeConfiguration.All, Settings_FeeConfiguration.DisplayNames) },
			{ "Transport.Settings.ZoneAreaConfiguration", new SubmoduleInfo(Settings_ZoneAreaConfiguration.All, Settings_ZoneAreaConfiguration.DisplayNames) }
		};
	}
}
