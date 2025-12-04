using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class HR
	{
		public static class ProfessionalManagement_PerformanceReview
		{
			private const string SubmoduleName = $"{ModuleName}.ProfessionalManagement.PerformanceReview";
			private const string SubmoduleDisplayName = $"{DisplayName}.ProfessionalManagement.PerformanceReview";
			
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class ProfessionalManagement_KpiSetup
		{
			private const string SubmoduleName = $"{ModuleName}.ProfessionalManagement.KpiSetup";
			private const string SubmoduleDisplayName = $"{DisplayName}.ProfessionalManagement.KpiSetup";
			
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class ProfessionalManagement_ReviewHistory
		{
			private const string SubmoduleName = $"{ModuleName}.ProfessionalManagement.ReviewHistory";
			private const string SubmoduleDisplayName = $"{DisplayName}.ProfessionalManagement.ReviewHistory";
			
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}
	}
}
