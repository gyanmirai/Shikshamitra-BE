using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Transport;

[Authorize(ShikshaMitraSubModulePermissions.TransportStaffManagement)]
[RemoteService(false)]
public class TransportStaffAppService(
	IRepository<TransportStaff, Guid> staffRepository,
	IRepository<TransportRouteAssignment, Guid> assignmentRepository,
	IRepository<TransportRoute, Guid> routeRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<EducationMedium, Guid> academicMediumRepository,
	StudentScopeAccessService studentScopeAccessService
) : ApplicationService, ITransportStaffAppService
{
	public async Task<PagedResultDto<TransportStaffDto>> GetListAsync(TransportStaffListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetListAsync));

		var staffQuery = await staffRepository.GetQueryableAsync();
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var query =
			from staff in staffQuery
			join employee in employeeQuery on staff.EmployeeId equals employee.Id
			where staff.AcademicMediumId == input.AcademicMediumId
			select new { Staff = staff, Employee = employee };

		if (input.Role.HasValue)
		{
			query = query.Where(row => row.Staff.Role == input.Role.Value);
		}

		if (input.Status.HasValue)
		{
			query = query.Where(row => row.Staff.Status == input.Status.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			query = query.Where(row =>
				row.Employee.EmployeeCode.ToLower().Contains(search) ||
				(row.Employee.FirstName != null && row.Employee.FirstName.ToLower().Contains(search)) ||
				(row.Employee.MiddleName != null && row.Employee.MiddleName.ToLower().Contains(search)) ||
				(row.Employee.LastName != null && row.Employee.LastName.ToLower().Contains(search)) ||
				(row.Employee.MobileNo != null && row.Employee.MobileNo.ToLower().Contains(search)) ||
				(row.Staff.LicenseNo != null && row.Staff.LicenseNo.ToLower().Contains(search)));
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderBy(row => row.Employee.FirstName)
				.ThenBy(row => row.Employee.LastName)
				.Skip(input.SkipCount)
				.Take(input.NormalizedPageSize));

		var assignments = await GetCurrentAssignmentsForStaffAsync(rows.Select(row => row.Staff.Id).ToList());
		return new PagedResultDto<TransportStaffDto>(
			totalCount,
			rows.Select(row => MapStaff(row.Staff, row.Employee, assignments)).ToList());
	}

	public async Task<TransportStaffDto> GetAsync(Guid staffId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetAsync));
		var staff = await GetStaffForMediumAsync(staffId, academicMediumId);
		var employee = await GetEmployeeAsync(staff.EmployeeId);
		var assignments = await GetCurrentAssignmentsForStaffAsync([staff.Id]);
		return MapStaff(staff, employee, assignments);
	}

	public async Task<TransportStaffDto> CreateAsync(Guid academicMediumId, CreateUpdateTransportStaffDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(CreateAsync));
		ValidateStaffInput(input);
		var employee = await GetActiveEmployeeAsync(input.EmployeeId);
		await EnsureStaffIsUniqueAsync(academicMediumId, input.EmployeeId, input.Role);

		var staff = new TransportStaff(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicMediumId,
			input.EmployeeId,
			input.Role,
			GetLicenseNo(input),
			GetLicenseExpiryDate(input),
			input.ExperienceYears,
			input.Remarks);

		await staffRepository.InsertAsync(staff, autoSave: true);
		return MapStaff(staff, employee, new Dictionary<Guid, TransportStaffCurrentAssignment>());
	}

	public async Task<TransportStaffDto> UpdateAsync(Guid staffId, Guid academicMediumId, CreateUpdateTransportStaffDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(UpdateAsync));
		ValidateStaffInput(input);
		var staff = await GetStaffForMediumAsync(staffId, academicMediumId);
		var employee = await GetActiveEmployeeAsync(input.EmployeeId);
		await EnsureStaffIsUniqueAsync(academicMediumId, input.EmployeeId, input.Role, staffId);

		staff.UpdateDetails(
			input.EmployeeId,
			input.Role,
			GetLicenseNo(input),
			GetLicenseExpiryDate(input),
			input.ExperienceYears,
			input.Remarks);

		await staffRepository.UpdateAsync(staff, autoSave: true);
		var assignments = await GetCurrentAssignmentsForStaffAsync([staff.Id]);
		return MapStaff(staff, employee, assignments);
	}

	public async Task<TransportStaffDto> ChangeStatusAsync(Guid staffId, Guid academicMediumId, ChangeTransportStaffStatusDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(ChangeStatusAsync));
		var staff = await GetStaffForMediumAsync(staffId, academicMediumId);

		if (input.Status != TransportStaffStatus.Active)
		{
			var currentAssignment = await FindCurrentAssignmentForStaffAsync(staff.Id, academicMediumId);
			if (currentAssignment != null)
			{
				throw new BusinessException(TransportErrorCodes.RouteAssignmentConflict)
					.WithData("StaffId", staffId)
					.WithData("RouteId", currentAssignment.RouteId);
			}
		}

		staff.ChangeStatus(input.Status, input.Remarks);
		await staffRepository.UpdateAsync(staff, autoSave: true);
		var employee = await GetEmployeeAsync(staff.EmployeeId);
		return MapStaff(staff, employee, new Dictionary<Guid, TransportStaffCurrentAssignment>());
	}

	public async Task<List<TransportOptionDto>> GetOptionsAsync(TransportStaffOptionsRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetOptionsAsync));

		var staffQuery = await staffRepository.GetQueryableAsync();
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var query =
			from staff in staffQuery
			join employee in employeeQuery on staff.EmployeeId equals employee.Id
			where staff.AcademicMediumId == input.AcademicMediumId && staff.Status == TransportStaffStatus.Active
			select new { Staff = staff, Employee = employee };

		if (input.Role.HasValue)
		{
			query = query.Where(row => row.Staff.Role == input.Role.Value);
		}

		if (input.OnlyAssignable)
		{
			var assignedStaffIds = await GetAssignedStaffIdsAsync(input.AcademicMediumId);
			query = query.Where(row => !assignedStaffIds.Contains(row.Staff.Id));
		}

		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderBy(row => row.Employee.FirstName)
				.ThenBy(row => row.Employee.LastName));

		return rows.Select(row =>
		{
			var name = GetEmployeeName(row.Employee);
			var code = row.Employee.EmployeeCode;
			var label = string.IsNullOrWhiteSpace(code) ? name : $"{name} ({code})";
			return new TransportOptionDto
			{
				Id = row.Staff.Id,
				Label = label,
				Value = row.Staff.Id.ToString("D"),
				Name = name,
				Code = code,
				Description = row.Staff.Role.ToString()
			};
		}).ToList();
	}

	private async Task<TransportStaff> GetStaffForMediumAsync(Guid staffId, Guid academicMediumId)
	{
		var staff = await staffRepository.FindAsync(staffId);
		if (staff == null || staff.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportStaff), staffId);
		}

		return staff;
	}

	private async Task<Employee> GetEmployeeAsync(Guid employeeId)
	{
		var employee = await employeeRepository.FindAsync(employeeId);
		if (employee == null)
		{
			throw new BusinessException(TransportErrorCodes.StaffEmployeeNotFound).WithData("EmployeeId", employeeId);
		}

		return employee;
	}

	private async Task<Employee> GetActiveEmployeeAsync(Guid employeeId)
	{
		var employee = await GetEmployeeAsync(employeeId);
		if (employee.EmployeeStatusType.HasValue && employee.EmployeeStatusType.Value != EmployeeStatusType.Active)
		{
			throw new BusinessException(TransportErrorCodes.StaffEmployeeInactive).WithData("EmployeeId", employeeId);
		}

		return employee;
	}

	private async Task EnsureCanUseMediumAsync(Guid academicMediumId, string operation)
	{
		await academicMediumRepository.GetAsync(academicMediumId);
		await studentScopeAccessService.EnsureCanAccessMediumAsync(academicMediumId, operation);
	}

	private async Task EnsureStaffIsUniqueAsync(Guid academicMediumId, Guid employeeId, TransportStaffRole role, Guid? excludingStaffId = null)
	{
		var exists = await staffRepository.AnyAsync(staff =>
			staff.AcademicMediumId == academicMediumId &&
			staff.EmployeeId == employeeId &&
			staff.Role == role &&
			(!excludingStaffId.HasValue || staff.Id != excludingStaffId.Value));

		if (exists)
		{
			throw new BusinessException(TransportErrorCodes.StaffAlreadyExists)
				.WithData("EmployeeId", employeeId)
				.WithData("Role", role.ToString())
				.WithData("AcademicMediumId", academicMediumId);
		}
	}

	private async Task<TransportRouteAssignment?> FindCurrentAssignmentForStaffAsync(Guid staffId, Guid academicMediumId)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(assignment =>
			assignment.AcademicMediumId == academicMediumId &&
			assignment.EffectiveTo == null &&
			(assignment.DriverStaffId == staffId || assignment.ConductorStaffId == staffId)));
	}

	private async Task<HashSet<Guid>> GetAssignedStaffIdsAsync(Guid academicMediumId)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Where(assignment =>
			assignment.AcademicMediumId == academicMediumId &&
			assignment.EffectiveTo == null));

		return rows
			.SelectMany(assignment => new[] { assignment.DriverStaffId, assignment.ConductorStaffId ?? Guid.Empty })
			.Where(id => id != Guid.Empty)
			.ToHashSet();
	}

	private async Task<IReadOnlyDictionary<Guid, TransportStaffCurrentAssignment>> GetCurrentAssignmentsForStaffAsync(List<Guid> staffIds)
	{
		if (staffIds.Count == 0)
		{
			return new Dictionary<Guid, TransportStaffCurrentAssignment>();
		}

		var assignmentQuery = await assignmentRepository.GetQueryableAsync();
		var routeQuery = await routeRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(
			from assignment in assignmentQuery
			join route in routeQuery on assignment.RouteId equals route.Id
			where assignment.EffectiveTo == null &&
				(staffIds.Contains(assignment.DriverStaffId) ||
					(assignment.ConductorStaffId.HasValue && staffIds.Contains(assignment.ConductorStaffId.Value)))
			select new { Assignment = assignment, Route = route });

		var result = new Dictionary<Guid, TransportStaffCurrentAssignment>();
		foreach (var row in rows)
		{
			result[row.Assignment.DriverStaffId] = new TransportStaffCurrentAssignment(row.Route.Id, row.Route.RouteName);
			if (row.Assignment.ConductorStaffId.HasValue)
			{
				result[row.Assignment.ConductorStaffId.Value] = new TransportStaffCurrentAssignment(row.Route.Id, row.Route.RouteName);
			}
		}

		return result;
	}

	private static void ValidateStaffInput(CreateUpdateTransportStaffDto input)
	{
		if (input.EmployeeId == Guid.Empty)
		{
			throw new UserFriendlyException("Employee is required.");
		}

		if (!Enum.IsDefined(input.Role))
		{
			throw new BusinessException(TransportErrorCodes.StaffRoleInvalid).WithData("Role", input.Role);
		}

		if (input.Role == TransportStaffRole.Driver)
		{
			if (string.IsNullOrWhiteSpace(GetLicenseNo(input)))
			{
				throw new UserFriendlyException("Driver license no. is required.");
			}

			if (!GetLicenseExpiryDate(input).HasValue)
			{
				throw new UserFriendlyException("Driver license expiry date is required.");
			}
		}

		if (input.ExperienceYears.HasValue && input.ExperienceYears.Value < 0)
		{
			throw new UserFriendlyException("Experience years cannot be negative.");
		}
	}

	private static string? GetLicenseNo(CreateUpdateTransportStaffDto input) =>
		string.IsNullOrWhiteSpace(input.LicenseNo) ? input.LicenceNo : input.LicenseNo;

	private static DateTime? GetLicenseExpiryDate(CreateUpdateTransportStaffDto input) =>
		input.LicenseExpiryDate ?? input.LicenceExpiryDate;

	private static TransportStaffDto MapStaff(
		TransportStaff staff,
		Employee employee,
		IReadOnlyDictionary<Guid, TransportStaffCurrentAssignment> assignments)
	{
		assignments.TryGetValue(staff.Id, out var assignment);
		var name = GetEmployeeName(employee);
		var employeeCode = employee.EmployeeCode ?? string.Empty;
		return new TransportStaffDto
		{
			Id = staff.Id,
			AcademicMediumId = staff.AcademicMediumId,
			EmployeeId = staff.EmployeeId,
			EmployeeCode = employeeCode,
			EmployeeIdentifier = employeeCode,
			Name = name,
			Email = employee.Email,
			PhoneNo = employee.MobileNo,
			Role = staff.Role.ToString(),
			RoleId = staff.Role,
			LicenseNo = staff.LicenseNo,
			LicenceNo = staff.LicenseNo,
			LicenseExpiryDate = staff.LicenseExpiryDate,
			LicenceExpiryDate = staff.LicenseExpiryDate,
			LicenceExpiry = staff.LicenseExpiryDate?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
			ExperienceYears = staff.ExperienceYears,
			Experience = staff.ExperienceYears.HasValue ? $"{staff.ExperienceYears.Value} years" : string.Empty,
			Status = staff.Status,
			StatusName = staff.Status == TransportStaffStatus.OnLeave ? "On Leave" : staff.Status.ToString(),
			AssignedRoute = assignment?.RouteName,
			AssignedRouteId = assignment?.RouteId,
			Remarks = staff.Remarks,
			CreationTime = staff.CreationTime,
			CreatorId = staff.CreatorId,
			LastModificationTime = staff.LastModificationTime,
			LastModifierId = staff.LastModifierId
		};
	}

	private static string GetEmployeeName(Employee employee)
	{
		var name = employee.EmployeeName;
		return string.IsNullOrWhiteSpace(name) ? employee.EmployeeCode : name;
	}

	private sealed record TransportStaffCurrentAssignment(Guid RouteId, string RouteName);
}
