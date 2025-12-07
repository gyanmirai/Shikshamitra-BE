using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

/// <summary>
/// End-to-End tests for Leave Policy App Service
/// </summary>
public class LeavePolicyAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly ILeavePolicyAppService _leavePolicyAppService;
    private readonly ILeaveTypeAppService _leaveTypeAppService;

    public LeavePolicyAppService_Tests()
    {
        _leavePolicyAppService = GetRequiredService<ILeavePolicyAppService>();
        _leaveTypeAppService = GetRequiredService<ILeaveTypeAppService>();
    }

    #region Create Tests

    [Fact]
    public async Task Should_Create_Leave_Policy()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Annual Leave");

        var input = new CreateLeavePolicyDto
        {
            PolicyName = "Test Leave Policy",
            Description = "Standard leave policy for all employees",
            LeaveType = leaveType.Type,
            MaximumDaysPerYear = 20,
            PaidType = LeavePaidType.Paid,
            CarryForward = true,
            Encashable = false,
            RequiresApproval = true
        };

        // Act
        var result = await _leavePolicyAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        result.PolicyName.ShouldBe(input.PolicyName);
        result.Description.ShouldBe(input.Description);
        result.LeaveType.ShouldBe(leaveType.Type);
        result.MaximumDaysPerYear.ShouldBe(20);
        result.CarryForward.ShouldBeTrue();
        result.Encashable.ShouldBeFalse();
        result.RequiresApproval.ShouldBeTrue();
        result.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Create_Policy_Without_CarryForward()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Sick Leave");

        var input = new CreateLeavePolicyDto
        {
            PolicyName = "No Carry Forward Policy",
            LeaveType = leaveType.Type,
            MaximumDaysPerYear = 10,
            PaidType = LeavePaidType.Paid,
            CarryForward = false
        };

        // Act
        var result = await _leavePolicyAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.CarryForward.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Not_Create_Duplicate_PolicyName()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Test Leave");
        var policyName = "Duplicate Policy Test";

        var input1 = new CreateLeavePolicyDto
        {
            PolicyName = policyName,
            LeaveType = leaveType.Type,
            MaximumDaysPerYear = 15,
            PaidType = LeavePaidType.Paid
        };

        await _leavePolicyAppService.CreateAsync(input1);

        var input2 = new CreateLeavePolicyDto
        {
            PolicyName = policyName,
            LeaveType = leaveType.Type,
            MaximumDaysPerYear = 10,
            PaidType = LeavePaidType.Unpaid
        };

        // Act & Assert
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _leavePolicyAppService.CreateAsync(input2);
        });
    }

    [Fact]
    public async Task Should_Not_Create_Without_Required_Fields()
    {
        // Arrange
        var input = new CreateLeavePolicyDto
        {
            PolicyName = "", // Empty name
            LeaveType = "Some Leave",
            MaximumDaysPerYear = 10,
            PaidType = LeavePaidType.Paid
        };

        // Act & Assert
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _leavePolicyAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Not_Create_With_Negative_MaxDays()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Test Leave");

        var input = new CreateLeavePolicyDto
        {
            PolicyName = "Negative Days Policy",
            LeaveType = leaveType.Type,
            MaximumDaysPerYear = -5, // Invalid
            PaidType = LeavePaidType.Paid
        };

        // Act & Assert
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _leavePolicyAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Trim_Whitespace_From_PolicyName()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Test Leave");

        var input = new CreateLeavePolicyDto
        {
            PolicyName = "  Test Policy With Spaces  ",
            LeaveType = leaveType.Type,
            MaximumDaysPerYear = 10,
            PaidType = LeavePaidType.Paid
        };

        // Act
        var result = await _leavePolicyAppService.CreateAsync(input);

        // Assert
        result.PolicyName.ShouldBe("Test Policy With Spaces");
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task Should_Get_Leave_Policy_By_Id()
    {
        // Arrange
        var created = await CreateTestLeavePolicyAsync("Get By ID Policy");

        // Act
        var result = await _leavePolicyAppService.GetAsync(created.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.PolicyName.ShouldBe(created.PolicyName);
        result.LeaveType.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_Throw_Exception_When_Policy_Not_Found()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _leavePolicyAppService.GetAsync(nonExistentId);
        });
    }

    [Fact]
    public async Task Should_Get_All_Active_Policies()
    {
        // Arrange
        await CreateTestLeavePolicyAsync("Active Policy 1");
        await CreateTestLeavePolicyAsync("Active Policy 2");
        await CreateTestLeavePolicyAsync("Active Policy 3");

        var input = new PagedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        };

        // Act
        var result = await _leavePolicyAppService.GetAllActiveAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Should_Get_Paginated_List()
    {
        // Arrange
        for (int i = 1; i <= 15; i++)
        {
            await CreateTestLeavePolicyAsync($"Paginated Policy {i}");
        }

        var input = new PagedAndSortedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 10
        };

        // Act
        var result = await _leavePolicyAppService.GetListAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeLessThanOrEqualTo(10);
        result.TotalCount.ShouldBeGreaterThanOrEqualTo(15);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Should_Update_Leave_Policy()
    {
        // Arrange
        var created = await CreateTestLeavePolicyAsync("Original Policy");
        var newLeaveType = await CreateTestLeaveTypeAsync("New Leave Type");

        var updateInput = new UpdateLeavePolicyDto
        {
            PolicyName = "Updated Policy",
            Description = "Updated description",
            LeaveType = newLeaveType.Type,
            MaximumDaysPerYear = 25,
            PaidType = LeavePaidType.Unpaid,
            CarryForward = false,
            Encashable = true
        };

        // Act
        var result = await _leavePolicyAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.PolicyName.ShouldBe("Updated Policy");
        result.Description.ShouldBe("Updated description");
        result.LeaveType.ShouldBe(newLeaveType.Type);
        result.MaximumDaysPerYear.ShouldBe(25);
        result.CarryForward.ShouldBeFalse();
        result.Encashable.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Not_Update_To_Duplicate_PolicyName()
    {
        // Arrange
        var policy1 = await CreateTestLeavePolicyAsync("Policy 1");
        var policy2 = await CreateTestLeavePolicyAsync("Policy 2");

        var updateInput = new UpdateLeavePolicyDto
        {
            PolicyName = "Policy 1", // Trying to use existing name
            LeaveType = policy2.LeaveType,
            MaximumDaysPerYear = 10,
            PaidType = LeavePaidType.Paid
        };

        // Act & Assert
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _leavePolicyAppService.UpdateAsync(policy2.Id, updateInput);
        });
    }

    [Fact]
    public async Task Should_Allow_Update_With_Same_PolicyName()
    {
        // Arrange
        var created = await CreateTestLeavePolicyAsync("Same Name Policy");

        var updateInput = new UpdateLeavePolicyDto
        {
            PolicyName = "Same Name Policy", // Same name
            Description = "Updated description only",
            LeaveType = created.LeaveType,
            MaximumDaysPerYear = created.MaximumDaysPerYear,
            PaidType = LeavePaidType.Paid
        };

        // Act
        var result = await _leavePolicyAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.PolicyName.ShouldBe("Same Name Policy");
        result.Description.ShouldBe("Updated description only");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Should_Delete_Leave_Policy()
    {
        // Arrange
        var created = await CreateTestLeavePolicyAsync("Policy to Delete");

        // Act
        await _leavePolicyAppService.DeleteAsync(created.Id);

        // Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _leavePolicyAppService.GetAsync(created.Id);
        });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Should_Handle_Special_Characters_In_PolicyName()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Test Leave");

        var input = new CreateLeavePolicyDto
        {
            PolicyName = "Policy & Rules (2024) - Test",
            LeaveType = leaveType.Type,
            MaximumDaysPerYear = 10,
            PaidType = LeavePaidType.Paid
        };

        // Act
        var result = await _leavePolicyAppService.CreateAsync(input);

        // Assert
        result.PolicyName.ShouldBe(input.PolicyName);
    }

    [Fact]
    public async Task Should_Handle_Large_MaxDays()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Extended Leave");

        var input = new CreateLeavePolicyDto
        {
            PolicyName = "Long Duration Policy",
            LeaveType = leaveType.Type,
            MaximumDaysPerYear = 365,
            PaidType = LeavePaidType.Unpaid
        };

        // Act
        var result = await _leavePolicyAppService.CreateAsync(input);

        // Assert
        result.MaximumDaysPerYear.ShouldBe(365);
    }

    [Fact]
    public async Task Should_Handle_Empty_Description()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Test Leave");

        var input = new CreateLeavePolicyDto
        {
            PolicyName = "No Description Policy",
            Description = null,
            LeaveType = leaveType.Type,
            MaximumDaysPerYear = 10,
            PaidType = LeavePaidType.Paid
        };

        // Act
        var result = await _leavePolicyAppService.CreateAsync(input);

        // Assert
        result.Description.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Create_With_All_PaidTypes()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Test Leave");

        // Act
        var paidPolicy = await CreateTestLeavePolicyAsync("Paid Policy", leaveType.Type, LeavePaidType.Paid);
        var unpaidPolicy = await CreateTestLeavePolicyAsync("Unpaid Policy", leaveType.Type, LeavePaidType.Unpaid);
        var halfPaidPolicy = await CreateTestLeavePolicyAsync("HalfPaid Policy", leaveType.Type, LeavePaidType.HalfPaid);

        // Assert
        paidPolicy.PaidType.ShouldBe("Paid");
        unpaidPolicy.PaidType.ShouldBe("Unpaid");
        halfPaidPolicy.PaidType.ShouldBe("HalfPaid");
    }

    #endregion

    #region Helper Methods

    private async Task<LeavePolicyDto> CreateTestLeavePolicyAsync(
        string policyName,
        string? leaveTypeName = null,
        LeavePaidType paidType = LeavePaidType.Paid,
        int maxDaysPerYear = 15)
    {
        // Create a leave type if not provided
        if (string.IsNullOrEmpty(leaveTypeName))
        {
            var leaveType = await CreateTestLeaveTypeAsync($"Leave Type for {policyName}");
            leaveTypeName = leaveType.Type;
        }

        var input = new CreateLeavePolicyDto
        {
            PolicyName = policyName,
            Description = $"Test description for {policyName}",
            LeaveType = leaveTypeName,
            MaximumDaysPerYear = maxDaysPerYear,
            PaidType = paidType,
            CarryForward = false,
            Encashable = false,
            RequiresApproval = true
        };

        return await _leavePolicyAppService.CreateAsync(input);
    }

    private async Task<LeaveTypeDto> CreateTestLeaveTypeAsync(string type)
    {
        var input = new CreateLeaveTypeDto
        {
            Type = type,
            Color = "#FF5733"
        };

        return await _leaveTypeAppService.CreateAsync(input);
    }

    #endregion
}
