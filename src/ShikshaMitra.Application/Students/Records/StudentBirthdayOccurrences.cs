using System;
using System.Globalization;
using System.Linq;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;

namespace ShikshaMitra.Students.Records;

internal static class StudentBirthdayOccurrences
{
	private const int SchoolDateOffsetMinutes = 330;

	internal static (DateOnly Start, DateOnly End) ResolveRange(StudentBirthdayQueryDto input, DateOnly today)
	{
		if (input.Period == "custom")
		{
			if (!DateOnly.TryParseExact(input.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
				!DateOnly.TryParseExact(input.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) ||
				end < start || end.DayNumber - start.DayNumber >= 366)
				throw new BusinessException(StudentRecordsErrorCodes.InvalidBirthdayQuery);
			return (start, end);
		}
		if (input.StartDate != null || input.EndDate != null) throw new BusinessException(StudentRecordsErrorCodes.InvalidBirthdayQuery);

		var monthStart = new DateOnly(today.Year, today.Month, 1);
		return input.Period switch
		{
			"today" => (today, today),
			"next7Days" => (today.AddDays(1), today.AddDays(7)),
			"thisMonth" => (monthStart, new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))),
			_ => throw new BusinessException(StudentRecordsErrorCodes.InvalidBirthdayQuery)
		};
	}

	internal static IQueryable<Occurrence> Query(IQueryable<Student> students, DateOnly today, DateOnly start, DateOnly end, string? search)
	{
		var startKey = DateKey(start);
		var endKey = DateKey(end);
		var invalidDateKey = DateKey(DateOnly.MinValue);
		var invalidDob = new DateTimeOffset(1, 1, 1, 1, 0, 0, TimeSpan.Zero);
		var tomorrow = new DateTimeOffset(today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
			.AddMinutes(-SchoolDateOffsetMinutes);
		var birthdates = from student in students
			where student.AdditionalDetail != null && student.AdditionalDetail.DateOfBirth.HasValue
			let savedDob = student.AdditionalDetail!.DateOfBirth!.Value
			// Replace provider timestamp infinities before extracting integer date parts.
			let finiteDob = savedDob >= invalidDob && savedDob < tomorrow ? savedDob : invalidDob
			// Saved DOBs can be UTC midnight or Indian local midnight serialized as UTC.
			let birth = finiteDob.UtcDateTime.AddMinutes(SchoolDateOffsetMinutes)
			let dateKey = birth.Year * 10000 + birth.Month * 100 + birth.Day
			where dateKey > invalidDateKey
			let name = ((student.FirstName ?? "") + (student.MiddleName != null && student.MiddleName != "" ? " " + student.MiddleName : "") +
				(student.LastName != null && student.LastName != "" ? " " + student.LastName : "")).Trim()
			select new
			{
				StudentId = student.Id,
				Name = name,
				StandardName = student.Standard != null ? student.Standard!.Name : null,
				DivisionName = student.Division != null ? student.Division!.Name : null,
				MonthDay = birth.Month * 100 + birth.Day,
				BirthDateKey = dateKey
			};
		if (!string.IsNullOrEmpty(search)) birthdates = birthdates.Where(row => row.Name.ToLower().Contains(search));

		IQueryable<Occurrence> InYear(int year)
		{
			var yearPrefix = year * 10000;
			var observeLeapBirthOnFebruary28 = !DateTime.IsLeapYear(year);
			return from birth in birthdates
				let occurrence = yearPrefix + (observeLeapBirthOnFebruary28 && birth.MonthDay == 229 ? 228 : birth.MonthDay)
				where occurrence >= startKey && occurrence <= endKey && occurrence >= birth.BirthDateKey
				select new Occurrence
				{
					StudentId = birth.StudentId, StudentName = birth.Name, DateKey = occurrence,
					StandardName = birth.StandardName, DivisionName = birth.DivisionName
				};
		}

		return start.Year == end.Year ? InYear(start.Year) : InYear(start.Year).Concat(InYear(end.Year));
	}

	private static int DateKey(DateOnly date) => date.Year * 10000 + date.Month * 100 + date.Day;

	internal sealed class Occurrence
	{
		public Guid StudentId { get; init; }
		public string StudentName { get; init; } = "";
		public string? StandardName { get; init; }
		public string? DivisionName { get; init; }
		public int DateKey { get; init; }
	}
}
