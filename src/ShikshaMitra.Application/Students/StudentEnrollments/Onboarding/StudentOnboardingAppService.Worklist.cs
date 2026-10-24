using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	private sealed class WorklistRow
	{
		public StudentOnboardingCase Case { get; set; } = null!;
		public string StudentName { get; set; } = string.Empty;
		public string OwnerName { get; set; } = string.Empty;
		public string StandardName { get; set; } = string.Empty;
		public string DivisionName { get; set; } = string.Empty;
	}

	// The input query is already tenant- and academically scoped. Keep filtering, counting and paging in SQL.
	private async Task<IQueryable<WorklistRow>> GetWorklistQueryAsync(
		IQueryable<StudentOnboardingCase> cases, StudentOnboardingListInput input)
	{
		if (input.PlacementDivisionId.HasValue)
			cases = cases.Where(item => item.DivisionId == input.PlacementDivisionId.Value);
		var students = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		var owners = await LazyServiceProvider.LazyGetRequiredService<IRepository<IdentityUser, Guid>>().GetQueryableAsync();
		var divisions = await divisionRepository.GetQueryableAsync();
		var standards = await standardRepository.GetQueryableAsync();
		var query = from item in cases
			join student in students on item.StudentId equals student.Id into studentGroup
			from student in studentGroup.DefaultIfEmpty()
			join owner in owners on item.AssignedToUserId equals (Guid?)owner.Id into ownerGroup
			from owner in ownerGroup.DefaultIfEmpty()
			join division in divisions on item.DivisionId equals (Guid?)division.Id into divisionGroup
			from division in divisionGroup.DefaultIfEmpty()
			join standard in standards on item.IntendedStandardId equals standard.Id into standardGroup
			from standard in standardGroup.DefaultIfEmpty()
			select new WorklistRow
			{
				Case = item,
				StudentName = student == null ? "" : ((student.FirstName ?? "") +
					((student.MiddleName ?? "").Trim() == "" ? "" : " " + student.MiddleName) +
					((student.LastName ?? "").Trim() == "" ? "" : " " + student.LastName)).Trim(),
				OwnerName = owner == null ? "Not assigned" :
					((owner.Name ?? "").Trim() == "" && (owner.Surname ?? "").Trim() == "" ? owner.UserName :
					((owner.Name ?? "") + " " + (owner.Surname ?? "")).Trim()),
				StandardName = standard == null ? "" : standard!.Name,
				DivisionName = division == null ? "" : division!.Name
			};
		query = FilterWorklistText(query, input.StudentNameFilter, input.StudentNameOperator, false);

		if (input.RollNumberFilter.HasValue || input.RollNumberMin.HasValue || input.RollNumberMax.HasValue)
		{
			var operation = input.RollNumberOperator ?? "equals";
			if (operation == "between")
			{
				// The contract validator guarantees both bounds before this query is constructed.
				var minimum = input.RollNumberMin.GetValueOrDefault();
				var maximum = input.RollNumberMax.GetValueOrDefault();
				query = query.Where(row => row.Case.RollNumber >= minimum && row.Case.RollNumber <= maximum);
			}
			else
			{
				var value = input.RollNumberFilter.GetValueOrDefault();
				query = operation switch
				{
					"equals" => query.Where(row => row.Case.RollNumber == value),
					"greaterThan" => query.Where(row => row.Case.RollNumber > value),
					"lessThan" => query.Where(row => row.Case.RollNumber < value),
					_ => throw OnboardingRuleConfiguration.Invalid("Choose a supported Roll number filter.")
				};
			}
		}
		return query;
	}

	private static IQueryable<WorklistRow> FilterWorklistText(
		IQueryable<WorklistRow> query, string? filter, string? operation, bool owner)
	{
		if (string.IsNullOrWhiteSpace(filter)) return query;
		var value = filter.Trim().ToLowerInvariant();
		return (owner, operation ?? "contains") switch
		{
			(false, "contains") => query.Where(row => row.StudentName.ToLower().Contains(value)),
			(false, "equals") => query.Where(row => row.StudentName.ToLower() == value),
			(false, "startsWith") => query.Where(row => row.StudentName.ToLower().StartsWith(value)),
			(false, "endsWith") => query.Where(row => row.StudentName.ToLower().EndsWith(value)),
			(true, "contains") => query.Where(row => row.OwnerName.ToLower().Contains(value)),
			(true, "equals") => query.Where(row => row.OwnerName.ToLower() == value),
			(true, "startsWith") => query.Where(row => row.OwnerName.ToLower().StartsWith(value)),
			(true, "endsWith") => query.Where(row => row.OwnerName.ToLower().EndsWith(value)),
			_ => throw OnboardingRuleConfiguration.Invalid("Choose a supported student or owner name filter.")
		};
	}

	private static IOrderedQueryable<WorklistRow> SortWorklist(IQueryable<WorklistRow> query, string? sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
			return query.OrderByDescending(row => row.Case.CreationTime).ThenBy(row => row.Case.Id);
		var parts = sorting.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var descending = parts.Length == 2 && string.Equals(parts[1], "desc", StringComparison.OrdinalIgnoreCase);
		var ordered = (parts[0], descending) switch
		{
			("StudentName", false) => query.OrderBy(row => row.StudentName),
			("StudentName", true) => query.OrderByDescending(row => row.StudentName),
			("OwnerName" or "AssignedToUserName", false) => query.OrderBy(row => row.OwnerName),
			("OwnerName" or "AssignedToUserName", true) => query.OrderByDescending(row => row.OwnerName),
			("DivisionName", false) => query.OrderBy(row => row.DivisionName),
			("DivisionName", true) => query.OrderByDescending(row => row.DivisionName),
			("Placement", false) => query.OrderBy(row => row.StandardName).ThenBy(row => row.DivisionName),
			("Placement", true) => query.OrderByDescending(row => row.StandardName).ThenByDescending(row => row.DivisionName),
			("RollNumber", false) => query.OrderBy(row => row.Case.RollNumber == null).ThenBy(row => row.Case.RollNumber),
			("RollNumber", true) => query.OrderBy(row => row.Case.RollNumber == null).ThenByDescending(row => row.Case.RollNumber),
			("Status", false) => query.OrderBy(row => row.Case.Status),
			("Status", true) => query.OrderByDescending(row => row.Case.Status),
			_ => throw OnboardingRuleConfiguration.Invalid("Choose a supported worklist sort.")
		};
		return ordered.ThenBy(row => row.Case.Id);
	}
}
