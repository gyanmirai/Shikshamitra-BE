using System;
using System.Collections.Generic;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmPlaybookValidationRules
{
	public static readonly StringComparer CodeComparer = StringComparer.OrdinalIgnoreCase;

	public static void CodeAndName(string code, string name)
	{
		Check.NotNullOrWhiteSpace(code, nameof(code), AdmissionCrmConsts.MaxPlaybookCodeLength);
		Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCrmConsts.MaxTemplateNameLength);
	}

	public static void Unique(IEnumerable<string> values, string errorCode)
	{
		var seen = new HashSet<string>(CodeComparer);
		foreach (var value in values)
			if (string.IsNullOrWhiteSpace(value) || !seen.Add(value.Trim()))
				throw new BusinessException(errorCode).WithData("Value", value);
	}
}
