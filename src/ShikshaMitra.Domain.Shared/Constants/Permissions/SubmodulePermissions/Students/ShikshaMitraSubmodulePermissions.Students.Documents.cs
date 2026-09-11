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
			public const string Upload = Prefix + ".Upload";
			public const string RecordOriginal = Prefix + ".RecordOriginal";
			public const string Review = Prefix + ".Review";
			public const string Waive = Prefix + ".Waive";
			public const string Download = Prefix + ".Download";
			public static readonly string[] All = [View, Upload, RecordOriginal, Review, Waive, Download];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				[View] = "View admission document verification", [Upload] = "Upload admission evidence",
				[RecordOriginal] = "Record original document checks", [Review] = "Review admission evidence",
				[Waive] = "Waive admission document requirements", [Download] = "Download private admission evidence"
			};
		}
	}
}
