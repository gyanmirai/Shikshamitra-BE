using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Transport;

public class TransportReportPlanGuard : ITransientDependency
{
	private static readonly string[] VehicleStatuses = Enum.GetNames<TransportVehicleStatus>();
	private static readonly string[] ComplianceStatuses = ["Missing", "Expired", "Expiring Soon", "Valid"];
	private static readonly string[] RecordStatuses = Enum.GetNames<TransportRecordStatus>();
	private static readonly string[] StaffStatuses = Enum.GetNames<TransportStaffStatus>();
	private static readonly string[] MemberStatuses = Enum.GetNames<TransportMemberStatus>();
	private static readonly string[] ApplicationStatuses = Enum.GetNames<TransportApplicationStatus>();
	private static readonly string[] FeeDueStatuses = Enum.GetNames<TransportFeeDueStatus>();
	private static readonly string[] FeeRunStatuses = Enum.GetNames<TransportFeeRunStatus>();

	private static readonly IReadOnlyDictionary<string, ReportRules> Rules =
		new Dictionary<string, ReportRules>(StringComparer.Ordinal)
		{
			["vehicle-master"] = Rule(["vehicle", "status"], VehicleStatuses),
			["vehicle-availability-status"] = Rule(["vehicle", "status"], VehicleStatuses),
			["vehicle-fitness-inspection"] = Rule(["vehicle", "status"], ComplianceStatuses),
			["vehicle-insurance-expiry"] = Rule(["vehicle", "status"], ComplianceStatuses),
			["vehicle-pollution-certificate-expiry"] = Rule(["vehicle", "status"], ComplianceStatuses),
			["vehicle-permit-status"] = Rule(["vehicle", "status"], ComplianceStatuses),
			["vehicle-maintenance-schedule"] = Rule(["vehicle", "dates"]),
			["vehicle-repair-service-history"] = Rule(["vehicle", "dates"]),
			["route-master"] = Rule(["route", "status"], RecordStatuses),
			["route-wise-student-allocation"] = Rule(["route", "stop", "memberType", "status"], MemberStatuses),
			["route-capacity-utilization"] = Rule(["route"]),
			["bus-stop-master"] = Rule(["stop", "status"], RecordStatuses),
			["pickup-drop-route-timing"] = Rule(["route", "stop"]),
			["route-distance-fuel-estimate"] = Rule(["route", "status"], RecordStatuses),
			["driver-master"] = Rule(["staff", "status"], StaffStatuses),
			["conductor-assistant-master"] = Rule(["staff", "status"], StaffStatuses),
			["driver-license-expiry"] = Rule(["staff", "status"], StaffStatuses),
			["driver-conductor-assignment-history"] = Rule(["route", "vehicle", "staff", "dates"]),
			["transport-registered-students"] = Rule(["route", "stop", "memberType", "status"], MemberStatuses),
			["student-pickup-drop-change-request"] = Rule(["route", "stop", "memberType", "status", "dates"], ApplicationStatuses),
			["student-transport-cancellation"] = Rule(["status"], ["Cancelled", "Inactive"]),
			["new-transport-enrolment"] = Rule(["route", "stop", "memberType", "status", "dates"], ApplicationStatuses),
			["transport-fee-pending"] = Rule(["route", "stop", "memberType", "status", "dates"], FeeDueStatuses),
			["route-wise-transport-fee"] = Rule(["route", "stop"]),
			["fee-run-posting-summary"] = Rule(["status", "dates"], FeeRunStatuses),
			["monthly-transportation-summary"] = Rule(["dates"]),
			["transport-performance-dashboard"] = Rule([])
		};

	public static IReadOnlyCollection<string> SupportedReportKeys { get; } = Rules.Keys.ToArray();

	public static IReadOnlyCollection<string> AllowedFilters(string reportKey) =>
		Rules.TryGetValue(reportKey, out var rules) ? rules.Filters : [];

	public static IReadOnlyCollection<string> AllowedStatuses(string reportKey) =>
		Rules.TryGetValue(reportKey, out var rules) ? rules.Statuses : [];

	public void Validate(TransportAiReportExecutionPlanDto plan, Guid expectedMediumId, Guid? expectedAcademicYearId)
	{
		if (!Rules.TryGetValue(plan.ReportKey, out var rules) ||
			plan.Query.AcademicMediumId == Guid.Empty ||
			plan.Query.AcademicMediumId != expectedMediumId ||
			plan.Query.AcademicYearId != expectedAcademicYearId ||
			plan.Query.PageNumber != 1 ||
			plan.Query.PageSize is < 1 or > 200 ||
			!string.Equals(plan.Format, "csv", StringComparison.OrdinalIgnoreCase) ||
			(plan.Query.FromDate.HasValue && plan.Query.ToDate.HasValue && plan.Query.FromDate.Value.Date > plan.Query.ToDate.Value.Date) ||
			(plan.Query.MemberType.HasValue && !Enum.IsDefined(plan.Query.MemberType.Value)) ||
			(!string.IsNullOrWhiteSpace(plan.Query.Status) && !rules.Statuses.Contains(plan.Query.Status.Trim(), StringComparer.OrdinalIgnoreCase)))
		{
			throw new BusinessException(TransportErrorCodes.ReportRequestInvalid);
		}

		var usedFilters = new Dictionary<string, bool>
		{
			["dates"] = plan.Query.FromDate.HasValue || plan.Query.ToDate.HasValue,
			["route"] = plan.Query.RouteId.HasValue,
			["stop"] = plan.Query.StopId.HasValue,
			["vehicle"] = plan.Query.VehicleId.HasValue,
			["staff"] = plan.Query.StaffId.HasValue,
			["memberType"] = plan.Query.MemberType.HasValue,
			["status"] = !string.IsNullOrWhiteSpace(plan.Query.Status)
		};
		if (usedFilters.Any(filter => filter.Value && !rules.Filters.Contains(filter.Key, StringComparer.Ordinal)))
		{
			throw new BusinessException(TransportErrorCodes.ReportRequestInvalid);
		}
	}

	private static ReportRules Rule(string[] filters, string[]? statuses = null) =>
		new(filters, statuses ?? []);

	private sealed record ReportRules(string[] Filters, string[] Statuses);
}
