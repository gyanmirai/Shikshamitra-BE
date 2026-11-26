using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Students.Records;

internal static class StudentIdCardAssignmentRosterFilters
{
	public static void Validate(IReadOnlyList<StudentDirectoryColumnFilterDto>? filters)
	{
		if (filters == null || filters.Count > 20 || filters.Any(filter => !IsValid(filter)))
			throw new BusinessException(StudentErrorCodes.InvalidIdAssignmentRosterFilter);
	}

	public static IQueryable<StudentIdCardAssignmentRosterStudentDto> Apply(
		IQueryable<StudentIdCardAssignmentRosterStudentDto> query, IReadOnlyList<StudentDirectoryColumnFilterDto> filters)
	{
		foreach (var filter in filters)
		{
			query = filter.Column switch
			{
				"studentName" => StudentDirectoryColumnFilters.ApplyTextFilter(query, student => student.StudentName, filter),
				"grNumber" => StudentDirectoryColumnFilters.ApplyTextFilter(query, student => student.GrNumber ?? "", filter),
				"rollNumber" => ApplyRollNumber(query, filter),
				_ => query.Where(student => student.HasValidIssuedCard == (filter.Value == "true"))
			};
		}
		return query;
	}

	private static bool IsValid(StudentDirectoryColumnFilterDto? filter) => filter?.Column switch
	{
		"studentName" or "grNumber" => StudentDirectoryColumnFilters.IsValidTextFilter(filter),
		"rollNumber" => StudentDirectoryColumnFilters.IsValidRollNumberFilter(filter),
		"hasValidIssuedCard" => filter.Operator == "equals" && filter.Value is ("true" or "false"),
		_ => false
	};

	private static IQueryable<StudentIdCardAssignmentRosterStudentDto> ApplyRollNumber(
		IQueryable<StudentIdCardAssignmentRosterStudentDto> query, StudentDirectoryColumnFilterDto filter)
	{
		// Integral bounds preserve decimal filter meaning and keep database predicates on the integer column.
		if (filter.Operator == "between")
		{
			var min = (int)decimal.Ceiling(filter.Min!.Value);
			var max = (int)decimal.Floor(filter.Max!.Value);
			return query.Where(student => student.RollNumber >= min && student.RollNumber <= max);
		}

		StudentDirectoryColumnFilters.TryParseRollNumberFilterValue(filter.Value, out var value);
		var integralPart = (int)value;
		return filter.Operator switch
		{
			"greaterThan" => query.Where(student => student.RollNumber > (int)decimal.Floor(value)),
			"lessThan" => query.Where(student => student.RollNumber < (int)decimal.Ceiling(value)),
			_ => value == integralPart ? query.Where(student => student.RollNumber == integralPart) : query.Where(_ => false)
		};
	}
}
