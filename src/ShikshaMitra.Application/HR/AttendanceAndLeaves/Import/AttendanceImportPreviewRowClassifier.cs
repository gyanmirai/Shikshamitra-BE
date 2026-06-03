using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Import;

public class AttendanceImportPreviewRowClassifier(
	IEmployeeAttendanceRepository employeeAttendanceRepository)
	: ApplicationService, ITransientDependency
{
	internal async Task ClassifyAsync(List<AttendanceImportPreviewRow> validRows)
	{
		if (validRows.Count == 0)
		{
			return;
		}

		var employeeIds = validRows.Select(x => x.EmployeeId).Distinct().ToList();
		var minDate = validRows.Min(x => x.AttendanceDateValue!.Value);
		var maxDate = validRows.Max(x => x.AttendanceDateValue!.Value);
		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();
		var existingKeys = await AsyncExecuter.ToListAsync(
			attendanceQuery
				.Where(x =>
					employeeIds.Contains(x.EmployeeId) &&
					x.AttendanceDate >= minDate &&
					x.AttendanceDate <= maxDate)
				.Select(x => new { x.EmployeeId, x.AttendanceDate }));

		var existingLookup = existingKeys
			.Select(x => AttendanceImportPreviewMapper.BuildRowKey(x.EmployeeId, x.AttendanceDate))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var row in validRows)
		{
			row.Action = existingLookup.Contains(AttendanceImportPreviewMapper.BuildRowKey(row.EmployeeId, row.AttendanceDateValue!.Value))
				? "replace"
				: "create";
		}
	}
}
