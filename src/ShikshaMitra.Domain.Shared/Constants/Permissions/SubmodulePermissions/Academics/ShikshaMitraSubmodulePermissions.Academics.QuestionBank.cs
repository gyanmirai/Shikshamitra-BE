using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Academics module - Question Bank related permissions
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Academics
	{
		public static class Examination_QuestionBank
		{
			private const string SubmoduleName = $"{ModuleName}.Examination.QuestionBank";
			private const string SubmoduleDisplayName = $"{DisplayName}.Examination.QuestionBank";

			public const string View = $"{SubmoduleName}.View";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Submit = $"{SubmoduleName}.Submit";
			public const string Approve = $"{SubmoduleName}.Approve";
			public const string Reject = $"{SubmoduleName}.Reject";
			public const string Archive = $"{SubmoduleName}.Archive";
			public const string Duplicate = $"{SubmoduleName}.Duplicate";

			public static readonly string[] All = { View, Create, Edit, Delete, Submit, Approve, Reject, Archive, Duplicate };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Submit, $"{SubmoduleDisplayName}.Submit" },
				{ Approve, $"{SubmoduleDisplayName}.Approve" },
				{ Reject, $"{SubmoduleDisplayName}.Reject" },
				{ Archive, $"{SubmoduleDisplayName}.Archive" },
				{ Duplicate, $"{SubmoduleDisplayName}.Duplicate" }
			};
		}
	}
}
