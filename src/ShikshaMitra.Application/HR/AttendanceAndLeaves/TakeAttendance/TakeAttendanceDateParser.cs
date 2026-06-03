using System;
using System.Globalization;
using ShikshaMitra.HR;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

public static class TakeAttendanceDateParser
{
	public static DateTime ParseDate(string dateString)
	{
		if (DateTime.TryParseExact(dateString, ["dd-MM-yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture,
			    DateTimeStyles.None, out var result))
		{
			return DateTime.SpecifyKind(result, DateTimeKind.Unspecified);
		}

		if (DateTimeOffset.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
		{
			return DateTime.SpecifyKind(dto.Date, DateTimeKind.Unspecified);
		}

		if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
		{
			return DateTime.SpecifyKind(result, DateTimeKind.Unspecified);
		}

		throw new BusinessException(HRErrorCodes.AttendanceInvalidDateFormat);
	}
}
