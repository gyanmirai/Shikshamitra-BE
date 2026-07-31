using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Academics module - Question Papers related permissions
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Academics
	{
		public static class Examination_QuestionPapers
		{
			private const string SubmoduleName = $"{ModuleName}.Examination.QuestionPapers";
			private const string SubmoduleDisplayName = $"{DisplayName}.Examination.QuestionPapers";

			public const string View = $"{SubmoduleName}.View";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Submit = $"{SubmoduleName}.Submit";
			public const string Approve = $"{SubmoduleName}.Approve";
			public const string Reject = $"{SubmoduleName}.Reject";
			public const string Publish = $"{SubmoduleName}.Publish";
			public const string Lock = $"{SubmoduleName}.Lock";
			public const string Archive = $"{SubmoduleName}.Archive";
			public const string ManageBlueprint = $"{SubmoduleName}.ManageBlueprint";
			public const string Generate = $"{SubmoduleName}.Generate";

			public static readonly string[] All = { View, Create, Edit, Delete, Submit, Approve, Reject, Publish, Lock, Archive, ManageBlueprint, Generate };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Submit, $"{SubmoduleDisplayName}.Submit" },
				{ Approve, $"{SubmoduleDisplayName}.Approve" },
				{ Reject, $"{SubmoduleDisplayName}.Reject" },
				{ Publish, $"{SubmoduleDisplayName}.Publish" },
				{ Lock, $"{SubmoduleDisplayName}.Lock" },
				{ Archive, $"{SubmoduleDisplayName}.Archive" },
				{ ManageBlueprint, $"{SubmoduleDisplayName}.ManageBlueprint" },
				{ Generate, $"{SubmoduleDisplayName}.Generate" }
			};
		}
	}
}
