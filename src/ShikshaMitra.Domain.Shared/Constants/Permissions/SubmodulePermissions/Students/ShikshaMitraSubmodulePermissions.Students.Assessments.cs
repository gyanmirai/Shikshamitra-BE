using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		public static class AdmissionAssessments
		{
			private const string Prefix = "Students.Admissions.Assessments";
			public const string View = Prefix + ".View";
			public const string Add = Prefix + ".Add";
			public const string Delete = Prefix + ".Delete";
			public const string ManageSessions = Add;
			public const string Book = Add;
			public const string RecordAttendance = Add;
			public const string RecordResults = Add;
			public const string Review = Add;
			public const string Waive = Add;
			public static readonly string[] All = [View, Add, Delete];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				[View] = "View admission assessments",
				[Add] = "Add or update admission assessments",
				[Delete] = "Delete admission assessments"
			};
		}
	}
}
