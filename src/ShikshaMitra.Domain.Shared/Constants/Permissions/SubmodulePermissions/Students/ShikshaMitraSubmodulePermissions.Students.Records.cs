using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Students module - Records related permissions
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		/// <summary>
		/// Student Records - Student List permissions
		/// </summary>
		public static class Records_StudentList
		{
			private const string SubmoduleName = $"{ModuleName}.Records.StudentList";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.StudentList";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";

			public static readonly string[] All = { Default, View };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" }
			};
		}

		/// <summary>
		/// Student Records - Student I-Card permissions
		/// </summary>
		public static class Records_StudentICard
		{
			private const string SubmoduleName = $"{ModuleName}.Records.StudentICard";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.StudentICard";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";

			public static readonly string[] All = { Default, View, Add, Update };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Update, $"{SubmoduleDisplayName}.Update" }
			};
		}

		/// <summary>
		/// Student Records - Student Management permissions
		/// </summary>
		public static class Records_StudentManagement
		{
			private const string SubmoduleName = $"{ModuleName}.Records.StudentManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.StudentManagement";

			public const string Default = SubmoduleName;
			public const string GrNoGenerateView = $"{SubmoduleName}.GrNoGenerate.View";
			public const string RollNoGenerationView = $"{SubmoduleName}.RollNoGeneration.View";
			public const string ChangeDivisionView = $"{SubmoduleName}.ChangeDivision.View";
			public const string HouseManagementView = $"{SubmoduleName}.HouseManagement.View";

			public static readonly string[] All = { Default, GrNoGenerateView, RollNoGenerationView, ChangeDivisionView, HouseManagementView };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ GrNoGenerateView, $"{SubmoduleDisplayName}.GrNoGenerate.View" },
				{ RollNoGenerationView, $"{SubmoduleDisplayName}.RollNoGeneration.View" },
				{ ChangeDivisionView, $"{SubmoduleDisplayName}.ChangeDivision.View" },
				{ HouseManagementView, $"{SubmoduleDisplayName}.HouseManagement.View" }
			};
		}

		/// <summary>
		/// Student Records - Certificates - Bonafide Certificates permissions
		/// </summary>
		public static class Records_CertificatesBonafide
		{
			private const string SubmoduleName = $"{ModuleName}.Records.Certificates.BonafideCertificates";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.Certificates.BonafideCertificates";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";

			public static readonly string[] All = { Default, View, Add };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }
			};
		}

		/// <summary>
		/// Student Records - Certificates - First Trial Certificates permissions
		/// </summary>
		public static class Records_CertificatesFirstTrial
		{
			private const string SubmoduleName = $"{ModuleName}.Records.Certificates.FirstTrialCertificates";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.Certificates.FirstTrialCertificates";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";

			public static readonly string[] All = { Default, View, Add };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }
			};
		}

		/// <summary>
		/// Student Records - Certificates - Leaving Certificates permissions
		/// </summary>
		public static class Records_CertificatesLeaving
		{
			private const string SubmoduleName = $"{ModuleName}.Records.Certificates.LeavingCertificates";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.Certificates.LeavingCertificates";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";

			public static readonly string[] All = { Default, View, Add };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }
			};
		}

		/// <summary>
		/// Student Records - Certificates - Character Certificates permissions
		/// </summary>
		public static class Records_CertificatesCharacter
		{
			private const string SubmoduleName = $"{ModuleName}.Records.Certificates.CharacterCertificates";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.Certificates.CharacterCertificates";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";

			public static readonly string[] All = { Default, View, Add };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }
			};
		}

		/// <summary>
		/// Student Records - Settings permissions
		/// </summary>
		public static class Records_Settings
		{
			private const string SubmoduleName = $"{ModuleName}.Records.Settings";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.Settings";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { Default, View, Add, Update, Delete };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}
	}
}
