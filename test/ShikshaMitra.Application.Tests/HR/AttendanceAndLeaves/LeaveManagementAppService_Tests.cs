using System;
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
