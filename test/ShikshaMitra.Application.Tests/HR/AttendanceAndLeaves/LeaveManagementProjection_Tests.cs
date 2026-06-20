using System;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveManagementProjection_Tests
{
	[Fact]
	public void ApprovedLeaveUnits_Should_Use_Persisted_Snapshot_While_TotalDays_Uses_Resolved_Units()
	{
		var leave = CreateApprovedLeave(approvedUnits: 7m);

		var dto = LeaveManagementProjection.ToDetailsDto(
			leave,
			("Lalu Solanki", "LALU"),
			new LeaveManagementUnits(TotalDays: 8m, ApprovedLeaveUnits: leave.ApprovedLeaveUnits));

		dto.TotalDays.ShouldBe(8m);
		dto.ApprovedLeaveUnits.ShouldBe(7m);
	}

	[Fact]
	public void ApprovedLeaveUnits_Should_Match_TotalDays_After_Repair_Snapshot_Is_Corrected()
	{
		var leave = CreateApprovedLeave(approvedUnits: 8m);

		var dto = LeaveManagementProjection.ToDetailsDto(
			leave,
			("Lalu Solanki", "LALU"),
			new LeaveManagementUnits(TotalDays: 8m, ApprovedLeaveUnits: leave.ApprovedLeaveUnits));

		dto.TotalDays.ShouldBe(8m);
		dto.ApprovedLeaveUnits.ShouldBe(8m);
	}

	private static EmployeeLeave CreateApprovedLeave(decimal approvedUnits)
	{
		var leave = EmployeeLeave.Create(
			Guid.NewGuid(),
			tenantId: Guid.NewGuid(),
			employeeId: Guid.NewGuid(),
			leavePolicyId: Guid.NewGuid(),
			leavePolicyRuleId: Guid.NewGuid(),
			LeavePaidType.Paid,
			new DateTimeOffset(new DateTime(2026, 6, 19)),
			new DateTimeOffset(new DateTime(2026, 6, 30)),
			"Approved leave projection regression");
		leave.SetApprovalPayrollSnapshot(LeavePaidType.Paid, approvedUnits);
		leave.UpdateLeaveStatus(EmployeeLeaveStatusType.Approved, rejectionReason: null);
		return leave;
	}
}
