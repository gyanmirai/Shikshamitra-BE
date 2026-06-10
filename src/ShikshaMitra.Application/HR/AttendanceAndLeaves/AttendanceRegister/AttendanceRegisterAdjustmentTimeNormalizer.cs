using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Policies;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;

internal static class AttendanceRegisterAdjustmentTimeNormalizer
{
	public static async Task<(DateTimeOffset? TimeIn, DateTimeOffset? TimeOut)> NormalizeAsync(
		HrPolicyResolver policyResolver,
		Guid employeeId,
		DateOnly attendanceDate,
		DateTimeOffset? timeIn,
		DateTimeOffset? timeOut)
	{
		var policies = await policyResolver.ResolveAsync(
			employeeId,
			attendanceDate.ToDateTime(TimeOnly.MinValue));
		return policies.AttendancePolicy.CaptureMode == AttendanceCaptureMode.ManualStatus
			? (null, null)
			: (timeIn, timeOut);
	}
}
