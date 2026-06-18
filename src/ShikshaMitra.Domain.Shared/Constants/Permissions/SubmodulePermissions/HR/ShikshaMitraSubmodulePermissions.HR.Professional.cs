using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
public static partial class HR
{
public static class ProfessionalManagement
{
private const string SubmoduleName = $"{ModuleName}.ProfessionalManagement";
private const string SubmoduleDisplayName = $"{DisplayName}.ProfessionalManagement";

public const string View = $"{SubmoduleName}.View";
public const string Add = $"{SubmoduleName}.Add";
public const string ManageTemplates = Add;
public const string ManageCycles = Add;
public const string AssignReviews = Add;
public const string SelfReview = View;
public const string ReviewEmployees = Add;
public const string ClassroomObservation = Add;
public const string Calibrate = Add;
public const string Finalize = Add;
public const string ManageOutcomes = Add;
public const string ViewConfidentialOutcome = View;
public const string ExportReports = View;
public const string ViewAudit = View;

public static readonly string[] All = { View, Add };
public static readonly Dictionary<string, string> DisplayNames = new()
{
{ SubmoduleName, SubmoduleDisplayName },
{ View, $"{SubmoduleDisplayName}.View" },
{ Add, $"{SubmoduleDisplayName}.Add" }
};
}

		public static class PerformanceManagement
		{
			private const string SubmoduleName = $"{ModuleName}.PerformanceManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.PerformanceManagement";

			public const string View = $"{SubmoduleName}.View";
			public const string ManageTemplates = $"{SubmoduleName}.ManageTemplates";
			public const string ManageCycles = $"{SubmoduleName}.ManageCycles";
			public const string AssignReviews = $"{SubmoduleName}.AssignReviews";
			public const string SelfReview = $"{SubmoduleName}.SelfReview";
			public const string ReviewEmployees = $"{SubmoduleName}.ReviewEmployees";
			public const string ClassroomObservation = $"{SubmoduleName}.ClassroomObservation";
			public const string Calibrate = $"{SubmoduleName}.Calibrate";
			public const string Finalize = $"{SubmoduleName}.Finalize";
			public const string ManageOutcomes = $"{SubmoduleName}.ManageOutcomes";
			public const string ViewConfidentialOutcome = $"{SubmoduleName}.ViewConfidentialOutcome";
			public const string ExportReports = $"{SubmoduleName}.ExportReports";
			public const string ViewAudit = $"{SubmoduleName}.ViewAudit";

			public static readonly string[] All =
			{
				View,
				ManageTemplates,
				ManageCycles,
				AssignReviews,
				SelfReview,
				ReviewEmployees,
				ClassroomObservation,
				Calibrate,
				Finalize,
				ManageOutcomes,
				ViewConfidentialOutcome,
				ExportReports,
				ViewAudit
			};

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ ManageTemplates, $"{SubmoduleDisplayName}.ManageTemplates" },
				{ ManageCycles, $"{SubmoduleDisplayName}.ManageCycles" },
				{ AssignReviews, $"{SubmoduleDisplayName}.AssignReviews" },
				{ SelfReview, $"{SubmoduleDisplayName}.SelfReview" },
				{ ReviewEmployees, $"{SubmoduleDisplayName}.ReviewEmployees" },
				{ ClassroomObservation, $"{SubmoduleDisplayName}.ClassroomObservation" },
				{ Calibrate, $"{SubmoduleDisplayName}.Calibrate" },
				{ Finalize, $"{SubmoduleDisplayName}.Finalize" },
				{ ManageOutcomes, $"{SubmoduleDisplayName}.ManageOutcomes" },
				{ ViewConfidentialOutcome, $"{SubmoduleDisplayName}.ViewConfidentialOutcome" },
				{ ExportReports, $"{SubmoduleDisplayName}.ExportReports" },
				{ ViewAudit, $"{SubmoduleDisplayName}.ViewAudit" }
			};
		}

		public static class PerformanceManagement_Overview
		{
			private const string SubmoduleName = $"{ModuleName}.PerformanceManagement.Overview";
			private const string SubmoduleDisplayName = $"{DisplayName}.PerformanceManagement.Overview";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class PerformanceManagement_Outcomes
		{
			private const string SubmoduleName = $"{ModuleName}.PerformanceManagement.Outcomes";
			private const string SubmoduleDisplayName = $"{DisplayName}.PerformanceManagement.Outcomes";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class PerformanceManagement_Reports
		{
			private const string SubmoduleName = $"{ModuleName}.PerformanceManagement.Reports";
			private const string SubmoduleDisplayName = $"{DisplayName}.PerformanceManagement.Reports";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class PerformanceManagement_Settings_Setup
		{
			private const string SubmoduleName = $"{ModuleName}.PerformanceManagement.Settings.Setup";
			private const string SubmoduleDisplayName = $"{DisplayName}.PerformanceManagement.Settings.Setup";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class PerformanceManagement_ReviewEmployees
		{
			private const string SubmoduleName = $"{ModuleName}.PerformanceManagement.ReviewEmployees";
			private const string SubmoduleDisplayName = $"{DisplayName}.PerformanceManagement.ReviewEmployees";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class PerformanceManagement_ManageCycles
		{
			private const string SubmoduleName = $"{ModuleName}.PerformanceManagement.ManageCycles";
			private const string SubmoduleDisplayName = $"{DisplayName}.PerformanceManagement.ManageCycles";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}
	}
}
