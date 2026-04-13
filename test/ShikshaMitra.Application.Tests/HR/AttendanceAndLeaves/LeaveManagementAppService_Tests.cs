using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveManagementAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly ILeaveManagementAppService _leaveManagement;

    public LeaveManagementAppService_Tests()
    {
        _leaveManagement = GetRequiredService<ILeaveManagementAppService>();
    }

    [Fact]
    public async Task GetListAsync_Should_Return_Empty_For_New_Tenant()
    {
        var result = await _leaveManagement.GetListAsync(Guid.NewGuid(), null, null, 1, 10);

        result.ShouldNotBeNull();
        result.TotalCount.ShouldBe(0);
        result.Items.ShouldNotBeNull();
        result.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetListAsync_Should_Reject_Invalid_Page()
    {
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await _leaveManagement.GetListAsync(Guid.NewGuid(), null, null, 0, 10));

        ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidPage);
    }

    [Fact]
    public async Task GetListAsync_Should_Reject_PageSize_Below_Minimum()
    {
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await _leaveManagement.GetListAsync(Guid.NewGuid(), null, null, 1, 5));

        ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidPageSize);
    }

    [Fact]
    public async Task GetListAsync_Should_Reject_PageSize_Above_Maximum()
    {
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await _leaveManagement.GetListAsync(Guid.NewGuid(), null, null, 1, 200));

        ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidPageSize);
    }

    [Fact]
    public async Task GetListAsync_Should_Reject_Invalid_Month()
    {
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await _leaveManagement.GetListAsync(Guid.NewGuid(), 13, 2024, 1, 10));

        ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidMonth);
    }

    [Fact]
    public async Task GetListAsync_Should_Reject_Invalid_Year()
    {
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await _leaveManagement.GetListAsync(Guid.NewGuid(), 6, 1800, 1, 10));

        ex.Code.ShouldBe(HRErrorCodes.LeaveInvalidYear);
    }

    [Fact]
    public void IsValidStatusTransition_Cancelled_Should_Be_Valid_From_Pending()
    {
        // Verify via reflection or indirectly that the transition table is correct.
        // Since IsValidStatusTransition is private, we test the domain entity directly.
        var leave = EmployeeLeave.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(6),
            "test", false);

        leave.Status.ShouldBe(EmployeeLeaveStatusType.Pending);
        leave.Cancel();
        leave.Status.ShouldBe(EmployeeLeaveStatusType.Cancelled);
    }

    [Fact]
    public void Cancel_Should_Throw_From_Approved()
    {
        var leave = EmployeeLeave.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(6),
            "test", false);

        leave.UpdateLeaveStatus(EmployeeLeaveStatusType.Approved, null);

        var ex = Should.Throw<BusinessException>(() => leave.Cancel());
        ex.Code.ShouldBe(HRErrorCodes.LeaveNotCancellable);
    }

    [Fact]
    public void Cancel_Should_Throw_From_Rejected()
    {
        var leave = EmployeeLeave.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(6),
            "test", false);

        leave.UpdateLeaveStatus(EmployeeLeaveStatusType.Rejected, "reason");

        var ex = Should.Throw<BusinessException>(() => leave.Cancel());
        ex.Code.ShouldBe(HRErrorCodes.LeaveNotCancellable);
    }

    [Fact]
    public void Cancel_Should_Throw_From_Already_Cancelled()
    {
        var leave = EmployeeLeave.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(6),
            "test", false);

        leave.Cancel(); // First cancel succeeds

        var ex = Should.Throw<BusinessException>(() => leave.Cancel());
        ex.Code.ShouldBe(HRErrorCodes.LeaveNotCancellable);
    }
}
