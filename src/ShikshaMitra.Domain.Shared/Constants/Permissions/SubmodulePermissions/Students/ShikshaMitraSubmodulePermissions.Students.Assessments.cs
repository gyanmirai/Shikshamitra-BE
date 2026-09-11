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
			public const string ManageSessions = Prefix + ".ManageSessions";
			public const string Book = Prefix + ".Book";
			public const string RecordAttendance = Prefix + ".RecordAttendance";
			public const string RecordResults = Prefix + ".RecordResults";
			public const string Review = Prefix + ".Review";
			public const string Waive = Prefix + ".Waive";
			public static readonly string[] All = [View, ManageSessions, Book, RecordAttendance, RecordResults, Review, Waive];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				[View] = "View admission assessments", [ManageSessions] = "Manage assessment sessions",
				[Book] = "Book assessments and arrange retakes", [RecordAttendance] = "Record assessment attendance",
				[RecordResults] = "Record assessment results", [Review] = "Review assessment results", [Waive] = "Waive admission assessments"
			};
		}
	}
}
