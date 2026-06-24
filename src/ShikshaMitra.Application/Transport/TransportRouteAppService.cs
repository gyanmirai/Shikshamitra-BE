using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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

[Authorize(ShikshaMitraSubModulePermissions.RouteManagement)]
[RemoteService(false)]
public class TransportRouteAppService(
	IRepository<TransportRoute, Guid> routeRepository,
	IRepository<TransportStop, Guid> stopRepository,
	IRepository<TransportRouteStop, Guid> routeStopRepository,
	IRepository<TransportVehicle, Guid> vehicleRepository,
	IRepository<TransportStaff, Guid> staffRepository,
	IRepository<TransportRouteAssignment, Guid> assignmentRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<EducationMedium, Guid> academicMediumRepository,
	StudentScopeAccessService studentScopeAccessService
) : ApplicationService, ITransportRouteAppService
{
	public async Task<PagedResultDto<TransportRouteDto>> GetListAsync(TransportRouteListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetListAsync));

		var query = await routeRepository.GetQueryableAsync();
		query = query.Where(route => route.AcademicMediumId == input.AcademicMediumId);

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			query = query.Where(route =>
				route.RouteName.ToLower().Contains(search) ||
				route.RouteCode.ToLower().Contains(search) ||
				route.StartPoint.ToLower().Contains(search) ||
				route.EndPoint.ToLower().Contains(search));
		}

		if (input.Status.HasValue)
		{
			query = query.Where(route => route.Status == input.Status.Value);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var routes = await AsyncExecuter.ToListAsync(
			query
				.OrderBy(route => route.RouteName)
				.Skip(input.SkipCount)
				.Take(input.NormalizedPageSize));

		var routeStops = await GetRouteStopsForRoutesAsync(routes.Select(route => route.Id).ToList());
		return new PagedResultDto<TransportRouteDto>(
			totalCount,
			routes.Select(route => MapRoute(route, routeStops)).ToList());
	}

	public async Task<TransportRouteDto> GetAsync(Guid routeId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetAsync));
		var route = await GetRouteForMediumAsync(routeId, academicMediumId);
		var stops = await GetStopsAsync(routeId, academicMediumId);
		var dto = MapRoute(route, stops);
		dto.VehicleAssignment = await GetVehicleAssignmentAsync(routeId, academicMediumId);
		return dto;
	}

	public async Task<TransportRouteDto> CreateAsync(Guid academicMediumId, CreateUpdateTransportRouteDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(CreateAsync));
		ValidateRouteInput(input);
		await EnsureRouteCodeIsUniqueAsync(academicMediumId, input.RouteCode);

		var route = new TransportRoute(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicMediumId,
			input.RouteName,
			input.RouteCode,
			input.RouteType,
			input.Distance,
			input.Duration,
			input.Description,
			input.StartPoint,
			input.EndPoint,
			input.PickupSchedule?.StartTime,
			input.PickupSchedule?.EndTime,
			input.DropoffSchedule?.StartTime,
			input.DropoffSchedule?.EndTime);

		await routeRepository.InsertAsync(route, autoSave: true);
		return MapRoute(route, new List<TransportRouteStopDto>());
	}

	public async Task<TransportRouteDto> UpdateAsync(Guid routeId, Guid academicMediumId, CreateUpdateTransportRouteDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(UpdateAsync));
		ValidateRouteInput(input);

		var route = await GetRouteForMediumAsync(routeId, academicMediumId);
		await EnsureRouteCodeIsUniqueAsync(academicMediumId, input.RouteCode, routeId);

		route.UpdateDetails(
			input.RouteName,
			input.RouteCode,
			input.RouteType,
			input.Distance,
			input.Duration,
			input.Description,
			input.StartPoint,
			input.EndPoint,
			input.PickupSchedule?.StartTime,
			input.PickupSchedule?.EndTime,
			input.DropoffSchedule?.StartTime,
			input.DropoffSchedule?.EndTime);

		await routeRepository.UpdateAsync(route, autoSave: true);
		return MapRoute(route, await GetStopsAsync(routeId, academicMediumId));
	}

	public async Task<List<TransportRouteStopDto>> GetStopsAsync(Guid routeId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetStopsAsync));
		await GetRouteForMediumAsync(routeId, academicMediumId);

		var routeStopQuery = await routeStopRepository.GetQueryableAsync();
		var stopQuery = await stopRepository.GetQueryableAsync();

		var rows = await AsyncExecuter.ToListAsync(
			from routeStop in routeStopQuery
			join stop in stopQuery on routeStop.StopId equals stop.Id
			where routeStop.RouteId == routeId && stop.AcademicMediumId == academicMediumId
			orderby routeStop.StopSequence
			select new { RouteStop = routeStop, Stop = stop });

		return rows.Select(row => MapRouteStop(row.RouteStop, row.Stop)).ToList();
	}

	public async Task<List<TransportRouteStopDto>> AssignStopsAsync(Guid routeId, Guid academicMediumId, AssignTransportRouteStopsDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(AssignStopsAsync));
		await GetRouteForMediumAsync(routeId, academicMediumId);
		ValidateRouteStops(input);

		var stopIds = input.Stops.Select(stop => stop.StopId).Distinct().ToList();
		var stops = await stopRepository.GetListAsync(stop =>
			stop.AcademicMediumId == academicMediumId &&
			stopIds.Contains(stop.Id));

		if (stops.Count != stopIds.Count)
		{
			throw new BusinessException(TransportErrorCodes.RouteStopInvalid)
				.WithData("RouteId", routeId);
		}

		var existing = await routeStopRepository.GetListAsync(routeStop => routeStop.RouteId == routeId);
		foreach (var routeStop in existing)
		{
			await routeStopRepository.DeleteAsync(routeStop, autoSave: false);
		}

		foreach (var inputStop in input.Stops.OrderBy(stop => stop.Order))
		{
			await routeStopRepository.InsertAsync(
				new TransportRouteStop(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					routeId,
					inputStop.StopId,
					inputStop.Order,
					inputStop.ExpectedArrivalTime,
					inputStop.ExpectedDepartureTime,
					inputStop.DistanceFromPrevious),
				autoSave: false);
		}

		await CurrentUnitOfWork!.SaveChangesAsync();
		return await GetStopsAsync(routeId, academicMediumId);
	}

	public async Task<TransportRouteDto> ChangeStatusAsync(Guid routeId, Guid academicMediumId, ChangeTransportStatusDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(ChangeStatusAsync));
		var route = await GetRouteForMediumAsync(routeId, academicMediumId);
		if (input.Status != TransportRecordStatus.Active)
		{
			await CloseCurrentAssignmentAsync(routeId, academicMediumId, input.Remarks ?? "Route deactivated");
		}
		route.ChangeStatus(input.Status);
		await routeRepository.UpdateAsync(route, autoSave: true);
		return MapRoute(route, await GetStopsAsync(routeId, academicMediumId));
	}

	public async Task<TransportRouteAssignmentDto?> GetVehicleAssignmentAsync(Guid routeId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetVehicleAssignmentAsync));
		await GetRouteForMediumAsync(routeId, academicMediumId);
		var assignment = await FindCurrentAssignmentForRouteAsync(routeId, academicMediumId);
		return assignment == null ? null : (await MapAssignmentsAsync([assignment])).Single();
	}

	public async Task<TransportRouteAssignmentDto> AssignVehicleAsync(Guid routeId, Guid academicMediumId, AssignTransportRouteVehicleDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(AssignVehicleAsync));
		ValidateAssignmentInput(input);
		var route = await GetRouteForMediumAsync(routeId, academicMediumId);
		if (route.Status != TransportRecordStatus.Active)
		{
			throw new BusinessException(TransportErrorCodes.RouteAssignmentInvalid).WithData("RouteId", routeId);
		}

		var vehicle = await GetVehicleForMediumAsync(input.VehicleId, academicMediumId);
		if (vehicle.Status != TransportVehicleStatus.Active)
		{
			throw new BusinessException(TransportErrorCodes.RouteAssignmentInvalid).WithData("VehicleId", input.VehicleId);
		}

		var driver = await GetStaffForAssignmentAsync(input.DriverStaffId, academicMediumId, TransportStaffRole.Driver);
		TransportStaff? conductor = null;
		if (input.ConductorStaffId.HasValue)
		{
			conductor = await GetStaffForAssignmentAsync(input.ConductorStaffId.Value, academicMediumId, TransportStaffRole.Conductor);
		}

		await EnsureAssignmentResourcesAreAvailableAsync(routeId, academicMediumId, input.VehicleId, driver.Id, conductor?.Id);
		await CloseCurrentAssignmentAsync(routeId, academicMediumId, "Reassigned");

		var assignment = new TransportRouteAssignment(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicMediumId,
			routeId,
			input.VehicleId,
			driver.Id,
			conductor?.Id,
			input.EffectiveFrom ?? DateTime.UtcNow,
			input.Remarks);

		await assignmentRepository.InsertAsync(assignment, autoSave: true);
		return (await MapAssignmentsAsync([assignment])).Single();
	}

	public async Task DeleteVehicleAssignmentAsync(Guid routeId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(DeleteVehicleAssignmentAsync));
		await GetRouteForMediumAsync(routeId, academicMediumId);
		await CloseCurrentAssignmentAsync(routeId, academicMediumId, "Unassigned");
		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	public async Task<PagedResultDto<TransportVehicleHistoryRowDto>> GetVehicleHistoryAsync(Guid routeId, TransportVehicleMaintenanceListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetVehicleHistoryAsync));
		await GetRouteForMediumAsync(routeId, input.AcademicMediumId);
		return await GetRouteAssignmentHistoryAsync(routeId, input);
	}

	public async Task<PagedResultDto<TransportVehicleHistoryRowDto>> GetDriverConductorHistoryAsync(Guid routeId, TransportVehicleMaintenanceListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetDriverConductorHistoryAsync));
		await GetRouteForMediumAsync(routeId, input.AcademicMediumId);
		return await GetRouteAssignmentHistoryAsync(routeId, input);
	}

	private async Task<TransportRoute> GetRouteForMediumAsync(Guid routeId, Guid academicMediumId)
	{
		var route = await routeRepository.FindAsync(routeId);
		if (route == null || route.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportRoute), routeId);
		}

		return route;
	}

	private async Task<TransportVehicle> GetVehicleForMediumAsync(Guid vehicleId, Guid academicMediumId)
	{
		var vehicle = await vehicleRepository.FindAsync(vehicleId);
		if (vehicle == null || vehicle.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportVehicle), vehicleId);
		}

		return vehicle;
	}

	private async Task<TransportStaff> GetStaffForAssignmentAsync(Guid staffId, Guid academicMediumId, TransportStaffRole role)
	{
		var staff = await staffRepository.FindAsync(staffId);
		if (staff == null || staff.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportStaff), staffId);
		}

		if (staff.Role != role || staff.Status != TransportStaffStatus.Active)
		{
			throw new BusinessException(TransportErrorCodes.RouteAssignmentInvalid)
				.WithData("StaffId", staffId)
				.WithData("Role", role.ToString());
		}

		return staff;
	}

	private async Task EnsureCanUseMediumAsync(Guid academicMediumId, string operation)
	{
		await academicMediumRepository.GetAsync(academicMediumId);
		await studentScopeAccessService.EnsureCanAccessMediumAsync(academicMediumId, operation);
	}

	private async Task<TransportRouteAssignment?> FindCurrentAssignmentForRouteAsync(Guid routeId, Guid academicMediumId)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(assignment =>
			assignment.AcademicMediumId == academicMediumId &&
			assignment.RouteId == routeId &&
			assignment.EffectiveTo == null));
	}

	private async Task CloseCurrentAssignmentAsync(Guid routeId, Guid academicMediumId, string? remarks)
	{
		var existing = await FindCurrentAssignmentForRouteAsync(routeId, academicMediumId);
		if (existing == null)
		{
			return;
		}

		existing.Close(DateTime.UtcNow, remarks);
		await assignmentRepository.UpdateAsync(existing, autoSave: false);
	}

	private async Task EnsureAssignmentResourcesAreAvailableAsync(
		Guid routeId,
		Guid academicMediumId,
		Guid vehicleId,
		Guid driverStaffId,
		Guid? conductorStaffId)
	{
		if (conductorStaffId.HasValue && conductorStaffId.Value == driverStaffId)
		{
			throw new BusinessException(TransportErrorCodes.RouteAssignmentConflict)
				.WithData("StaffId", driverStaffId);
		}

		var staffIds = new[] { driverStaffId, conductorStaffId ?? Guid.Empty }
			.Where(id => id != Guid.Empty)
			.ToHashSet();

		var query = await assignmentRepository.GetQueryableAsync();
		var conflicts = await AsyncExecuter.ToListAsync(query.Where(assignment =>
			assignment.AcademicMediumId == academicMediumId &&
			assignment.RouteId != routeId &&
			assignment.EffectiveTo == null &&
			(assignment.VehicleId == vehicleId ||
				staffIds.Contains(assignment.DriverStaffId) ||
				(assignment.ConductorStaffId.HasValue && staffIds.Contains(assignment.ConductorStaffId.Value)))));

		if (conflicts.Count > 0)
		{
			throw new BusinessException(TransportErrorCodes.RouteAssignmentConflict)
				.WithData("RouteId", conflicts[0].RouteId)
				.WithData("VehicleId", vehicleId);
		}
	}

	private async Task<PagedResultDto<TransportVehicleHistoryRowDto>> GetRouteAssignmentHistoryAsync(Guid routeId, TransportVehicleMaintenanceListRequestDto input)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		query = query.Where(assignment =>
			assignment.AcademicMediumId == input.AcademicMediumId &&
			assignment.RouteId == routeId);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var assignments = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(assignment => assignment.EffectiveFrom)
				.Skip(input.SkipCount)
				.Take(input.NormalizedPageSize));

		return new PagedResultDto<TransportVehicleHistoryRowDto>(
			totalCount,
			(await MapAssignmentsAsync(assignments)).Select(ToHistoryRow).ToList());
	}

	private async Task EnsureRouteCodeIsUniqueAsync(Guid academicMediumId, string routeCode, Guid? excludingRouteId = null)
	{
		var normalized = NormalizeRouteCode(routeCode);
		var routes = await routeRepository.GetListAsync(route =>
			route.AcademicMediumId == academicMediumId &&
			(!excludingRouteId.HasValue || route.Id != excludingRouteId.Value));

		if (routes.Any(route => NormalizeRouteCode(route.RouteCode) == normalized))
		{
			throw new BusinessException(TransportErrorCodes.RouteAlreadyExists)
				.WithData("RouteCode", normalized)
				.WithData("AcademicMediumId", academicMediumId);
		}
	}

	private async Task<IReadOnlyDictionary<Guid, List<TransportRouteStopDto>>> GetRouteStopsForRoutesAsync(List<Guid> routeIds)
	{
		if (routeIds.Count == 0)
		{
			return new Dictionary<Guid, List<TransportRouteStopDto>>();
		}

		var routeStopQuery = await routeStopRepository.GetQueryableAsync();
		var stopQuery = await stopRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(
			from routeStop in routeStopQuery
			join stop in stopQuery on routeStop.StopId equals stop.Id
			where routeIds.Contains(routeStop.RouteId)
			orderby routeStop.StopSequence
			select new { routeStop.RouteId, RouteStop = routeStop, Stop = stop });

		return rows
			.GroupBy(row => row.RouteId)
			.ToDictionary(
				group => group.Key,
				group => group.Select(row => MapRouteStop(row.RouteStop, row.Stop)).ToList());
	}

	private static void ValidateRouteInput(CreateUpdateTransportRouteDto input)
	{
		if (string.IsNullOrWhiteSpace(input.RouteName))
		{
			throw new UserFriendlyException("Route name is required.");
		}

		if (string.IsNullOrWhiteSpace(input.RouteCode))
		{
			throw new UserFriendlyException("Route code is required.");
		}

		if (string.IsNullOrWhiteSpace(input.RouteType))
		{
			throw new UserFriendlyException("Route type is required.");
		}

		if (string.IsNullOrWhiteSpace(input.StartPoint))
		{
			throw new UserFriendlyException("Start point is required.");
		}

		if (string.IsNullOrWhiteSpace(input.EndPoint))
		{
			throw new UserFriendlyException("End point is required.");
		}

		if (input.Distance < 0)
		{
			throw new UserFriendlyException("Distance cannot be negative.");
		}
	}

	private static void ValidateRouteStops(AssignTransportRouteStopsDto input)
	{
		if (input.Stops.Count == 0)
		{
			throw new BusinessException(TransportErrorCodes.RouteStopInvalid);
		}

		if (input.Stops.Select(stop => stop.StopId).Distinct().Count() != input.Stops.Count)
		{
			throw new BusinessException(TransportErrorCodes.RouteStopInvalid);
		}

		if (input.Stops.Any(stop => stop.StopId == Guid.Empty || stop.Order <= 0))
		{
			throw new BusinessException(TransportErrorCodes.RouteStopInvalid);
		}
	}

	private static void ValidateAssignmentInput(AssignTransportRouteVehicleDto input)
	{
		if (input.VehicleId == Guid.Empty)
		{
			throw new UserFriendlyException("Vehicle is required.");
		}

		if (input.DriverStaffId == Guid.Empty)
		{
			throw new UserFriendlyException("Driver is required.");
		}

		if (input.ConductorStaffId == Guid.Empty)
		{
			input.ConductorStaffId = null;
		}
	}

	private async Task<List<TransportRouteAssignmentDto>> MapAssignmentsAsync(List<TransportRouteAssignment> assignments)
	{
		if (assignments.Count == 0)
		{
			return new List<TransportRouteAssignmentDto>();
		}

		var routeIds = assignments.Select(x => x.RouteId).Distinct().ToList();
		var vehicleIds = assignments.Select(x => x.VehicleId).Distinct().ToList();
		var staffIds = assignments
			.SelectMany(x => new[] { x.DriverStaffId, x.ConductorStaffId ?? Guid.Empty })
			.Where(id => id != Guid.Empty)
			.Distinct()
			.ToList();

		var routes = (await routeRepository.GetListAsync(x => routeIds.Contains(x.Id))).ToDictionary(x => x.Id);
		var vehicles = (await vehicleRepository.GetListAsync(x => vehicleIds.Contains(x.Id))).ToDictionary(x => x.Id);
		var staff = (await staffRepository.GetListAsync(x => staffIds.Contains(x.Id))).ToDictionary(x => x.Id);
		var employeeIds = staff.Values.Select(x => x.EmployeeId).Distinct().ToList();
		var employees = (await employeeRepository.GetListAsync(x => employeeIds.Contains(x.Id))).ToDictionary(x => x.Id);

		return assignments.Select(assignment =>
		{
			routes.TryGetValue(assignment.RouteId, out var route);
			vehicles.TryGetValue(assignment.VehicleId, out var vehicle);
			staff.TryGetValue(assignment.DriverStaffId, out var driverStaff);
			Employee? driverEmployee = null;
			if (driverStaff != null)
			{
				employees.TryGetValue(driverStaff.EmployeeId, out driverEmployee);
			}

			TransportStaff? conductorStaff = null;
			Employee? conductorEmployee = null;
			if (assignment.ConductorStaffId.HasValue)
			{
				staff.TryGetValue(assignment.ConductorStaffId.Value, out conductorStaff);
				if (conductorStaff != null)
				{
					employees.TryGetValue(conductorStaff.EmployeeId, out conductorEmployee);
				}
			}

			return new TransportRouteAssignmentDto
			{
				Id = assignment.Id,
				AcademicMediumId = assignment.AcademicMediumId,
				RouteId = assignment.RouteId,
				RouteName = route?.RouteName ?? string.Empty,
				RouteCode = route?.RouteCode ?? string.Empty,
				VehicleId = assignment.VehicleId,
				VehicleName = vehicle?.VehicleName ?? vehicle?.VehicleNo ?? vehicle?.RegistrationNo ?? string.Empty,
				VehicleRegistrationNo = vehicle?.RegistrationNo ?? string.Empty,
				DriverStaffId = assignment.DriverStaffId,
				DriverEmployeeId = driverStaff?.EmployeeId ?? Guid.Empty,
				DriverName = driverEmployee == null ? string.Empty : GetEmployeeName(driverEmployee),
				DriverEmployeeCode = driverEmployee?.EmployeeCode ?? string.Empty,
				ConductorStaffId = assignment.ConductorStaffId,
				ConductorEmployeeId = conductorStaff?.EmployeeId,
				ConductorName = conductorEmployee == null ? null : GetEmployeeName(conductorEmployee),
				ConductorEmployeeCode = conductorEmployee?.EmployeeCode,
				EffectiveFrom = assignment.EffectiveFrom,
				EffectiveTo = assignment.EffectiveTo,
				IsCurrent = assignment.EffectiveTo == null,
				Remarks = assignment.Remarks,
				CreationTime = assignment.CreationTime,
				CreatorId = assignment.CreatorId,
				LastModificationTime = assignment.LastModificationTime,
				LastModifierId = assignment.LastModifierId
			};
		}).ToList();
	}

	private static TransportVehicleHistoryRowDto ToHistoryRow(TransportRouteAssignmentDto assignment) => new()
	{
		Id = assignment.Id,
		Name = assignment.RouteName,
		RouteId = assignment.RouteId,
		RouteName = assignment.RouteName,
		RouteCode = assignment.RouteCode,
		VehicleId = assignment.VehicleId,
		VehicleName = assignment.VehicleName,
		VehicleRegistrationNo = assignment.VehicleRegistrationNo,
		DriverStaffId = assignment.DriverStaffId,
		DriverName = assignment.DriverName,
		ConductorStaffId = assignment.ConductorStaffId,
		ConductorName = assignment.ConductorName,
		EffectiveFrom = assignment.EffectiveFrom,
		EffectiveTo = assignment.EffectiveTo,
		IsCurrent = assignment.IsCurrent
	};

	private static string GetEmployeeName(Employee employee)
	{
		var name = employee.EmployeeName;
		return string.IsNullOrWhiteSpace(name) ? employee.EmployeeCode : name;
	}

	private static TransportRouteDto MapRoute(
		TransportRoute route,
		IReadOnlyDictionary<Guid, List<TransportRouteStopDto>> routeStops)
	{
		routeStops.TryGetValue(route.Id, out var stops);
		return MapRoute(route, stops ?? new List<TransportRouteStopDto>());
	}

	private static TransportRouteDto MapRoute(TransportRoute route, List<TransportRouteStopDto> stops)
	{
		return new TransportRouteDto
		{
			Id = route.Id,
			AcademicMediumId = route.AcademicMediumId,
			RouteName = route.RouteName,
			RouteCode = route.RouteCode,
			RouteType = route.RouteType,
			Distance = route.Distance,
			Duration = route.Duration,
			Description = route.Description,
			StartPoint = route.StartPoint,
			EndPoint = route.EndPoint,
			PickupSchedule = new TransportScheduleDto
			{
				StartTime = route.PickupStartTime,
				EndTime = route.PickupEndTime
			},
			DropoffSchedule = new TransportScheduleDto
			{
				StartTime = route.DropoffStartTime,
				EndTime = route.DropoffEndTime
			},
			Status = route.Status,
			Stops = stops.Count,
			Capacity = 0,
			Students = 0,
			StopData = stops,
			CreationTime = route.CreationTime,
			CreatorId = route.CreatorId,
			LastModificationTime = route.LastModificationTime,
			LastModifierId = route.LastModifierId
		};
	}

	private static TransportRouteStopDto MapRouteStop(TransportRouteStop routeStop, TransportStop stop)
	{
		return new TransportRouteStopDto
		{
			Id = routeStop.Id,
			StopId = routeStop.StopId,
			StopName = stop.StopName,
			Order = routeStop.StopSequence,
			ExpectedArrivalTime = routeStop.ExpectedArrivalTime,
			ExpectedDepartureTime = routeStop.ExpectedDepartureTime,
			DistanceFromPrevious = routeStop.DistanceFromPrevious,
			CreationTime = routeStop.CreationTime,
			CreatorId = routeStop.CreatorId,
			LastModificationTime = routeStop.LastModificationTime,
			LastModifierId = routeStop.LastModifierId
		};
	}

	private static string NormalizeRouteCode(string routeCode)
	{
		return routeCode.Trim().ToUpper(CultureInfo.InvariantCulture);
	}
}
