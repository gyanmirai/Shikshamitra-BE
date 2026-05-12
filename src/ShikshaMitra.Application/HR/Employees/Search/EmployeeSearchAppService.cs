using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.Search;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
public class EmployeeSearchAppService(
	IEmployeeRepository employeeRepository)
	: ApplicationService, IEmployeeSearchAppService
{
	public async Task<EmployeeListResponse> GetFilteredListAsync(GetEmployeeListInput input)
	{
		var queryable = await employeeRepository.GetQueryableAsync();
		queryable = await ApplyVisibilityScopeAsync(queryable);

		if (!string.IsNullOrEmpty(input.Status))
		{
			if (Enum.TryParse<EmployeeStatusType>(input.Status, true, out var statusType))
			{
				queryable = queryable.Where(e => e.EmployeeStatusType == statusType);
			}
		}

		if (input.IsTeaching.HasValue)
		{
			if (input.IsTeaching.Value)
			{
				queryable = queryable.Where(e => e.EmployeeGroup == EmployeeGroupType.Teaching);
			}
			else
			{
				queryable = queryable.Where(e => e.EmployeeGroup != EmployeeGroupType.Teaching);
			}
		}

		if (input.RoleId.HasValue)
		{
			queryable = queryable.Where(e => e.RoleId == input.RoleId.Value);
		}

		queryable = ApplySearchFilter(queryable, input.Search);

		var totalCount = await AsyncExecuter.CountAsync(queryable);

		queryable = queryable.OrderBy(e => e.FirstName).ThenBy(e => e.LastName);

		var employees = await AsyncExecuter.ToListAsync(
			queryable.Skip(input.SkipCount).Take(input.MaxResultCount));

		var pageSize = input.MaxResultCount > 0 ? input.MaxResultCount : 10;
		var currentPage = (input.SkipCount / pageSize) + 1;
		var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

		var items = employees.Select(MapToEmployeeSummary).ToList();

		return new EmployeeListResponse
		{
			TotalCount = totalCount,
			CurrentPage = currentPage,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = items
		};
	}

	private async Task<IQueryable<Employee>> ApplyVisibilityScopeAsync(IQueryable<Employee> queryable)
	{
		if (IsPrivilegedUser())
		{
			return queryable;
		}

		var currentUserId = CurrentUser.Id;
		if (!currentUserId.HasValue)
		{
			return queryable.Where(_ => false);
		}

		var actorEmployee = await employeeRepository.GetEmployeeByUserIdAsync(currentUserId.Value);
		if (actorEmployee == null)
		{
			return queryable.Where(_ => false);
		}

		var reportees = await employeeRepository.GetEmployeesByManagerAsync(actorEmployee.Id);
		if (reportees.Count == 0)
		{
			return queryable.Where(e => e.Id == actorEmployee.Id);
		}

		var reporteeIds = reportees.Select(e => e.Id).ToList();
		return queryable.Where(e => reporteeIds.Contains(e.Id));
	}

	private bool IsPrivilegedUser()
	{
		return CurrentUser.IsInRole("admin")
			|| CurrentUser.IsInRole("Administrator")
			|| CurrentUser.IsInRole("HR Admin")
			|| CurrentUser.IsInRole("HR Manager");
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
			(e.MobileNo != null && e.MobileNo.Contains(searchTerm)));
	}

	private static EmployeeSummary MapToEmployeeSummary(Employee employee)
	{
		return new EmployeeSummary
		{
			Id = employee.Id,
			UserId = employee.UserId,
			RoleName = employee.Role?.Name,
			FullName = employee.EmployeeName,
			ThumbnailPicture = employee.ProfilePictureBlobName,
			EmployeeIdentifier = employee.EmployeeCode,
			Dob = employee.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
			Gender = employee.GenderType?.ToString(),
			Doj = employee.DateOfJoining?.ToDateTime(TimeOnly.MinValue),
			Email = employee.Email,
			MobileNo = employee.MobileNo,
			IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
			Shift = employee.Shift != null ? new ShiftInfo
			{
				Id = employee.Shift.Id,
				Name = employee.Shift.ShiftName ?? string.Empty,
				StartTime = employee.Shift.StartTime.ToString("HH:mm:ss"),
				EndTime = employee.Shift.EndTime.ToString("HH:mm:ss")
			} : null,
			Status = (int)(employee.EmployeeStatusType ?? EmployeeStatusType.Active)
		};
	}

}
