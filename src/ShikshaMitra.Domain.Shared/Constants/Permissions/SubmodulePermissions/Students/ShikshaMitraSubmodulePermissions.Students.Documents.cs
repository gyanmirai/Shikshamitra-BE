using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		public static class AdmissionDocuments
		{
			private const string Prefix = "Students.Admissions.Documents";
			public const string View = Prefix + ".View";
			public const string Add = Prefix + ".Add";
			public const string Delete = Prefix + ".Delete";
			public const string Upload = Add;
			public const string RecordOriginal = Add;
			public const string Review = Add;
			public const string Waive = Delete;
			public const string Download = View;
			public static readonly string[] All = [View, Add, Delete];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				[View] = "View admission document verification",
				[Add] = "Add or update admission document verification",
				[Delete] = "Delete or waive admission document verification"
			};
		}
	}
}
