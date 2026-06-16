using System;

namespace ShikshaMitra.TenantSettings.AcademicYears;

public static class AcademicYearBusinessDate
{
	public static DateTime Normalize(DateTime value) =>
		DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

	public static string Format(DateTime value) =>
		Normalize(value).ToString("yyyy-MM-dd");
}
