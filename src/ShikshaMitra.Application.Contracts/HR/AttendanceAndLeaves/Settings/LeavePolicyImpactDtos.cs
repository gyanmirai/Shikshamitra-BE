using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.HR.Policies;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeavePolicyReviewScope
{
	public DateTime AsOfDate { get; set; }
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
	public int? ExpectedVersion { get; set; }
}

public class LeavePolicyImpactRequest
{
	public Guid AcademicYearId { get; set; }
	public Guid? PolicyId { get; set; }
	public string Action { get; set; } = "update";
	public DateTime AsOfDate { get; set; }
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
	public int? ExpectedVersion { get; set; }
	public LeavePolicyCreateRequest? Proposal { get; set; }
}

public interface ILeavePolicyImpactAppService : IApplicationService
{
	Task<LeavePolicyImpactReviewDto> PreviewAsync(LeavePolicyImpactRequest input);
	Task<LeavePolicyImpactPageDto> GetDetailsAsync(LeavePolicyImpactDetailsRequest input);
}

public class LeavePolicyImpactReviewDto
{
	public Guid? PolicyId { get; set; }
	public string Action { get; set; } = string.Empty;
	public int? CurrentVersion { get; set; }
	public int? ProposedVersion { get; set; }
	public LeavePolicyImpactDto Impact { get; set; } = new();
	public bool RequiresConfirmation { get; set; }
	public string? ConfirmationToken { get; set; }
	public DateTime? ExpiresAt { get; set; }
	public List<HrPolicyImpactWarningDto> Warnings { get; set; } = new();
	public List<HrPolicyImpactWarningDto> BlockingWarnings { get; set; } = new();
	public List<HrPolicyVersionDifferenceDto> Differences { get; set; } = new();
}

public class LeavePolicyImpactDetailsRequest
{
	public LeavePolicyImpactRequest Assessment { get; set; } = new();
	public string Fingerprint { get; set; } = string.Empty;
	public string Metric { get; set; } = string.Empty;
	public int SkipCount { get; set; }
	public int MaxResultCount { get; set; } = 20;
}

public class LeavePolicyImpactPageDto
{
	public int AssessedTotalCount { get; set; }
	public int TotalCount { get; set; }
	public int RedactedCount { get; set; }
	public bool IsScopeRestricted { get; set; }
	public List<LeavePolicyImpactRowDto> Items { get; set; } = new();
}

public class LeavePolicyImpactRowDto
{
	public Guid Id { get; set; }
	public Guid? EmployeeId { get; set; }
	public string Label { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public string? CurrentSource { get; set; }
	public string? ProposedSource { get; set; }
	public Guid? CurrentPolicyId { get; set; }
	public Guid? ProposedPolicyId { get; set; }
	public string? Period { get; set; }
	public bool? IsLocked { get; set; }
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
}

public class LeaveImpactMeasure
{
	public string Status { get; set; } = "not_calculated";
	public int? Count { get; set; }
	public string Definition { get; set; } = string.Empty;
	public string Scope { get; set; } = "requested_window";
	public string? AvailabilityReason { get; set; }
	public string? DrilldownKey { get; set; }
}

public class LeavePayrollExposurePeriodDto
{
	public string Month { get; set; } = string.Empty;
	public int SnapshotEmployeeCount { get; set; }
	public int SettlementEmployeeCount { get; set; }
	public int LockedSnapshotEmployeeCount { get; set; }
	public string LockState { get; set; } = "no_snapshot";
}

public class LeavePolicyImpactDto
{
	public DateTime CalculatedAt { get; set; }
	public DateTime AsOfDate { get; set; }
	public DateTime FromDate { get; set; }
	public DateTime ToDate { get; set; }
	public string Completeness { get; set; } = "not_calculated";
	public string Fingerprint { get; set; } = string.Empty;
	public string? Limitation { get; set; }
	public List<LeavePayrollExposurePeriodDto> PayrollPeriods { get; set; } = new();
	public Dictionary<string, LeaveImpactMeasure> Measures { get; set; } = new();
}
