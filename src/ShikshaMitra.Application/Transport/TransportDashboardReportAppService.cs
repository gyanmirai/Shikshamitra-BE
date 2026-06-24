using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Transport;

[RemoteService(false)]
public class TransportDashboardReportAppService(
	IRepository<TransportStop, Guid> stopRepository,
	IRepository<TransportRoute, Guid> routeRepository,
	IRepository<TransportRouteStop, Guid> routeStopRepository,
	IRepository<TransportVehicle, Guid> vehicleRepository,
	IRepository<TransportVehicleType, Guid> vehicleTypeRepository,
	IRepository<TransportFuelType, Guid> fuelTypeRepository,
	IRepository<TransportVehicleCompliance, Guid> complianceRepository,
	IRepository<TransportRequiredVehicleDocument, Guid> requiredDocumentRepository,
	IRepository<TransportVehicleDocument, Guid> vehicleDocumentRepository,
	IRepository<TransportVehicleMaintenanceRecord, Guid> maintenanceRepository,
	IRepository<TransportStaff, Guid> staffRepository,
	IRepository<TransportRouteAssignment, Guid> routeAssignmentRepository,
	IRepository<TransportMember, Guid> memberRepository,
	IRepository<TransportMemberRouteAssignment, Guid> memberAssignmentRepository,
	IRepository<TransportApplication, Guid> applicationRepository,
	IRepository<TransportFeeRun, Guid> feeRunRepository,
	IRepository<TransportFeeDue, Guid> feeDueRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<EducationMedium, Guid> academicMediumRepository,
	StudentScopeAccessService studentScopeAccessService
) : ApplicationService, ITransportDashboardReportAppService
{
	private const string UnsupportedMessage = "This report depends on a future transport phase.";

	private static readonly List<ReportDefinition> ReportDefinitions =
	[
		new("vehicle-master", "Vehicle Master Report", "Vehicle & Fleet Reports", true, "Current vehicle registry with type, fuel, assignment, and status."),
		new("vehicle-availability-status", "Vehicle Availability Status Report", "Vehicle & Fleet Reports", true, "Vehicle availability by active, inactive, and maintenance status."),
		new("vehicle-fitness-inspection", "Vehicle Fitness & Inspection Report", "Vehicle & Fleet Reports", true, "Vehicle fitness certificate expiry and compliance state."),
		new("vehicle-insurance-expiry", "Vehicle Insurance Expiry Report", "Vehicle & Fleet Reports", true, "Vehicle insurance expiry and policy details."),
		new("vehicle-pollution-certificate-expiry", "Vehicle Pollution Certificate Expiry Report", "Vehicle & Fleet Reports", true, "Vehicle PUC expiry and certificate details."),
		new("vehicle-permit-status", "Vehicle Permit Status Report", "Vehicle & Fleet Reports", true, "Vehicle road tax and permit-like compliance data currently stored."),
		new("vehicle-maintenance-schedule", "Vehicle Maintenance Schedule Report", "Vehicle & Fleet Reports", true, "Vehicle maintenance records with next service dates."),
		new("vehicle-repair-service-history", "Vehicle Repair & Service History Report", "Vehicle & Fleet Reports", true, "Vehicle service history from maintenance records."),
		new("route-master", "Route Master Report", "Route & Stop Reports", true, "Route setup, distance, timings, stop count, and assignment coverage."),
		new("route-wise-student-allocation", "Route-wise Student Allocation Report", "Route & Stop Reports", true, "Passenger allocation by route and stop."),
		new("route-capacity-utilization", "Route Capacity Utilization Report", "Route & Stop Reports", true, "Assigned capacity, occupied seats, and remaining seats by route."),
		new("bus-stop-master", "Bus Stop Master Report", "Route & Stop Reports", true, "Stop registry with amount, zone, status, and allocation count."),
		new("pickup-drop-route-timing", "Pickup & Drop Route Timing Report", "Route & Stop Reports", true, "Ordered stops with expected pickup and drop timing data."),
		new("route-distance-fuel-estimate", "Route Distance & Fuel Estimate Report", "Route & Stop Reports", true, "Route distance and approximate stop distance data."),
		new("driver-master", "Driver Master Report", "Driver & Staff Reports", true, "Transport driver records linked to HR employees."),
		new("conductor-assistant-master", "Conductor / Assistant Master Report", "Driver & Staff Reports", true, "Transport conductor records linked to HR employees."),
		new("driver-license-expiry", "Driver License Expiry Report", "Driver & Staff Reports", true, "Driver license expiry monitoring."),
		new("driver-conductor-assignment-history", "Driver / Conductor Assignment History Report", "Driver & Staff Reports", true, "Route assignment history for vehicles and staff."),
		new("transport-registered-students", "Transport Registered Students List", "Student Transport Reports", true, "Active and inactive passenger membership records."),
		new("student-pickup-drop-change-request", "Student Pickup/Drop Change Request Report", "Student Transport Reports", true, "Stop-change transport applications."),
		new("student-transport-cancellation", "Student Transport Cancellation Report", "Student Transport Reports", true, "Cancelled transport applications and inactive members."),
		new("new-transport-enrolment", "New Transport Enrolment Report", "Student Transport Reports", true, "New enrollment transport applications."),
		new("transport-fee-pending", "Transport Fee Pending Report", "Fee & Accounting Reports", true, "Pending transport dues for student and employee passengers."),
		new("route-wise-transport-fee", "Route-wise Transport Fee Report", "Fee & Accounting Reports", true, "Transport dues summarized by route and stop."),
		new("fee-run-posting-summary", "Fee-run Posting Summary Report", "Fee & Accounting Reports", true, "Fee-run generation and Finance posting summary."),
		new("monthly-transportation-summary", "Monthly Transportation Summary Report", "Fee & Accounting Reports", true, "Monthly dues and posting totals."),
		new("transport-performance-dashboard", "Transport Performance Dashboard Report", "Administrative & Audit Reports", true, "Dashboard KPI snapshot as report rows."),
		new("gps-tracking", "GPS Tracking Report", "Attendance & Trip Reports", false, UnsupportedMessage),
		new("speed-violation", "Speed Violation Report", "Safety Equipment Status Report", false, UnsupportedMessage),
		new("trip-attendance", "Trip Attendance Report", "Attendance & Trip Reports", false, UnsupportedMessage),
		new("missed-pickup-drop", "Missed Pickup / Drop Report", "Attendance & Trip Reports", false, UnsupportedMessage),
		new("fuel-consumption", "Fuel Consumption Report", "Vehicle & Fleet Reports", false, UnsupportedMessage),
		new("vehicle-mileage", "Vehicle Mileage Report", "Vehicle & Fleet Reports", false, UnsupportedMessage),
		new("breakdown", "Breakdown Report", "Vehicle & Fleet Reports", false, UnsupportedMessage),
		new("tyre-replacement", "Tyre Replacement Report", "Vehicle & Fleet Reports", false, UnsupportedMessage),
		new("transport-refund-adjustment", "Transport Refund / Adjustment Report", "Fee & Accounting Reports", false, UnsupportedMessage),
		new("payroll-deduction", "Employee Payroll Deduction Report", "Fee & Accounting Reports", false, UnsupportedMessage),
		new("audit-log", "Audit Log Report", "Administrative & Audit Reports", false, UnsupportedMessage)
	];

	[Authorize(ShikshaMitraSubModulePermissions.TransportDashboard)]
	public async Task<TransportDashboardSummaryDto> GetDashboardSummaryAsync(Guid academicMediumId, Guid? academicYearId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetDashboardSummaryAsync));
		var data = await LoadContextAsync(academicMediumId, academicYearId);
		var routeUsage = await BuildRouteUsageAsync(data);
		var activeRoutes = data.Routes.Where(route => route.Status == TransportRecordStatus.Active).ToList();
		var currentAssignments = data.RouteAssignments.Where(assignment => assignment.EffectiveTo == null).ToList();
		var dues = data.FeeDues;

		return new TransportDashboardSummaryDto
		{
			AcademicMediumId = academicMediumId,
			AcademicYearId = academicYearId,
			ActiveStops = data.Stops.Count(stop => stop.Status == TransportRecordStatus.Active),
			InactiveStops = data.Stops.Count(stop => stop.Status == TransportRecordStatus.Inactive),
			ActiveRoutes = activeRoutes.Count,
			InactiveRoutes = data.Routes.Count(route => route.Status == TransportRecordStatus.Inactive),
			ActiveVehicles = data.Vehicles.Count(vehicle => vehicle.Status == TransportVehicleStatus.Active),
			InactiveVehicles = data.Vehicles.Count(vehicle => vehicle.Status == TransportVehicleStatus.Inactive),
			MaintenanceVehicles = data.Vehicles.Count(vehicle => vehicle.Status == TransportVehicleStatus.Maintenance),
			ActiveDrivers = data.Staff.Count(staff => staff.Role == TransportStaffRole.Driver && staff.Status == TransportStaffStatus.Active),
			ActiveConductors = data.Staff.Count(staff => staff.Role == TransportStaffRole.Conductor && staff.Status == TransportStaffStatus.Active),
			InactiveStaff = data.Staff.Count(staff => staff.Status == TransportStaffStatus.Inactive),
			ActiveMembers = data.Members.Count(member => member.Status == TransportMemberStatus.Active),
			InactiveMembers = data.Members.Count(member => member.Status == TransportMemberStatus.Inactive),
			StudentMembers = data.Members.Count(member => member.MemberType == TransportMemberType.Student),
			EmployeeMembers = data.Members.Count(member => member.MemberType == TransportMemberType.Employee),
			PendingApplications = data.Applications.Count(application => application.Status == TransportApplicationStatus.Pending),
			ApprovedApplications = data.Applications.Count(application => application.Status == TransportApplicationStatus.Approved),
			HeldApplications = data.Applications.Count(application => application.Status == TransportApplicationStatus.Hold),
			RejectedApplications = data.Applications.Count(application => application.Status == TransportApplicationStatus.Rejected),
			CancelledApplications = data.Applications.Count(application => application.Status == TransportApplicationStatus.Cancelled),
			PendingFeeDues = dues.Count(due => due.Status == TransportFeeDueStatus.Pending),
			PostedFeeDues = dues.Count(due => due.Status == TransportFeeDueStatus.PostedToFinance),
			PendingStudentDues = dues.Count(due => due.MemberType == TransportMemberType.Student && due.Status == TransportFeeDueStatus.Pending),
			PendingEmployeeDues = dues.Count(due => due.MemberType == TransportMemberType.Employee && due.Status == TransportFeeDueStatus.Pending),
			PendingDueAmount = dues.Where(due => due.Status == TransportFeeDueStatus.Pending).Sum(due => due.Amount),
			AssignedRoutes = activeRoutes.Count(route => currentAssignments.Any(assignment => assignment.RouteId == route.Id)),
			UnassignedRoutes = activeRoutes.Count(route => currentAssignments.All(assignment => assignment.RouteId != route.Id)),
			RouteCapacity = routeUsage.Sum(route => route.Capacity),
			OccupiedSeats = routeUsage.Sum(route => route.OccupiedSeats),
			RouteCapacityUsagePercent = CalculatePercent(routeUsage.Sum(route => route.OccupiedSeats), routeUsage.Sum(route => route.Capacity)),
			MissingRequiredDocuments = CountMissingRequiredDocuments(data),
			ExpiringComplianceItems = CountExpiringComplianceItems(data),
			FeeRunsPendingFinancePost = CountFeeRunsPendingFinancePost(data),
			RouteCapacityUsage = routeUsage,
			ActiveRouteOperations = BuildOperationRows(data, routeUsage)
		};
	}

	[Authorize(ShikshaMitraSubModulePermissions.TransportDashboard)]
	public async Task<PagedResultDto<TransportDashboardAlertDto>> GetDashboardAlertsAsync(TransportDashboardAlertListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetDashboardAlertsAsync));
		var data = await LoadContextAsync(input.AcademicMediumId, input.AcademicYearId);
		var alerts = await BuildAlertsAsync(data);

		if (!string.IsNullOrWhiteSpace(input.Category))
		{
			alerts = alerts
				.Where(alert => string.Equals(alert.Category, input.Category.Trim(), StringComparison.OrdinalIgnoreCase))
				.ToList();
		}

		if (!string.IsNullOrWhiteSpace(input.Severity))
		{
			alerts = alerts
				.Where(alert => string.Equals(alert.Severity, input.Severity.Trim(), StringComparison.OrdinalIgnoreCase))
				.ToList();
		}

		var totalCount = alerts.Count;
		var rows = alerts
			.OrderByDescending(alert => SeverityRank(alert.Severity))
			.ThenByDescending(alert => alert.CreatedAt)
			.Skip(input.SkipCount)
			.Take(input.NormalizedPageSize)
			.ToList();

		return new PagedResultDto<TransportDashboardAlertDto>(totalCount, rows);
	}

	[Authorize(ShikshaMitraSubModulePermissions.TransportReports)]
	public async Task<List<TransportReportCatalogItemDto>> GetReportCatalogAsync(Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetReportCatalogAsync));
		return ReportDefinitions
			.Select(definition => new TransportReportCatalogItemDto
			{
				Key = definition.Key,
				Title = definition.Title,
				Category = definition.Category,
				Description = definition.Description,
				IsSupported = definition.IsSupported
			})
			.ToList();
	}

	[Authorize(ShikshaMitraSubModulePermissions.TransportReports)]
	public async Task<TransportReportResultDto> GetReportAsync(string reportKey, TransportReportListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetReportAsync));
		var definition = FindReportDefinition(reportKey);
		if (!definition.IsSupported)
		{
			return BuildUnsupportedReport(definition);
		}

		var data = await LoadContextAsync(input.AcademicMediumId, input.AcademicYearId);
		var table = await BuildReportTableAsync(definition.Key, input, data);
		var totalCount = table.Rows.Count;
		var rows = table.Rows
			.Skip(input.SkipCount)
			.Take(input.NormalizedPageSize)
			.ToList();

		return new TransportReportResultDto
		{
			Key = definition.Key,
			Title = definition.Title,
			Category = definition.Category,
			IsSupported = true,
			TotalCount = totalCount,
			Columns = table.Columns,
			Items = rows
		};
	}

	[Authorize(ShikshaMitraSubModulePermissions.TransportReports)]
	public async Task<TransportReportExportDto> ExportReportAsync(string reportKey, TransportReportListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(ExportReportAsync));
		var definition = FindReportDefinition(reportKey);
		if (!definition.IsSupported)
		{
			var columns = new List<TransportReportColumnDto> { new() { Key = "message", Label = "Message" } };
			var rows = new List<Dictionary<string, string?>>
			{
				new() { ["message"] = definition.Description }
			};
			return new TransportReportExportDto
			{
				FileName = $"{definition.Key}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv",
				ContentType = "text/csv",
				Content = Encoding.UTF8.GetBytes(BuildCsv(columns, rows))
			};
		}

		var data = await LoadContextAsync(input.AcademicMediumId, input.AcademicYearId);
		var table = await BuildReportTableAsync(definition.Key, input, data);
		var csv = BuildCsv(table.Columns, table.Rows);
		return new TransportReportExportDto
		{
			FileName = $"{definition.Key}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv",
			ContentType = "text/csv",
			Content = Encoding.UTF8.GetBytes(csv)
		};
	}

	private async Task<TransportReportContext> LoadContextAsync(Guid academicMediumId, Guid? academicYearId)
	{
		var stops = await stopRepository.GetListAsync(stop => stop.AcademicMediumId == academicMediumId);
		var routes = await routeRepository.GetListAsync(route => route.AcademicMediumId == academicMediumId);
		var routeIds = routes.Select(route => route.Id).ToList();
		var routeStops = routeIds.Count == 0
			? new List<TransportRouteStop>()
			: await routeStopRepository.GetListAsync(routeStop => routeIds.Contains(routeStop.RouteId));
		var vehicles = await vehicleRepository.GetListAsync(vehicle => vehicle.AcademicMediumId == academicMediumId);
		var vehicleTypes = await vehicleTypeRepository.GetListAsync(type => type.AcademicMediumId == academicMediumId);
		var fuelTypes = await fuelTypeRepository.GetListAsync(type => type.AcademicMediumId == academicMediumId);
		var compliance = await complianceRepository.GetListAsync(row => row.AcademicMediumId == academicMediumId);
		var requiredDocuments = await requiredDocumentRepository.GetListAsync(row => row.AcademicMediumId == academicMediumId && row.IsActive);
		var vehicleDocuments = await vehicleDocumentRepository.GetListAsync(row => row.AcademicMediumId == academicMediumId);
		var maintenance = await maintenanceRepository.GetListAsync(row => row.AcademicMediumId == academicMediumId);
		var staff = await staffRepository.GetListAsync(row => row.AcademicMediumId == academicMediumId);
		var routeAssignments = await routeAssignmentRepository.GetListAsync(row => row.AcademicMediumId == academicMediumId);
		var members = await memberRepository.GetListAsync(row => row.AcademicMediumId == academicMediumId);
		var memberAssignments = await memberAssignmentRepository.GetListAsync(row => row.AcademicMediumId == academicMediumId);
		var applications = await applicationRepository.GetListAsync(row => row.AcademicMediumId == academicMediumId);
		var feeRuns = await feeRunRepository.GetListAsync(row =>
			row.AcademicMediumId == academicMediumId &&
			(!academicYearId.HasValue || row.AcademicYearId == academicYearId.Value));
		var feeDues = await feeDueRepository.GetListAsync(row =>
			row.AcademicMediumId == academicMediumId &&
			(!academicYearId.HasValue || row.AcademicYearId == academicYearId.Value));
		var employeeIds = staff.Select(row => row.EmployeeId)
			.Concat(members.Where(row => row.EmployeeId.HasValue).Select(row => row.EmployeeId!.Value))
			.Distinct()
			.ToList();
		var employees = employeeIds.Count == 0
			? new List<Employee>()
			: await employeeRepository.GetListAsync(employee => employeeIds.Contains(employee.Id));

		return new TransportReportContext(
			academicMediumId,
			academicYearId,
			stops,
			routes,
			routeStops,
			vehicles,
			vehicleTypes,
			fuelTypes,
			compliance,
			requiredDocuments,
			vehicleDocuments,
			maintenance,
			staff,
			routeAssignments,
			members,
			memberAssignments,
			applications,
			feeRuns,
			feeDues,
			employees);
	}

	private async Task<List<TransportDashboardRouteUsageDto>> BuildRouteUsageAsync(TransportReportContext data)
	{
		var employees = data.Employees.ToDictionary(employee => employee.Id);
		var staffById = data.Staff.ToDictionary(staff => staff.Id);
		var vehiclesById = data.Vehicles.ToDictionary(vehicle => vehicle.Id);
		var currentAssignments = data.RouteAssignments
			.Where(assignment => assignment.EffectiveTo == null)
			.GroupBy(assignment => assignment.RouteId)
			.ToDictionary(group => group.Key, group => group.OrderByDescending(assignment => assignment.EffectiveFrom).First());
		var activeMembersByRoute = data.Members
			.Where(member => member.Status == TransportMemberStatus.Active && member.CurrentRouteId.HasValue)
			.GroupBy(member => member.CurrentRouteId!.Value)
			.ToDictionary(group => group.Key, group => group.Count());
		var rows = new List<TransportDashboardRouteUsageDto>();

		foreach (var route in data.Routes.OrderBy(route => route.RouteName))
		{
			currentAssignments.TryGetValue(route.Id, out var assignment);
			TransportVehicle? vehicle = null;
			if (assignment != null)
			{
				vehiclesById.TryGetValue(assignment.VehicleId, out vehicle);
			}

			activeMembersByRoute.TryGetValue(route.Id, out var occupied);
			var capacity = vehicle?.SeatingCapacity ?? 0;
			rows.Add(new TransportDashboardRouteUsageDto
			{
				RouteId = route.Id,
				RouteName = route.RouteName,
				RouteCode = route.RouteCode,
				Capacity = capacity,
				OccupiedSeats = occupied,
				RemainingSeats = capacity <= 0 ? 0 : Math.Max(capacity - occupied, 0),
				UsagePercent = CalculatePercent(occupied, capacity),
				HasVehicleAssignment = assignment != null,
				VehicleRegistrationNo = vehicle?.RegistrationNo,
				DriverName = assignment == null ? null : GetStaffName(assignment.DriverStaffId, staffById, employees)
			});
		}

		await Task.CompletedTask;
		return rows;
	}

	private static List<TransportDashboardOperationRowDto> BuildOperationRows(
		TransportReportContext data,
		List<TransportDashboardRouteUsageDto> routeUsage)
	{
		var routeStopCounts = data.RouteStops.GroupBy(stop => stop.RouteId).ToDictionary(group => group.Key, group => group.Count());
		var staffById = data.Staff.ToDictionary(staff => staff.Id);
		var employeeById = data.Employees.ToDictionary(employee => employee.Id);
		var vehicleById = data.Vehicles.ToDictionary(vehicle => vehicle.Id);
		var currentAssignments = data.RouteAssignments
			.Where(assignment => assignment.EffectiveTo == null)
			.GroupBy(assignment => assignment.RouteId)
			.ToDictionary(group => group.Key, group => group.OrderByDescending(assignment => assignment.EffectiveFrom).First());
		var usageByRoute = routeUsage.ToDictionary(row => row.RouteId);

		return data.Routes
			.Where(route => route.Status == TransportRecordStatus.Active)
			.OrderBy(route => route.RouteName)
			.Take(10)
			.Select(route =>
			{
				currentAssignments.TryGetValue(route.Id, out var assignment);
				usageByRoute.TryGetValue(route.Id, out var usage);
				TransportVehicle? vehicle = null;
				if (assignment != null)
				{
					vehicleById.TryGetValue(assignment.VehicleId, out vehicle);
				}

				return new TransportDashboardOperationRowDto
				{
					RouteId = route.Id,
					RouteName = route.RouteName,
					RouteCode = route.RouteCode,
					Status = route.Status.ToString(),
					Stops = routeStopCounts.GetValueOrDefault(route.Id),
					Members = usage?.OccupiedSeats ?? 0,
					Capacity = usage?.Capacity ?? 0,
					Vehicle = vehicle == null ? null : $"{vehicle.RegistrationNo} {vehicle.VehicleName}".Trim(),
					Driver = assignment == null ? null : GetStaffName(assignment.DriverStaffId, staffById, employeeById),
					Conductor = assignment?.ConductorStaffId == null ? null : GetStaffName(assignment.ConductorStaffId.Value, staffById, employeeById)
				};
			})
			.ToList();
	}

	private async Task<List<TransportDashboardAlertDto>> BuildAlertsAsync(TransportReportContext data)
	{
		var alerts = new List<TransportDashboardAlertDto>();
		var now = DateTime.UtcNow;
		var currentAssignments = data.RouteAssignments.Where(assignment => assignment.EffectiveTo == null).ToList();
		var assignedRouteIds = currentAssignments.Select(assignment => assignment.RouteId).ToHashSet();

		foreach (var application in data.Applications.Where(application => application.Status == TransportApplicationStatus.Pending))
		{
			alerts.Add(Alert("Applications", "Medium", "Pending transport application", $"{application.ApplicantName} is waiting for transport review.", application.Id, "TransportApplication", application.CreationTime));
		}

		foreach (var route in data.Routes.Where(route => route.Status == TransportRecordStatus.Active && !assignedRouteIds.Contains(route.Id)))
		{
			alerts.Add(Alert("Assignment", "High", "Route has no vehicle/staff assignment", $"{route.RouteName} is active but has no current vehicle assignment.", route.Id, "TransportRoute", now));
		}

		var routeUsage = await BuildRouteUsageAsync(data);
		foreach (var usage in routeUsage.Where(row => row.Capacity > 0 && row.UsagePercent >= 90))
		{
			var severity = usage.UsagePercent >= 100 ? "High" : "Medium";
			alerts.Add(Alert("Capacity", severity, "Route capacity is near full", $"{usage.RouteName} has {usage.OccupiedSeats}/{usage.Capacity} seats occupied.", usage.RouteId, "TransportRoute", now));
		}

		var complianceByVehicle = data.Compliance.ToDictionary(row => row.VehicleId);
		foreach (var vehicle in data.Vehicles.Where(vehicle => vehicle.Status == TransportVehicleStatus.Active))
		{
			complianceByVehicle.TryGetValue(vehicle.Id, out var compliance);
			var missingOrExpiring = CountVehicleComplianceIssues(compliance, now.Date.AddDays(30));
			if (missingOrExpiring > 0)
			{
				alerts.Add(Alert("Compliance", "High", "Vehicle compliance needs attention", $"{vehicle.RegistrationNo} has {missingOrExpiring} missing or expiring compliance item(s).", vehicle.Id, "TransportVehicle", now));
			}
		}

		var requiredDocumentIds = data.RequiredDocuments
			.Where(document => document.IsActive && document.IsRequired)
			.Select(document => document.Id)
			.ToList();
		var uploadedDocumentKeys = data.VehicleDocuments
			.Select(document => (document.VehicleId, document.RequiredDocumentId))
			.ToHashSet();
		foreach (var vehicle in data.Vehicles.Where(vehicle => vehicle.Status == TransportVehicleStatus.Active))
		{
			var missingCount = requiredDocumentIds.Count(requiredId => !uploadedDocumentKeys.Contains((vehicle.Id, requiredId)));
			if (missingCount > 0)
			{
				alerts.Add(Alert("Compliance", "Medium", "Required vehicle documents missing", $"{vehicle.RegistrationNo} is missing {missingCount} required document(s).", vehicle.Id, "TransportVehicle", now));
			}
		}

		var pendingEmployeeDues = data.FeeDues
			.Where(due => due.MemberType == TransportMemberType.Employee && due.Status == TransportFeeDueStatus.Pending)
			.ToList();
		if (pendingEmployeeDues.Count > 0)
		{
			alerts.Add(Alert("Billing", "Medium", "Employee transport dues pending", $"{pendingEmployeeDues.Count} employee transport due(s) are pending inside Transport.", null, "TransportFeeDue", now));
		}

		foreach (var run in data.FeeRuns.Where(run => run.Status != TransportFeeRunStatus.FinancePosted && run.StudentDueCount > 0))
		{
			alerts.Add(Alert("Billing", "Medium", "Fee run pending Finance posting", $"Fee run {run.PeriodFrom:dd-MMM-yyyy} to {run.PeriodTo:dd-MMM-yyyy} has student dues pending Finance posting.", run.Id, "TransportFeeRun", run.CreationTime));
		}

		return alerts;
	}

	private async Task<ReportTable> BuildReportTableAsync(string reportKey, TransportReportListRequestDto input, TransportReportContext data)
	{
		return reportKey switch
		{
			"vehicle-master" or "vehicle-availability-status" => BuildVehicleReport(input, data),
			"vehicle-fitness-inspection" => BuildVehicleComplianceReport(input, data, "fitness"),
			"vehicle-insurance-expiry" => BuildVehicleComplianceReport(input, data, "insurance"),
			"vehicle-pollution-certificate-expiry" => BuildVehicleComplianceReport(input, data, "puc"),
			"vehicle-permit-status" => BuildVehicleComplianceReport(input, data, "permit"),
			"vehicle-maintenance-schedule" or "vehicle-repair-service-history" => BuildMaintenanceReport(input, data),
			"route-master" => BuildRouteMasterReport(input, data),
			"route-wise-student-allocation" => BuildRouteAllocationReport(input, data),
			"route-capacity-utilization" => new ReportTable(RouteCapacityColumns(), (await BuildRouteUsageAsync(data)).Select(RouteUsageRow).ToList()),
			"bus-stop-master" => BuildStopReport(input, data),
			"pickup-drop-route-timing" => BuildRouteTimingReport(input, data),
			"route-distance-fuel-estimate" => BuildRouteDistanceReport(input, data),
			"driver-master" => BuildStaffReport(input, data, TransportStaffRole.Driver),
			"conductor-assistant-master" => BuildStaffReport(input, data, TransportStaffRole.Conductor),
			"driver-license-expiry" => BuildDriverLicenseReport(input, data),
			"driver-conductor-assignment-history" => BuildAssignmentHistoryReport(input, data),
			"transport-registered-students" => BuildMemberReport(input, data),
			"student-pickup-drop-change-request" => BuildApplicationReport(input, data, TransportApplicationType.StopChange),
			"student-transport-cancellation" => BuildCancellationReport(input, data),
			"new-transport-enrolment" => BuildApplicationReport(input, data, TransportApplicationType.NewEnrollment),
			"transport-fee-pending" => BuildDueReport(input, data, onlyPending: true),
			"route-wise-transport-fee" => BuildRouteWiseDueReport(input, data),
			"fee-run-posting-summary" => BuildFeeRunReport(input, data),
			"monthly-transportation-summary" => BuildMonthlySummaryReport(input, data),
			"transport-performance-dashboard" => BuildPerformanceReport(await GetDashboardSummaryAsync(data.AcademicMediumId, data.AcademicYearId)),
			_ => new ReportTable([], [])
		};
	}

	private static ReportTable BuildVehicleReport(TransportReportListRequestDto input, TransportReportContext data)
	{
		var vehicleTypes = data.VehicleTypes.ToDictionary(type => type.Id, type => type.Name);
		var fuelTypes = data.FuelTypes.ToDictionary(type => type.Id, type => type.Name);
		var currentAssignments = data.RouteAssignments
			.Where(assignment => assignment.EffectiveTo == null)
			.GroupBy(assignment => assignment.VehicleId)
			.ToDictionary(group => group.Key, group => group.OrderByDescending(assignment => assignment.EffectiveFrom).First());
		var routes = data.Routes.ToDictionary(route => route.Id);
		var staff = data.Staff.ToDictionary(row => row.Id);
		var employees = data.Employees.ToDictionary(row => row.Id);

		var vehicles = FilterVehicles(input, data.Vehicles)
			.Select(vehicle =>
			{
				currentAssignments.TryGetValue(vehicle.Id, out var assignment);
				var routeName = assignment != null && routes.TryGetValue(assignment.RouteId, out var route) ? route.RouteName : null;
				return new Dictionary<string, string?>
				{
					["registrationNo"] = vehicle.RegistrationNo,
					["vehicleNo"] = vehicle.VehicleNo,
					["vehicleName"] = vehicle.VehicleName,
					["vehicleType"] = vehicleTypes.GetValueOrDefault(vehicle.VehicleTypeId),
					["fuelType"] = fuelTypes.GetValueOrDefault(vehicle.FuelTypeId),
					["company"] = vehicle.VehicleCompany,
					["model"] = vehicle.Model,
					["capacity"] = vehicle.SeatingCapacity.ToString(CultureInfo.InvariantCulture),
					["status"] = vehicle.Status.ToString(),
					["route"] = routeName,
					["driver"] = assignment == null ? null : GetStaffName(assignment.DriverStaffId, staff, employees)
				};
			})
			.ToList();

		return new ReportTable(
			Columns(("registrationNo", "Registration No"), ("vehicleNo", "Vehicle No"), ("vehicleName", "Vehicle Name"), ("vehicleType", "Type"), ("fuelType", "Fuel"), ("company", "Company"), ("model", "Model"), ("capacity", "Capacity"), ("status", "Status"), ("route", "Route"), ("driver", "Driver")),
			vehicles);
	}

	private static ReportTable BuildVehicleComplianceReport(TransportReportListRequestDto input, TransportReportContext data, string mode)
	{
		var complianceByVehicle = data.Compliance.ToDictionary(row => row.VehicleId);
		var rows = FilterVehicles(input, data.Vehicles)
			.Select(vehicle =>
			{
				complianceByVehicle.TryGetValue(vehicle.Id, out var compliance);
				return mode switch
				{
					"fitness" => VehicleComplianceRow(vehicle, "Fitness", compliance?.FitnessCertificateNo, compliance?.FitnessIssueDate, compliance?.FitnessExpiryDate),
					"insurance" => VehicleComplianceRow(vehicle, "Insurance", compliance?.PolicyNo, compliance?.InsuranceIssueDate, compliance?.InsuranceExpiryDate, compliance?.InsuranceCompany),
					"puc" => VehicleComplianceRow(vehicle, "PUC", compliance?.PucCertificateNo, compliance?.PucIssueDate, compliance?.PucExpiryDate),
					_ => VehicleComplianceRow(vehicle, "Road Tax", compliance?.RoadTaxReceiptNo, compliance?.RoadTaxIssueDate, null)
				};
			})
			.Where(row => MatchesStatus(row.GetValueOrDefault("status"), input.Status))
			.ToList();

		return new ReportTable(
			Columns(("registrationNo", "Registration No"), ("vehicleName", "Vehicle"), ("documentType", "Document"), ("referenceNo", "Reference No"), ("provider", "Provider"), ("issueDate", "Issue Date"), ("expiryDate", "Expiry Date"), ("status", "Status")),
			rows);
	}

	private static ReportTable BuildMaintenanceReport(TransportReportListRequestDto input, TransportReportContext data)
	{
		var vehicles = data.Vehicles.ToDictionary(vehicle => vehicle.Id);
		var rows = data.Maintenance
			.Where(record => !input.VehicleId.HasValue || record.VehicleId == input.VehicleId.Value)
			.Where(record => IsWithinDateRange(record.ServiceDate, input.FromDate, input.ToDate))
			.OrderByDescending(record => record.ServiceDate)
			.Select(record =>
			{
				vehicles.TryGetValue(record.VehicleId, out var vehicle);
				return new Dictionary<string, string?>
				{
					["serviceDate"] = FormatDate(record.ServiceDate),
					["vehicle"] = vehicle?.RegistrationNo,
					["serviceType"] = record.ServiceType,
					["provider"] = record.Provider,
					["cost"] = FormatAmount(record.Cost),
					["nextServiceDate"] = FormatDate(record.NextServiceDate),
					["notes"] = record.Notes
				};
			})
			.ToList();

		return new ReportTable(
			Columns(("serviceDate", "Service Date"), ("vehicle", "Vehicle"), ("serviceType", "Service Type"), ("provider", "Provider"), ("cost", "Cost"), ("nextServiceDate", "Next Service"), ("notes", "Notes")),
			rows);
	}

	private static ReportTable BuildRouteMasterReport(TransportReportListRequestDto input, TransportReportContext data)
	{
		var stopCounts = data.RouteStops.GroupBy(row => row.RouteId).ToDictionary(group => group.Key, group => group.Count());
		var assignedRouteIds = data.RouteAssignments.Where(row => row.EffectiveTo == null).Select(row => row.RouteId).ToHashSet();
		var rows = FilterRoutes(input, data.Routes)
			.Select(route => new Dictionary<string, string?>
			{
				["routeCode"] = route.RouteCode,
				["routeName"] = route.RouteName,
				["routeType"] = route.RouteType,
				["startPoint"] = route.StartPoint,
				["endPoint"] = route.EndPoint,
				["distance"] = route.Distance.ToString("0.##", CultureInfo.InvariantCulture),
				["stops"] = stopCounts.GetValueOrDefault(route.Id).ToString(CultureInfo.InvariantCulture),
				["assignment"] = assignedRouteIds.Contains(route.Id) ? "Assigned" : "Unassigned",
				["status"] = route.Status.ToString()
			})
			.ToList();

		return new ReportTable(
			Columns(("routeCode", "Route Code"), ("routeName", "Route"), ("routeType", "Type"), ("startPoint", "Start"), ("endPoint", "End"), ("distance", "Distance"), ("stops", "Stops"), ("assignment", "Assignment"), ("status", "Status")),
			rows);
	}

	private static ReportTable BuildRouteAllocationReport(TransportReportListRequestDto input, TransportReportContext data)
	{
		var routes = data.Routes.ToDictionary(route => route.Id);
		var stops = data.Stops.ToDictionary(stop => stop.Id);
		var rows = data.Members
			.Where(member => member.CurrentRouteId.HasValue && member.CurrentStopId.HasValue)
			.Where(member => !input.RouteId.HasValue || member.CurrentRouteId == input.RouteId.Value)
			.Where(member => !input.StopId.HasValue || member.CurrentStopId == input.StopId.Value)
			.Where(member => !input.MemberType.HasValue || member.MemberType == input.MemberType.Value)
			.Where(member => MatchesStatus(member.Status.ToString(), input.Status))
			.OrderBy(member => member.DisplayName)
			.Select(member =>
			{
				routes.TryGetValue(member.CurrentRouteId!.Value, out var route);
				stops.TryGetValue(member.CurrentStopId!.Value, out var stop);
				return new Dictionary<string, string?>
				{
					["memberName"] = member.DisplayName,
					["memberCode"] = member.MemberCode,
					["memberType"] = member.MemberType.ToString(),
					["route"] = route?.RouteName,
					["stop"] = stop?.StopName,
					["status"] = member.Status.ToString()
				};
			})
			.ToList();

		return new ReportTable(
			Columns(("memberName", "Member"), ("memberCode", "Code"), ("memberType", "Type"), ("route", "Route"), ("stop", "Stop"), ("status", "Status")),
			rows);
	}

	private static ReportTable BuildStopReport(TransportReportListRequestDto input, TransportReportContext data)
	{
		var memberCounts = data.Members
			.Where(member => member.CurrentStopId.HasValue)
			.GroupBy(member => member.CurrentStopId!.Value)
			.ToDictionary(group => group.Key, group => group.Count());
		var routeCounts = data.RouteStops.GroupBy(stop => stop.StopId).ToDictionary(group => group.Key, group => group.Count());
		var rows = data.Stops
			.Where(stop => !input.StopId.HasValue || stop.Id == input.StopId.Value)
			.Where(stop => MatchesStatus(stop.Status.ToString(), input.Status))
			.OrderBy(stop => stop.StopName)
			.Select(stop => new Dictionary<string, string?>
			{
				["stopName"] = stop.StopName,
				["landmark"] = stop.Landmark,
				["zone"] = stop.ZoneId,
				["amount"] = FormatAmount(stop.Amount),
				["approxKm"] = stop.ApproxKmFromSchool.ToString("0.##", CultureInfo.InvariantCulture),
				["routes"] = routeCounts.GetValueOrDefault(stop.Id).ToString(CultureInfo.InvariantCulture),
				["members"] = memberCounts.GetValueOrDefault(stop.Id).ToString(CultureInfo.InvariantCulture),
				["status"] = stop.Status.ToString()
			})
			.ToList();

		return new ReportTable(
			Columns(("stopName", "Stop"), ("landmark", "Landmark"), ("zone", "Zone"), ("amount", "Amount"), ("approxKm", "Approx Km"), ("routes", "Routes"), ("members", "Members"), ("status", "Status")),
			rows);
	}

	private static ReportTable BuildRouteTimingReport(TransportReportListRequestDto input, TransportReportContext data)
	{
		var routes = data.Routes.ToDictionary(route => route.Id);
		var stops = data.Stops.ToDictionary(stop => stop.Id);
		var rows = data.RouteStops
			.Where(routeStop => !input.RouteId.HasValue || routeStop.RouteId == input.RouteId.Value)
			.Where(routeStop => !input.StopId.HasValue || routeStop.StopId == input.StopId.Value)
			.OrderBy(routeStop => routes.GetValueOrDefault(routeStop.RouteId)?.RouteName)
			.ThenBy(routeStop => routeStop.StopSequence)
			.Select(routeStop =>
			{
				routes.TryGetValue(routeStop.RouteId, out var route);
				stops.TryGetValue(routeStop.StopId, out var stop);
				return new Dictionary<string, string?>
				{
					["routeCode"] = route?.RouteCode,
					["route"] = route?.RouteName,
					["sequence"] = routeStop.StopSequence.ToString(CultureInfo.InvariantCulture),
					["stop"] = stop?.StopName,
					["arrival"] = FormatTime(routeStop.ExpectedArrivalTime),
					["departure"] = FormatTime(routeStop.ExpectedDepartureTime),
					["distanceFromPrevious"] = routeStop.DistanceFromPrevious?.ToString("0.##", CultureInfo.InvariantCulture)
				};
			})
			.ToList();

		return new ReportTable(
			Columns(("routeCode", "Route Code"), ("route", "Route"), ("sequence", "Seq"), ("stop", "Stop"), ("arrival", "Arrival"), ("departure", "Departure"), ("distanceFromPrevious", "Distance From Previous")),
			rows);
	}

	private static ReportTable BuildRouteDistanceReport(TransportReportListRequestDto input, TransportReportContext data)
	{
		var rows = FilterRoutes(input, data.Routes)
			.Select(route => new Dictionary<string, string?>
			{
				["routeCode"] = route.RouteCode,
				["route"] = route.RouteName,
				["startPoint"] = route.StartPoint,
				["endPoint"] = route.EndPoint,
				["routeDistance"] = route.Distance.ToString("0.##", CultureInfo.InvariantCulture),
				["duration"] = route.Duration,
				["stops"] = data.RouteStops.Count(stop => stop.RouteId == route.Id).ToString(CultureInfo.InvariantCulture)
			})
			.ToList();

		return new ReportTable(
			Columns(("routeCode", "Route Code"), ("route", "Route"), ("startPoint", "Start"), ("endPoint", "End"), ("routeDistance", "Distance"), ("duration", "Duration"), ("stops", "Stops")),
			rows);
	}

	private static ReportTable BuildStaffReport(TransportReportListRequestDto input, TransportReportContext data, TransportStaffRole role)
	{
		var employees = data.Employees.ToDictionary(employee => employee.Id);
		var assignments = data.RouteAssignments.Where(assignment => assignment.EffectiveTo == null).ToList();
		var routes = data.Routes.ToDictionary(route => route.Id);
		var rows = data.Staff
			.Where(staff => staff.Role == role)
			.Where(staff => !input.StaffId.HasValue || staff.Id == input.StaffId.Value)
			.Where(staff => MatchesStatus(staff.Status.ToString(), input.Status))
			.OrderBy(staff => GetStaffName(staff, employees))
			.Select(staff =>
			{
				var assignment = assignments.FirstOrDefault(row =>
					row.DriverStaffId == staff.Id ||
					(row.ConductorStaffId.HasValue && row.ConductorStaffId.Value == staff.Id));
				var routeName = assignment != null && routes.TryGetValue(assignment.RouteId, out var route) ? route.RouteName : null;
				return new Dictionary<string, string?>
				{
					["employeeCode"] = GetStaffEmployeeCode(staff, employees),
					["name"] = GetStaffName(staff, employees),
					["role"] = staff.Role.ToString(),
					["licenseNo"] = staff.LicenseNo,
					["licenseExpiry"] = FormatDate(staff.LicenseExpiryDate),
					["experience"] = staff.ExperienceYears?.ToString(CultureInfo.InvariantCulture),
					["assignedRoute"] = routeName,
					["status"] = staff.Status.ToString()
				};
			})
			.ToList();

		return new ReportTable(
			Columns(("employeeCode", "Employee Code"), ("name", "Name"), ("role", "Role"), ("licenseNo", "License No"), ("licenseExpiry", "License Expiry"), ("experience", "Experience Years"), ("assignedRoute", "Assigned Route"), ("status", "Status")),
			rows);
	}

	private static ReportTable BuildDriverLicenseReport(TransportReportListRequestDto input, TransportReportContext data)
	{
		var table = BuildStaffReport(input, data, TransportStaffRole.Driver);
		table.Rows = table.Rows
			.OrderBy(row => row.GetValueOrDefault("licenseExpiry"))
			.ToList();
		return table;
	}

	private static ReportTable BuildAssignmentHistoryReport(TransportReportListRequestDto input, TransportReportContext data)
	{
		var routes = data.Routes.ToDictionary(route => route.Id);
		var vehicles = data.Vehicles.ToDictionary(vehicle => vehicle.Id);
		var staff = data.Staff.ToDictionary(row => row.Id);
		var employees = data.Employees.ToDictionary(employee => employee.Id);
		var rows = data.RouteAssignments
			.Where(assignment => !input.RouteId.HasValue || assignment.RouteId == input.RouteId.Value)
			.Where(assignment => !input.VehicleId.HasValue || assignment.VehicleId == input.VehicleId.Value)
			.Where(assignment => !input.StaffId.HasValue ||
				assignment.DriverStaffId == input.StaffId.Value ||
				assignment.ConductorStaffId == input.StaffId.Value)
			.Where(assignment => IsWithinDateRange(assignment.EffectiveFrom, input.FromDate, input.ToDate))
			.OrderByDescending(assignment => assignment.EffectiveFrom)
			.Select(assignment =>
			{
				routes.TryGetValue(assignment.RouteId, out var route);
				vehicles.TryGetValue(assignment.VehicleId, out var vehicle);
				return new Dictionary<string, string?>
				{
					["route"] = route?.RouteName,
					["vehicle"] = vehicle?.RegistrationNo,
					["driver"] = GetStaffName(assignment.DriverStaffId, staff, employees),
					["conductor"] = assignment.ConductorStaffId.HasValue ? GetStaffName(assignment.ConductorStaffId.Value, staff, employees) : null,
					["effectiveFrom"] = FormatDate(assignment.EffectiveFrom),
					["effectiveTo"] = FormatDate(assignment.EffectiveTo),
					["status"] = assignment.EffectiveTo == null ? "Current" : "Closed",
					["remarks"] = assignment.Remarks
				};
			})
			.ToList();

		return new ReportTable(
			Columns(("route", "Route"), ("vehicle", "Vehicle"), ("driver", "Driver"), ("conductor", "Conductor"), ("effectiveFrom", "From"), ("effectiveTo", "To"), ("status", "Status"), ("remarks", "Remarks")),
			rows);
	}

	private static ReportTable BuildMemberReport(TransportReportListRequestDto input, TransportReportContext data)
	{
		var routes = data.Routes.ToDictionary(route => route.Id);
		var stops = data.Stops.ToDictionary(stop => stop.Id);
		var rows = data.Members
			.Where(member => !input.MemberType.HasValue || member.MemberType == input.MemberType.Value)
			.Where(member => !input.RouteId.HasValue || member.CurrentRouteId == input.RouteId.Value)
			.Where(member => !input.StopId.HasValue || member.CurrentStopId == input.StopId.Value)
			.Where(member => MatchesStatus(member.Status.ToString(), input.Status))
			.OrderBy(member => member.DisplayName)
			.Select(member =>
			{
				var routeName = member.CurrentRouteId.HasValue && routes.TryGetValue(member.CurrentRouteId.Value, out var route) ? route.RouteName : null;
				var stopName = member.CurrentStopId.HasValue && stops.TryGetValue(member.CurrentStopId.Value, out var stop) ? stop.StopName : null;
				return new Dictionary<string, string?>
				{
					["memberName"] = member.DisplayName,
					["memberCode"] = member.MemberCode,
					["memberType"] = member.MemberType.ToString(),
					["classOrRole"] = member.MemberType == TransportMemberType.Student ? $"{member.ClassName} {member.DivisionName}".Trim() : member.Role,
					["phone"] = member.PhoneNo,
					["route"] = routeName,
					["stop"] = stopName,
					["effectiveFrom"] = FormatDate(member.EffectiveFrom),
					["status"] = member.Status.ToString()
				};
			})
			.ToList();

		return new ReportTable(
			Columns(("memberName", "Member"), ("memberCode", "Code"), ("memberType", "Type"), ("classOrRole", "Class/Role"), ("phone", "Phone"), ("route", "Route"), ("stop", "Stop"), ("effectiveFrom", "Effective From"), ("status", "Status")),
			rows);
	}

	private static ReportTable BuildApplicationReport(TransportReportListRequestDto input, TransportReportContext data, TransportApplicationType applicationType)
	{
		var routes = data.Routes.ToDictionary(route => route.Id);
		var stops = data.Stops.ToDictionary(stop => stop.Id);
		var rows = data.Applications
			.Where(application => application.ApplicationType == applicationType)
			.Where(application => !input.MemberType.HasValue || application.MemberType == input.MemberType.Value)
			.Where(application => !input.RouteId.HasValue || application.AssignedRouteId == input.RouteId.Value)
			.Where(application => !input.StopId.HasValue || application.AssignedStopId == input.StopId.Value || application.RequestedStopId == input.StopId.Value)
			.Where(application => MatchesStatus(application.Status.ToString(), input.Status))
			.Where(application => IsWithinDateRange(application.CreationTime, input.FromDate, input.ToDate))
			.OrderByDescending(application => application.CreationTime)
			.Select(application => ApplicationRow(application, routes, stops))
			.ToList();

		return new ReportTable(ApplicationColumns(), rows);
	}

	private static ReportTable BuildCancellationReport(TransportReportListRequestDto input, TransportReportContext data)
	{
		var routes = data.Routes.ToDictionary(route => route.Id);
		var stops = data.Stops.ToDictionary(stop => stop.Id);
		var cancelledApplications = data.Applications
			.Where(application => application.Status == TransportApplicationStatus.Cancelled);
		var inactiveMembers = data.Members
			.Where(member => member.Status == TransportMemberStatus.Inactive)
			.Select(member => new Dictionary<string, string?>
			{
				["applicationDate"] = FormatDate(member.LastModificationTime ?? member.CreationTime),
				["applicant"] = member.DisplayName,
				["memberCode"] = member.MemberCode,
				["memberType"] = member.MemberType.ToString(),
				["requestedStop"] = null,
				["assignedRoute"] = null,
				["assignedStop"] = null,
				["status"] = "Inactive",
				["remarks"] = member.Remarks
			});

		var rows = cancelledApplications
			.Select(application => ApplicationRow(application, routes, stops))
			.Concat(inactiveMembers)
			.Where(row => MatchesStatus(row.GetValueOrDefault("status"), input.Status))
			.ToList();

		return new ReportTable(ApplicationColumns(), rows);
	}

	private static ReportTable BuildDueReport(TransportReportListRequestDto input, TransportReportContext data, bool onlyPending)
	{
		var rows = data.FeeDues
			.Where(due => !onlyPending || due.Status == TransportFeeDueStatus.Pending)
			.Where(due => !input.RouteId.HasValue || due.RouteId == input.RouteId.Value)
			.Where(due => !input.StopId.HasValue || due.StopId == input.StopId.Value)
			.Where(due => !input.MemberType.HasValue || due.MemberType == input.MemberType.Value)
			.Where(due => MatchesStatus(due.Status.ToString(), input.Status))
			.Where(due => IsWithinDateRange(due.DueDate, input.FromDate, input.ToDate))
			.OrderBy(due => due.DueDate)
			.Select(DueRow)
			.ToList();

		return new ReportTable(DueColumns(), rows);
	}

	private static ReportTable BuildRouteWiseDueReport(TransportReportListRequestDto input, TransportReportContext data)
	{
		var rows = data.FeeDues
			.Where(due => !input.RouteId.HasValue || due.RouteId == input.RouteId.Value)
			.Where(due => !input.StopId.HasValue || due.StopId == input.StopId.Value)
			.GroupBy(due => new { due.RouteId, due.RouteName, due.RouteCode, due.StopId, due.StopName })
			.Select(group => new Dictionary<string, string?>
			{
				["routeCode"] = group.Key.RouteCode,
				["route"] = group.Key.RouteName,
				["stop"] = group.Key.StopName,
				["dueCount"] = group.Count().ToString(CultureInfo.InvariantCulture),
				["pendingCount"] = group.Count(due => due.Status == TransportFeeDueStatus.Pending).ToString(CultureInfo.InvariantCulture),
				["postedCount"] = group.Count(due => due.Status == TransportFeeDueStatus.PostedToFinance).ToString(CultureInfo.InvariantCulture),
				["totalAmount"] = FormatAmount(group.Sum(due => due.Amount)),
				["pendingAmount"] = FormatAmount(group.Where(due => due.Status == TransportFeeDueStatus.Pending).Sum(due => due.Amount))
			})
			.ToList();

		return new ReportTable(
			Columns(("routeCode", "Route Code"), ("route", "Route"), ("stop", "Stop"), ("dueCount", "Dues"), ("pendingCount", "Pending"), ("postedCount", "Posted"), ("totalAmount", "Total Amount"), ("pendingAmount", "Pending Amount")),
			rows);
	}

	private static ReportTable BuildFeeRunReport(TransportReportListRequestDto input, TransportReportContext data)
	{
		var rows = data.FeeRuns
			.Where(run => MatchesStatus(run.Status.ToString(), input.Status))
			.Where(run => IsWithinDateRange(run.PeriodFrom, input.FromDate, input.ToDate))
			.OrderByDescending(run => run.PeriodFrom)
			.Select(run => new Dictionary<string, string?>
			{
				["periodFrom"] = FormatDate(run.PeriodFrom),
				["periodTo"] = FormatDate(run.PeriodTo),
				["dueDate"] = FormatDate(run.DueDate),
				["status"] = run.Status.ToString(),
				["generatedDues"] = run.GeneratedDueCount.ToString(CultureInfo.InvariantCulture),
				["studentDues"] = run.StudentDueCount.ToString(CultureInfo.InvariantCulture),
				["employeeDues"] = run.EmployeeDueCount.ToString(CultureInfo.InvariantCulture),
				["totalAmount"] = FormatAmount(run.TotalAmount),
				["financePostedAt"] = FormatDate(run.FinancePostedAt),
				["financeChargeRequests"] = run.FinanceChargeRequestCount.ToString(CultureInfo.InvariantCulture)
			})
			.ToList();

		return new ReportTable(
			Columns(("periodFrom", "Period From"), ("periodTo", "Period To"), ("dueDate", "Due Date"), ("status", "Status"), ("generatedDues", "Generated"), ("studentDues", "Student Dues"), ("employeeDues", "Employee Dues"), ("totalAmount", "Total Amount"), ("financePostedAt", "Finance Posted At"), ("financeChargeRequests", "Finance Requests")),
			rows);
	}

	private static ReportTable BuildMonthlySummaryReport(TransportReportListRequestDto input, TransportReportContext data)
	{
		var rows = data.FeeDues
			.Where(due => IsWithinDateRange(due.PeriodFrom, input.FromDate, input.ToDate))
			.GroupBy(due => new { due.PeriodFrom.Year, due.PeriodFrom.Month })
			.OrderBy(group => group.Key.Year)
			.ThenBy(group => group.Key.Month)
			.Select(group => new Dictionary<string, string?>
			{
				["month"] = new DateTime(group.Key.Year, group.Key.Month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture),
				["dues"] = group.Count().ToString(CultureInfo.InvariantCulture),
				["studentDues"] = group.Count(due => due.MemberType == TransportMemberType.Student).ToString(CultureInfo.InvariantCulture),
				["employeeDues"] = group.Count(due => due.MemberType == TransportMemberType.Employee).ToString(CultureInfo.InvariantCulture),
				["postedDues"] = group.Count(due => due.Status == TransportFeeDueStatus.PostedToFinance).ToString(CultureInfo.InvariantCulture),
				["pendingDues"] = group.Count(due => due.Status == TransportFeeDueStatus.Pending).ToString(CultureInfo.InvariantCulture),
				["amount"] = FormatAmount(group.Sum(due => due.Amount))
			})
			.ToList();

		return new ReportTable(
			Columns(("month", "Month"), ("dues", "Dues"), ("studentDues", "Student Dues"), ("employeeDues", "Employee Dues"), ("postedDues", "Posted"), ("pendingDues", "Pending"), ("amount", "Amount")),
			rows);
	}

	private static ReportTable BuildPerformanceReport(TransportDashboardSummaryDto summary)
	{
		var rows = new List<Dictionary<string, string?>>
		{
			MetricRow("Active Stops", summary.ActiveStops),
			MetricRow("Active Routes", summary.ActiveRoutes),
			MetricRow("Active Vehicles", summary.ActiveVehicles),
			MetricRow("Active Members", summary.ActiveMembers),
			MetricRow("Pending Applications", summary.PendingApplications),
			MetricRow("Pending Fee Dues", summary.PendingFeeDues),
			MetricRow("Unassigned Routes", summary.UnassignedRoutes),
			MetricRow("Missing Required Documents", summary.MissingRequiredDocuments),
			MetricRow("Expiring Compliance Items", summary.ExpiringComplianceItems),
			MetricRow("Route Capacity Usage %", summary.RouteCapacityUsagePercent)
		};

		return new ReportTable(Columns(("metric", "Metric"), ("value", "Value")), rows);
	}

	private static IEnumerable<TransportVehicle> FilterVehicles(TransportReportListRequestDto input, IEnumerable<TransportVehicle> vehicles) =>
		vehicles
			.Where(vehicle => !input.VehicleId.HasValue || vehicle.Id == input.VehicleId.Value)
			.Where(vehicle => MatchesStatus(vehicle.Status.ToString(), input.Status))
			.OrderBy(vehicle => vehicle.RegistrationNo);

	private static IEnumerable<TransportRoute> FilterRoutes(TransportReportListRequestDto input, IEnumerable<TransportRoute> routes) =>
		routes
			.Where(route => !input.RouteId.HasValue || route.Id == input.RouteId.Value)
			.Where(route => MatchesStatus(route.Status.ToString(), input.Status))
			.OrderBy(route => route.RouteName);

	private static Dictionary<string, string?> VehicleComplianceRow(
		TransportVehicle vehicle,
		string documentType,
		string? referenceNo,
		DateTime? issueDate,
		DateTime? expiryDate,
		string? provider = null)
	{
		return new Dictionary<string, string?>
		{
			["registrationNo"] = vehicle.RegistrationNo,
			["vehicleName"] = vehicle.VehicleName,
			["documentType"] = documentType,
			["referenceNo"] = referenceNo,
			["provider"] = provider,
			["issueDate"] = FormatDate(issueDate),
			["expiryDate"] = FormatDate(expiryDate),
			["status"] = GetExpiryStatus(expiryDate)
		};
	}

	private static Dictionary<string, string?> RouteUsageRow(TransportDashboardRouteUsageDto row) => new()
	{
		["routeCode"] = row.RouteCode,
		["route"] = row.RouteName,
		["capacity"] = row.Capacity.ToString(CultureInfo.InvariantCulture),
		["occupiedSeats"] = row.OccupiedSeats.ToString(CultureInfo.InvariantCulture),
		["remainingSeats"] = row.RemainingSeats.ToString(CultureInfo.InvariantCulture),
		["usagePercent"] = row.UsagePercent.ToString("0.##", CultureInfo.InvariantCulture),
		["assignment"] = row.HasVehicleAssignment ? "Assigned" : "Unassigned",
		["vehicle"] = row.VehicleRegistrationNo,
		["driver"] = row.DriverName
	};

	private static Dictionary<string, string?> ApplicationRow(
		TransportApplication application,
		IReadOnlyDictionary<Guid, TransportRoute> routes,
		IReadOnlyDictionary<Guid, TransportStop> stops)
	{
		return new Dictionary<string, string?>
		{
			["applicationDate"] = FormatDate(application.CreationTime),
			["applicant"] = application.ApplicantName,
			["memberCode"] = application.ApplicantCode,
			["memberType"] = application.MemberType.ToString(),
			["requestedStop"] = application.RequestedStopId.HasValue && stops.TryGetValue(application.RequestedStopId.Value, out var requestedStop) ? requestedStop.StopName : null,
			["assignedRoute"] = application.AssignedRouteId.HasValue && routes.TryGetValue(application.AssignedRouteId.Value, out var assignedRoute) ? assignedRoute.RouteName : null,
			["assignedStop"] = application.AssignedStopId.HasValue && stops.TryGetValue(application.AssignedStopId.Value, out var assignedStop) ? assignedStop.StopName : null,
			["status"] = application.Status.ToString(),
			["remarks"] = application.StatusRemarks
		};
	}

	private static Dictionary<string, string?> DueRow(TransportFeeDue due) => new()
	{
		["memberName"] = due.MemberName,
		["memberCode"] = due.MemberCode,
		["memberType"] = due.MemberType.ToString(),
		["route"] = due.RouteName,
		["stop"] = due.StopName,
		["period"] = $"{FormatDate(due.PeriodFrom)} - {FormatDate(due.PeriodTo)}",
		["dueDate"] = FormatDate(due.DueDate),
		["amount"] = FormatAmount(due.Amount),
		["status"] = due.Status.ToString(),
		["financePostedAt"] = FormatDate(due.FinancePostedAt)
	};

	private static Dictionary<string, string?> MetricRow(string metric, decimal value) => new()
	{
		["metric"] = metric,
		["value"] = value.ToString("0.##", CultureInfo.InvariantCulture)
	};

	private static List<TransportReportColumnDto> RouteCapacityColumns() =>
		Columns(("routeCode", "Route Code"), ("route", "Route"), ("capacity", "Capacity"), ("occupiedSeats", "Occupied"), ("remainingSeats", "Remaining"), ("usagePercent", "Usage %"), ("assignment", "Assignment"), ("vehicle", "Vehicle"), ("driver", "Driver"));

	private static List<TransportReportColumnDto> ApplicationColumns() =>
		Columns(("applicationDate", "Application Date"), ("applicant", "Applicant"), ("memberCode", "Code"), ("memberType", "Type"), ("requestedStop", "Requested Stop"), ("assignedRoute", "Assigned Route"), ("assignedStop", "Assigned Stop"), ("status", "Status"), ("remarks", "Remarks"));

	private static List<TransportReportColumnDto> DueColumns() =>
		Columns(("memberName", "Member"), ("memberCode", "Code"), ("memberType", "Type"), ("route", "Route"), ("stop", "Stop"), ("period", "Period"), ("dueDate", "Due Date"), ("amount", "Amount"), ("status", "Status"), ("financePostedAt", "Finance Posted At"));

	private static List<TransportReportColumnDto> Columns(params (string Key, string Label)[] columns) =>
		columns.Select(column => new TransportReportColumnDto { Key = column.Key, Label = column.Label }).ToList();

	private static int CountMissingRequiredDocuments(TransportReportContext data)
	{
		var requiredDocumentIds = data.RequiredDocuments
			.Where(document => document.IsActive && document.IsRequired)
			.Select(document => document.Id)
			.ToList();
		if (requiredDocumentIds.Count == 0)
		{
			return 0;
		}

		var uploadedDocumentKeys = data.VehicleDocuments
			.Select(document => (document.VehicleId, document.RequiredDocumentId))
			.ToHashSet();
		return data.Vehicles
			.Where(vehicle => vehicle.Status == TransportVehicleStatus.Active)
			.Sum(vehicle => requiredDocumentIds.Count(requiredId => !uploadedDocumentKeys.Contains((vehicle.Id, requiredId))));
	}

	private static int CountExpiringComplianceItems(TransportReportContext data)
	{
		var threshold = DateTime.UtcNow.Date.AddDays(30);
		var complianceByVehicle = data.Compliance.ToDictionary(row => row.VehicleId);
		return data.Vehicles
			.Where(vehicle => vehicle.Status == TransportVehicleStatus.Active)
			.Sum(vehicle =>
			{
				complianceByVehicle.TryGetValue(vehicle.Id, out var compliance);
				return CountVehicleComplianceIssues(compliance, threshold);
			});
	}

	private static int CountVehicleComplianceIssues(TransportVehicleCompliance? compliance, DateTime threshold)
	{
		if (compliance == null)
		{
			return 3;
		}

		return new[] { compliance.InsuranceExpiryDate, compliance.PucExpiryDate, compliance.FitnessExpiryDate }
			.Count(date => !date.HasValue || date.Value.Date <= threshold);
	}

	private static int CountFeeRunsPendingFinancePost(TransportReportContext data)
	{
		return data.FeeRuns.Count(run =>
			run.StudentDueCount > 0 &&
			run.Status != TransportFeeRunStatus.FinancePosted);
	}

	private static TransportDashboardAlertDto Alert(
		string category,
		string severity,
		string title,
		string message,
		Guid? entityId,
		string? entityType,
		DateTime createdAt)
	{
		return new TransportDashboardAlertDto
		{
			Id = $"{category}:{entityType}:{entityId}:{title}".ToLowerInvariant(),
			Category = category,
			Severity = severity,
			Title = title,
			Message = message,
			EntityId = entityId,
			EntityType = entityType,
			CreatedAt = createdAt
		};
	}

	private static int SeverityRank(string severity)
	{
		return severity.Equals("High", StringComparison.OrdinalIgnoreCase) ? 3 :
			severity.Equals("Medium", StringComparison.OrdinalIgnoreCase) ? 2 :
			1;
	}

	private static decimal CalculatePercent(int numerator, int denominator)
	{
		return denominator <= 0 ? 0 : Math.Round((decimal)numerator * 100 / denominator, 2);
	}

	private static string? GetStaffName(
		Guid staffId,
		IReadOnlyDictionary<Guid, TransportStaff> staffById,
		IReadOnlyDictionary<Guid, Employee> employeesById)
	{
		return staffById.TryGetValue(staffId, out var staff) ? GetStaffName(staff, employeesById) : null;
	}

	private static string GetStaffName(TransportStaff staff, IReadOnlyDictionary<Guid, Employee> employeesById)
	{
		if (!employeesById.TryGetValue(staff.EmployeeId, out var employee))
		{
			return string.Empty;
		}

		return string.IsNullOrWhiteSpace(employee.EmployeeName) ? employee.EmployeeCode ?? string.Empty : employee.EmployeeName;
	}

	private static string GetStaffEmployeeCode(TransportStaff staff, IReadOnlyDictionary<Guid, Employee> employeesById)
	{
		return employeesById.TryGetValue(staff.EmployeeId, out var employee) ? employee.EmployeeCode ?? string.Empty : string.Empty;
	}

	private static bool IsWithinDateRange(DateTime value, DateTime? fromDate, DateTime? toDate)
	{
		return (!fromDate.HasValue || value.Date >= fromDate.Value.Date) &&
			(!toDate.HasValue || value.Date <= toDate.Value.Date);
	}

	private static bool IsWithinDateRange(DateTime? value, DateTime? fromDate, DateTime? toDate)
	{
		return !value.HasValue || IsWithinDateRange(value.Value, fromDate, toDate);
	}

	private static bool MatchesStatus(string? actual, string? expected)
	{
		return string.IsNullOrWhiteSpace(expected) ||
			string.Equals(actual, expected.Trim(), StringComparison.OrdinalIgnoreCase);
	}

	private static string GetExpiryStatus(DateTime? expiryDate)
	{
		if (!expiryDate.HasValue)
		{
			return "Missing";
		}

		var today = DateTime.UtcNow.Date;
		if (expiryDate.Value.Date < today)
		{
			return "Expired";
		}

		return expiryDate.Value.Date <= today.AddDays(30) ? "Expiring Soon" : "Valid";
	}

	private static string? FormatDate(DateTime? value) =>
		value?.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture);

	private static string? FormatTime(DateTimeOffset? value) =>
		value?.ToString("hh:mm tt", CultureInfo.InvariantCulture);

	private static string? FormatAmount(decimal? value) =>
		value?.ToString("0.##", CultureInfo.InvariantCulture);

	private static ReportDefinition FindReportDefinition(string reportKey)
	{
		var normalized = NormalizeReportKey(reportKey);
		return ReportDefinitions.FirstOrDefault(definition => definition.Key == normalized) ??
			new ReportDefinition(normalized, reportKey, "Unsupported Reports", false, UnsupportedMessage);
	}

	private static string NormalizeReportKey(string reportKey)
	{
		return string.IsNullOrWhiteSpace(reportKey)
			? string.Empty
			: reportKey.Trim().ToLowerInvariant().Replace(' ', '-');
	}

	private static TransportReportResultDto BuildUnsupportedReport(ReportDefinition definition)
	{
		return new TransportReportResultDto
		{
			Key = definition.Key,
			Title = definition.Title,
			Category = definition.Category,
			IsSupported = false,
			Message = definition.Description,
			Columns = [],
			Items = [],
			TotalCount = 0
		};
	}

	private static string BuildCsv(IReadOnlyCollection<TransportReportColumnDto> columns, IReadOnlyCollection<Dictionary<string, string?>> rows)
	{
		var builder = new StringBuilder();
		builder.AppendLine(string.Join(",", columns.Select(column => EscapeCsv(column.Label))));
		foreach (var row in rows)
		{
			builder.AppendLine(string.Join(",", columns.Select(column => EscapeCsv(row.GetValueOrDefault(column.Key)))));
		}

		return builder.ToString();
	}

	private static string EscapeCsv(string? value)
	{
		var text = value ?? string.Empty;
		if (!text.Contains('"') && !text.Contains(',') && !text.Contains('\n') && !text.Contains('\r'))
		{
			return text;
		}

		return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
	}

	private async Task EnsureCanUseMediumAsync(Guid academicMediumId, string operation)
	{
		await academicMediumRepository.GetAsync(academicMediumId);
		await studentScopeAccessService.EnsureCanAccessMediumAsync(academicMediumId, operation);
	}

	private sealed record ReportDefinition(string Key, string Title, string Category, bool IsSupported, string Description);

	private sealed record TransportReportContext(
		Guid AcademicMediumId,
		Guid? AcademicYearId,
		List<TransportStop> Stops,
		List<TransportRoute> Routes,
		List<TransportRouteStop> RouteStops,
		List<TransportVehicle> Vehicles,
		List<TransportVehicleType> VehicleTypes,
		List<TransportFuelType> FuelTypes,
		List<TransportVehicleCompliance> Compliance,
		List<TransportRequiredVehicleDocument> RequiredDocuments,
		List<TransportVehicleDocument> VehicleDocuments,
		List<TransportVehicleMaintenanceRecord> Maintenance,
		List<TransportStaff> Staff,
		List<TransportRouteAssignment> RouteAssignments,
		List<TransportMember> Members,
		List<TransportMemberRouteAssignment> MemberAssignments,
		List<TransportApplication> Applications,
		List<TransportFeeRun> FeeRuns,
		List<TransportFeeDue> FeeDues,
		List<Employee> Employees);

	private sealed class ReportTable(List<TransportReportColumnDto> columns, List<Dictionary<string, string?>> rows)
	{
		public List<TransportReportColumnDto> Columns { get; } = columns;
		public List<Dictionary<string, string?>> Rows { get; set; } = rows;
	}
}
