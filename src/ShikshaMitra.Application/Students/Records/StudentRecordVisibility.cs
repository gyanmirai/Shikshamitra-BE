using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Students.StudentRecords;

namespace ShikshaMitra.Students.Records;

public static class StudentRecordVisibility
{
	public static bool IsMasked(StudentRecordsPolicy policy, string section, IEnumerable<string> roles)
	{
		string[] groups = section switch
		{
			"general" => ["Identity", "Contact"], "family" => ["Guardian", "Contact"], "photo" => ["Identity"],
			"communication" => ["Contact"], "medical" => ["Medical"], "bank" => ["Bank"],
			"additional" => ["Identity", "Medical", "Contact"], _ => []
		};
		return groups.Any(group => policy.IsMasked(group, roles));
	}
}
