using System;
using System.Data.Common;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.Attendance;

internal static class AttendanceCommandConflictMapper
{
	public static async Task<T> ExecuteAsync<T>(Func<Task<T>> command)
	{
		try { return await command(); }
		catch (AbpDbConcurrencyException) { throw new BusinessException(AttendanceOperationErrorCodes.ConcurrencyConflict); }
		catch (Exception exception) when (IsConflict(exception))
		{ throw new BusinessException(AttendanceOperationErrorCodes.ConcurrencyConflict); }
	}
	private static bool IsConflict(Exception exception)
	{
		for (Exception? current = exception; current != null; current = current.InnerException)
			if (current is DbException database && database.SqlState is "40001" or "40P01" or "23505") return true;
		return false;
	}
}
