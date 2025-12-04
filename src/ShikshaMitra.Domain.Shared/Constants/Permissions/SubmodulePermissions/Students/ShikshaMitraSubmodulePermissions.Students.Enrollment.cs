using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Students module - Enrollment related permissions
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		/// <summary>
		/// Student Enrollment - Inquiry permissions
		/// </summary>
		public static class Enrollment_Inquiry
		{
			private const string SubmoduleName = $"{ModuleName}.Enrollment.Inquiry";
			private const string SubmoduleDisplayName = $"{DisplayName}.Enrollment.Inquiry";

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
		/// Student Enrollment - Registration Form Issued permissions
		/// </summary>
		public static class Enrollment_RegistrationFormIssued
		{
			private const string SubmoduleName = $"{ModuleName}.Enrollment.RegistrationFormIssued";
			private const string SubmoduleDisplayName = $"{DisplayName}.Enrollment.RegistrationFormIssued";

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
		/// Student Enrollment - Entrance Exam permissions
		/// </summary>
		public static class Enrollment_EntranceExam
		{
			private const string SubmoduleName = $"{ModuleName}.Enrollment.EntranceExam";
			private const string SubmoduleDisplayName = $"{DisplayName}.Enrollment.EntranceExam";

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
		/// Student Enrollment - Admission permissions
		/// </summary>
		public static class Enrollment_Admission
		{
			private const string SubmoduleName = $"{ModuleName}.Enrollment.Admission";
			private const string SubmoduleDisplayName = $"{DisplayName}.Enrollment.Admission";

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
		/// Student Enrollment - Reports permissions
		/// </summary>
		public static class Enrollment_Reports
		{
			private const string SubmoduleName = $"{ModuleName}.Enrollment.Reports";
			private const string SubmoduleDisplayName = $"{DisplayName}.Enrollment.Reports";

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
		/// Student Enrollment - Settings - Document Types permissions
		/// </summary>
		public static class Enrollment_SettingsDocumentTypes
		{
			private const string SubmoduleName = $"{ModuleName}.Enrollment.Settings.DocumentTypes";
			private const string SubmoduleDisplayName = $"{DisplayName}.Enrollment.Settings.DocumentTypes";

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
		/// Student Enrollment - Settings - Inquiry Preference permissions
		/// </summary>
		public static class Enrollment_SettingsInquiryPreference
		{
			private const string SubmoduleName = $"{ModuleName}.Enrollment.Settings.InquiryPreference";
			private const string SubmoduleDisplayName = $"{DisplayName}.Enrollment.Settings.InquiryPreference";

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
		/// Student Enrollment - Settings - Entrance Exam Slots permissions
		/// </summary>
		public static class Enrollment_SettingsEntranceExamSlots
		{
			private const string SubmoduleName = $"{ModuleName}.Enrollment.Settings.EntranceExamSlots";
			private const string SubmoduleDisplayName = $"{DisplayName}.Enrollment.Settings.EntranceExamSlots";

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
		/// Student Enrollment - Settings - Registration Form Amount permissions
		/// </summary>
		public static class Enrollment_SettingsRegistrationFormAmount
		{
			private const string SubmoduleName = $"{ModuleName}.Enrollment.Settings.RegistrationFormAmount";
			private const string SubmoduleDisplayName = $"{DisplayName}.Enrollment.Settings.RegistrationFormAmount";

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
		/// Student Enrollment - Settings - Form Number Series permissions
		/// </summary>
		public static class Enrollment_SettingsFormNumberSeries
		{
			private const string SubmoduleName = $"{ModuleName}.Enrollment.Settings.FormNumberSeries";
			private const string SubmoduleDisplayName = $"{DisplayName}.Enrollment.Settings.FormNumberSeries";

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
