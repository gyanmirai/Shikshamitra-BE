using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees;

[RemoteService(false)]
public class EmployeeLegacyListReader(
	IEmployeeRepository employeeRepository,
	EmployeeOrganizationScopeResolver organizationScopeResolver)
	: ApplicationService
{
	public async Task<EmployeeListResponse> GetFilteredListAsync(GetEmployeeListInput input)
	{
		var queryable = await employeeRepository.GetQueryableAsync();

		if (!string.IsNullOrEmpty(input.Status) &&
		    Enum.TryParse<EmployeeStatusType>(input.Status, true, out var statusType))
		{
			queryable = queryable.Where(e => e.EmployeeStatusType == statusType);
		}

		if (string.Equals(input.ReportingManagerStatus, "nonActive", StringComparison.OrdinalIgnoreCase))
		{
			queryable = queryable.Where(e =>
				e.EmployeeStatusType == EmployeeStatusType.Active &&
				e.ReportingManagerId.HasValue &&
				e.ReportingManager != null &&
				e.ReportingManager.EmployeeStatusType.HasValue &&
				e.ReportingManager.EmployeeStatusType.Value != EmployeeStatusType.Active);
		}

		if (input.IsTeaching.HasValue)
		{
			queryable = input.IsTeaching.Value
				? queryable.Where(e => e.EmployeeGroup == EmployeeGroupType.Teaching)
				: queryable.Where(e => e.EmployeeGroup != EmployeeGroupType.Teaching);
		}

		if (input.RoleId.HasValue)
		{
			queryable = queryable.Where(e => e.RoleId == input.RoleId.Value);
		}

		if (input.MediumStageId.HasValue)
		{
			var matchingUserIds = await organizationScopeResolver.GetUserIdsWithAnyStageAsync([input.MediumStageId.Value]);
			queryable = queryable.Where(e => e.UserId.HasValue && matchingUserIds.Contains(e.UserId.Value));
		}

		if (!string.IsNullOrWhiteSpace(input.Designation))
		{
			var designation = input.Designation.Trim().ToLower();
			queryable = queryable.Where(e => e.Designation != null && e.Designation.ToLower().Contains(designation));
		}

		queryable = ApplySearchFilter(queryable, input.Search);

		var totalCount = await AsyncExecuter.CountAsync(queryable);
		var employees = await AsyncExecuter.ToListAsync(
			queryable
				.OrderBy(e => e.FirstName)
				.ThenBy(e => e.LastName)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));
		var organizationsByUserId = await organizationScopeResolver.GetOrganizationsByUserIdsAsync(
			employees.Select(x => x.UserId));

		var pageSize = input.MaxResultCount > 0 ? input.MaxResultCount : 10;
		var currentPage = (input.SkipCount / pageSize) + 1;
		var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

		return new EmployeeListResponse
		{
			TotalCount = totalCount,
			CurrentPage = currentPage,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = employees.Select(employee => MapToEmployeeSummary(employee, organizationsByUserId)).ToList()
		};
	}

	private static IQueryable<Employee> ApplySearchFilter(IQueryable<Employee> queryable, string? search)
	{
		if (string.IsNullOrWhiteSpace(search))
		{
			return queryable;
		}

		var searchTerm = search.Trim().ToLower();

		return queryable.Where(e =>
			(e.FirstName != null && e.FirstName.ToLower().Contains(searchTerm)) ||
			(e.LastName != null && e.LastName.ToLower().Contains(searchTerm)) ||
			(e.MiddleName != null && e.MiddleName.ToLower().Contains(searchTerm)) ||
			(e.EmployeeCode != null && e.EmployeeCode.ToLower().Contains(searchTerm)) ||
			(e.Email != null && e.Email.ToLower().Contains(searchTerm)) ||
			(e.MobileNo != null && e.MobileNo.Contains(searchTerm)) ||
			(e.Designation != null && e.Designation.ToLower().Contains(searchTerm)));
	}

	private static EmployeeSummary MapToEmployeeSummary(
		Employee employee,
		IReadOnlyDictionary<Guid, List<EmployeeOrganizationDto>> organizationsByUserId)
	{
		var organizations = employee.UserId.HasValue &&
			organizationsByUserId.TryGetValue(employee.UserId.Value, out var scopedOrganizations)
				? scopedOrganizations
				: [];

		return new EmployeeSummary
		{
			Id = employee.Id,
			RoleName = employee.Role?.Name,
			FullName = employee.EmployeeName,
			ThumbnailPicture = employee.ProfilePictureBlobName,
			EmployeeIdentifier = employee.EmployeeCode,
			Dob = employee.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
			Gender = employee.GenderType?.ToString(),
			Doj = employee.DateOfJoining?.ToDateTime(TimeOnly.MinValue),
			Email = employee.Email,
			MobileNo = employee.MobileNo,
			Department = employee.Department,
			Designation = employee.Designation,
			BloodGroup = employee.BloodGroup,
			Address = string.Join(", ", new[] { employee.Address1, employee.Address2, employee.Address3 }.Where(x => !string.IsNullOrWhiteSpace(x))),
			IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
			Shift = employee.Shift != null ? new ShiftInfo
			{
				Id = employee.Shift.Id,
				Name = employee.Shift.ShiftName ?? string.Empty,
				StartTime = employee.Shift.StartTime.ToString("HH:mm:ss"),
				EndTime = employee.Shift.EndTime.ToString("HH:mm:ss")
			} : null,
			Organizations = organizations,
			OrganizationNames = organizations
				.Select(x => x.Name)
				.Distinct()
				.OrderBy(x => x)
				.ToList(),
			Status = (int)(employee.EmployeeStatusType ?? EmployeeStatusType.Active)
		};
	}
}
