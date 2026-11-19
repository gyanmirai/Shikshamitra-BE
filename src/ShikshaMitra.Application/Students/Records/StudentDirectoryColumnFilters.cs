using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using Volo.Abp;

namespace ShikshaMitra.Students.Records;

public static class StudentDirectoryColumnFilters
{
	public static void Validate(IReadOnlyList<StudentDirectoryColumnFilterDto>? filters)
	{
		if (filters == null || filters.Count > 20) throw Invalid();
		foreach (var filter in filters)
		{
			if (filter == null) throw Invalid();
			if (filter.Column is "name" or "grNumber" or "classDivision")
			{
				if (filter.Operator is not ("contains" or "equals" or "startsWith" or "endsWith") ||
					string.IsNullOrWhiteSpace(filter.Value) || filter.Value.Length > 150) throw Invalid();
			}
			else if (filter.Column == "rollNo")
			{
				if (filter.Operator == "between")
				{
					if (!filter.Min.HasValue || !filter.Max.HasValue || filter.Min > filter.Max ||
						filter.Min < int.MinValue || filter.Max > int.MaxValue) throw Invalid();
				}
				else if (filter.Operator is not ("equals" or "greaterThan" or "lessThan") || !Number(filter.Value, out _)) throw Invalid();
			}
			else if (filter.Column == "status")
			{
				if (filter.Operator != "equals" || filter.Value is not ("6" or "7" or "8")) throw Invalid();
			}
			else throw Invalid();
		}
	}

	public static IQueryable<StudentDirectoryFilterRow> Apply(IQueryable<StudentDirectoryFilterRow> query,
		IReadOnlyList<StudentDirectoryColumnFilterDto> filters)
	{
		Validate(filters);
		foreach (var filter in filters)
		{
			if (filter.Column == "status")
			{
				var status = (StudentLifeCycleStatusEnum)int.Parse(filter.Value!, CultureInfo.InvariantCulture);
				query = query.Where(row => row.Status == status);
			}
			else if (filter.Column == "rollNo")
			{
				Number(filter.Value, out var value);
				query = filter.Operator switch
				{
					"equals" => query.Where(row => row.RollNo == value),
					"greaterThan" => query.Where(row => row.RollNo > value),
					"lessThan" => query.Where(row => row.RollNo < value),
					_ => query.Where(row => row.RollNo >= filter.Min && row.RollNo <= filter.Max)
				};
			}
			else
			{
				var row = Expression.Parameter(typeof(StudentDirectoryFilterRow), "row");
				var field = filter.Column switch { "name" => nameof(StudentDirectoryFilterRow.Name), "grNumber" => nameof(StudentDirectoryFilterRow.GrNumber), _ => nameof(StudentDirectoryFilterRow.ClassDivision) };
				var text = Expression.Call(Expression.Property(row, field), nameof(string.ToLower), Type.EmptyTypes);
				var term = Expression.Constant(filter.Value!.ToLowerInvariant());
				Expression predicate = filter.Operator == "equals" ? Expression.Equal(text, term) :
					Expression.Call(text, filter.Operator switch { "contains" => nameof(string.Contains), "startsWith" => nameof(string.StartsWith), _ => nameof(string.EndsWith) }, Type.EmptyTypes, term);
				query = query.Where(Expression.Lambda<Func<StudentDirectoryFilterRow, bool>>(predicate, row));
			}
		}
		return query;
	}

	private static bool Number(string? raw, out decimal value) =>
		decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= int.MinValue && value <= int.MaxValue;
	private static BusinessException Invalid() => new("Students:Records:InvalidDirectoryFilter");
}

public class StudentDirectoryFilterRow
{
	public Guid StudentId { get; set; }
	public string Name { get; set; } = "";
	public string GrNumber { get; set; } = "";
	public string ClassDivision { get; set; } = "";
	public decimal? RollNo { get; set; }
	public StudentLifeCycleStatusEnum Status { get; set; }
}
