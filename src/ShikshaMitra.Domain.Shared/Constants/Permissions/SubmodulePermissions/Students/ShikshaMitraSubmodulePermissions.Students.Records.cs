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

		/// <summary>
		/// Student Records - Student I-Card permissions
		/// </summary>
		public static class Records_StudentICard
		{
			private const string SubmoduleName = $"{ModuleName}.Records.StudentICard";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.StudentICard";

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

		/// <summary>
		/// Student Records - Student Management permissions
		/// </summary>
		public static class Records_StudentManagement
		{
			private const string SubmoduleName = $"{ModuleName}.Records.StudentManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.StudentManagement";

			public const string GrNoGenerateView = $"{SubmoduleName}.GrNoGenerate.View";
			public const string GrNoGenerateAdd = $"{SubmoduleName}.GrNoGenerate.Add";
			public const string GrNoGenerateDelete = $"{SubmoduleName}.GrNoGenerate.Delete";
			
			public const string RollNoGenerationView = $"{SubmoduleName}.RollNoGeneration.View";
			public const string RollNoGenerationAdd = $"{SubmoduleName}.RollNoGeneration.Add";
			public const string RollNoGenerationDelete = $"{SubmoduleName}.RollNoGeneration.Delete";
			
			public const string ChangeDivisionView = $"{SubmoduleName}.ChangeDivision.View";
			public const string ChangeDivisionAdd = $"{SubmoduleName}.ChangeDivision.Add";
			public const string ChangeDivisionDelete = $"{SubmoduleName}.ChangeDivision.Delete";
			
			public const string HouseManagementView = $"{SubmoduleName}.HouseManagement.View";
			public const string HouseManagementAdd = $"{SubmoduleName}.HouseManagement.Add";
			public const string HouseManagementDelete = $"{SubmoduleName}.HouseManagement.Delete";

			public static readonly string[] All = { 
				GrNoGenerateView, GrNoGenerateAdd, GrNoGenerateDelete,
				RollNoGenerationView, RollNoGenerationAdd, RollNoGenerationDelete,
				ChangeDivisionView, ChangeDivisionAdd, ChangeDivisionDelete,
				HouseManagementView, HouseManagementAdd, HouseManagementDelete
			};

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ GrNoGenerateView, $"{SubmoduleDisplayName}.GrNoGenerate.View" },
				{ GrNoGenerateAdd, $"{SubmoduleDisplayName}.GrNoGenerate.Add" },
				{ GrNoGenerateDelete, $"{SubmoduleDisplayName}.GrNoGenerate.Delete" },
				
				{ RollNoGenerationView, $"{SubmoduleDisplayName}.RollNoGeneration.View" },
				{ RollNoGenerationAdd, $"{SubmoduleDisplayName}.RollNoGeneration.Add" },
				{ RollNoGenerationDelete, $"{SubmoduleDisplayName}.RollNoGeneration.Delete" },
				
				{ ChangeDivisionView, $"{SubmoduleDisplayName}.ChangeDivision.View" },
				{ ChangeDivisionAdd, $"{SubmoduleDisplayName}.ChangeDivision.Add" },
				{ ChangeDivisionDelete, $"{SubmoduleDisplayName}.ChangeDivision.Delete" },
				
				{ HouseManagementView, $"{SubmoduleDisplayName}.HouseManagement.View" },
				{ HouseManagementAdd, $"{SubmoduleDisplayName}.HouseManagement.Add" },
				{ HouseManagementDelete, $"{SubmoduleDisplayName}.HouseManagement.Delete" }
			};
		}

		/// <summary>
		/// Student Records - Certificates - Bonafide Certificates permissions
		/// </summary>
		public static class Records_CertificatesBonafide
		{
			private const string SubmoduleName = $"{ModuleName}.Records.Certificates.BonafideCertificates";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.Certificates.BonafideCertificates";

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

		/// <summary>
		/// Student Records - Certificates - First Trial Certificates permissions
		/// </summary>
		public static class Records_CertificatesFirstTrial
		{
			private const string SubmoduleName = $"{ModuleName}.Records.Certificates.FirstTrialCertificates";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.Certificates.FirstTrialCertificates";

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

		/// <summary>
		/// Student Records - Certificates - Leaving Certificates permissions
		/// </summary>
		public static class Records_CertificatesLeaving
		{
			private const string SubmoduleName = $"{ModuleName}.Records.Certificates.LeavingCertificates";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.Certificates.LeavingCertificates";

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

		/// <summary>
		/// Student Records - Certificates - Character Certificates permissions
		/// </summary>
		public static class Records_CertificatesCharacter
		{
			private const string SubmoduleName = $"{ModuleName}.Records.Certificates.CharacterCertificates";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.Certificates.CharacterCertificates";

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

		/// <summary>
		/// Student Records - Settings permissions
		/// </summary>
		public static class Records_Settings
		{
			private const string SubmoduleName = $"{ModuleName}.Records.Settings";
			private const string SubmoduleDisplayName = $"{DisplayName}.Records.Settings";

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
