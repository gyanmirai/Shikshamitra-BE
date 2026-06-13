using System;
using ShikshaMitra.Common;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveManagementAppService_Tests
{
	[Fact]
	public void IsValidStatusTransition_Cancelled_Should_Be_Valid_From_Pending()
	{
		var leave = CreatePendingLeave();

		leave.Status.ShouldBe(EmployeeLeaveStatusType.Pending);
		leave.Cancel();
		leave.Status.ShouldBe(EmployeeLeaveStatusType.Cancelled);
	}

	[Fact]
	public void Cancel_Should_Throw_From_Approved()
	{
		var leave = CreatePendingLeave();
		leave.UpdateLeaveStatus(EmployeeLeaveStatusType.Approved, null);

		var ex = Should.Throw<BusinessException>(() => leave.Cancel());
		ex.Code.ShouldBe(HRErrorCodes.LeaveNotCancellable);
	}

	[Fact]
	public void Cancel_Should_Throw_From_Rejected()
	{
		var leave = CreatePendingLeave();
		leave.UpdateLeaveStatus(EmployeeLeaveStatusType.Rejected, "reason");

		var ex = Should.Throw<BusinessException>(() => leave.Cancel());
		ex.Code.ShouldBe(HRErrorCodes.LeaveNotCancellable);
	}

	[Fact]
	public void Create_Should_Snapshot_Policy_Rule_And_Leave_Type()
	{
		var leavePolicyId = Guid.NewGuid();
		var leavePolicyRuleId = Guid.NewGuid();
		var leaveTypeId = Guid.NewGuid();

		var leave = EmployeeLeave.Create(
			Guid.NewGuid(),
			Guid.Empty,
			Guid.NewGuid(),
			leavePolicyId,
			leavePolicyRuleId,
			leaveTypeId,
			DateTimeOffset.UtcNow.AddDays(5),
			DateTimeOffset.UtcNow.AddDays(6),
			"test",
			false);

		leave.LeavePolicyId.ShouldBe(leavePolicyId);
		leave.LeavePolicyRuleId.ShouldBe(leavePolicyRuleId);
		leave.LeaveTypeId.ShouldBe(leaveTypeId);
	}

	[Fact]
	public void Submitter_Lookup_Should_Tolerate_Duplicate_Employees_For_Same_User()
	{
		var userId = Guid.NewGuid();
		var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		var rows = new[]
		{
			new LeaveSubmitterEmployeeRow(
				userId,
				"Inactive Recent",
				"EMP-INACTIVE",
				EmployeeStatusType.Inactive,
				createdAt,
				createdAt.AddDays(3)),
			new LeaveSubmitterEmployeeRow(
				userId,
				"Active Older",
				"EMP-OLD",
				EmployeeStatusType.Active,
				createdAt,
				createdAt.AddDays(1)),
			new LeaveSubmitterEmployeeRow(
				userId,
				"Active Recent",
				"EMP-NEW",
				EmployeeStatusType.Active,
				createdAt,
				createdAt.AddDays(2))
		};
		var lookup = LeaveSubmitterResolver.BuildEmployeeByUserIdLookup(rows);

		lookup[userId].Name.ShouldBe("Active Recent");
		lookup[userId].EmployeeCode.ShouldBe("EMP-NEW");
	}

	private static EmployeeLeave CreatePendingLeave() =>
		EmployeeLeave.Create(
			Guid.NewGuid(),
			Guid.Empty,
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			DateTimeOffset.UtcNow.AddDays(5),
			DateTimeOffset.UtcNow.AddDays(6),
			"test",
			false);
}
