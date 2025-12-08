using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

/// <summary>
/// End-to-End tests for Leave Type App Service
/// </summary>
public class LeaveTypeAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly ILeaveTypeAppService _leaveTypeAppService;

    public LeaveTypeAppService_Tests()
    {
        _leaveTypeAppService = GetRequiredService<ILeaveTypeAppService>();
    }

    #region Create Tests

    [Fact]
    public async Task Should_Create_Leave_Type()
    {
        // Arrange
        var input = new CreateLeaveTypeDto
        {
            Type = "Test Annual Leave",
            Color = "#FF5733"
        };

        // Act
        var result = await _leaveTypeAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        result.Type.ShouldBe(input.Type);
        result.Color.ShouldBe(input.Color);
        result.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Not_Create_Duplicate_Type()
    {
        // Arrange
        var leaveTypeName = "Duplicate Leave Type";
        var input1 = new CreateLeaveTypeDto
        {
            Type = leaveTypeName,
            Color = "#FF0000"
        };

        await _leaveTypeAppService.CreateAsync(input1);

        var input2 = new CreateLeaveTypeDto
        {
            Type = leaveTypeName,
            Color = "#00FF00"
        };

        // Act & Assert
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _leaveTypeAppService.CreateAsync(input2);
        });
    }

    [Fact]
    public async Task Should_Not_Create_Without_Required_Fields()
    {
        // Arrange
        var input = new CreateLeaveTypeDto
        {
            Type = "", // Empty type
            Color = "#FF0000"
        };

        // Act & Assert
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _leaveTypeAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Trim_Whitespace_From_Type()
    {
        // Arrange
        var input = new CreateLeaveTypeDto
        {
            Type = "  Test Leave Type With Spaces  ",
            Color = "#FF0000"
        };

        // Act
        var result = await _leaveTypeAppService.CreateAsync(input);

        // Assert
        result.Type.ShouldBe("Test Leave Type With Spaces");
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task Should_Get_Leave_Type_By_Id()
    {
        // Arrange
        var created = await CreateTestLeaveTypeAsync("Get By ID Leave Test");

        // Act
        var result = await _leaveTypeAppService.GetAsync(created.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.Type.ShouldBe(created.Type);
    }

    [Fact]
    public async Task Should_Throw_Exception_When_Leave_Type_Not_Found()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _leaveTypeAppService.GetAsync(nonExistentId);
        });
    }

    [Fact]
    public async Task Should_Get_Paginated_List()
    {
        // Arrange
        for (int i = 1; i <= 15; i++)
        {
            await CreateTestLeaveTypeAsync($"Paginated Leave {i}");
        }

        var input = new PagedAndSortedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 10
        };

        // Act
        var result = await _leaveTypeAppService.GetListAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeLessThanOrEqualTo(10);
        result.TotalCount.ShouldBeGreaterThanOrEqualTo(15);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Should_Update_Leave_Type()
    {
        // Arrange
        var created = await CreateTestLeaveTypeAsync("Original Leave Type");

        var updateInput = new UpdateLeaveTypeDto
        {
            Type = "Updated Leave Type",
            Color = "#00FF00"
        };

        // Act
        var result = await _leaveTypeAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.Type.ShouldBe("Updated Leave Type");
        result.Color.ShouldBe("#00FF00");
    }

    [Fact]
    public async Task Should_Not_Update_To_Duplicate_Type()
    {
        // Arrange
        var leaveType1 = await CreateTestLeaveTypeAsync("Leave Type 1");
        var leaveType2 = await CreateTestLeaveTypeAsync("Leave Type 2");

        var updateInput = new UpdateLeaveTypeDto
        {
            Type = "Leave Type 1", // Trying to use existing type
            Color = "#FF0000"
        };

        // Act & Assert
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _leaveTypeAppService.UpdateAsync(leaveType2.Id, updateInput);
        });
    }

    [Fact]
    public async Task Should_Allow_Update_With_Same_Type()
    {
        // Arrange
        var created = await CreateTestLeaveTypeAsync("Same Type Leave");

        var updateInput = new UpdateLeaveTypeDto
        {
            Type = "Same Type Leave", // Same type
            Color = "#0000FF" // Updated color only
        };

        // Act
        var result = await _leaveTypeAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.Type.ShouldBe("Same Type Leave");
        result.Color.ShouldBe("#0000FF");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Should_Delete_Leave_Type()
    {
        // Arrange
        var created = await CreateTestLeaveTypeAsync("Leave to Delete");

        // Act
        await _leaveTypeAppService.DeleteAsync(created.Id);

        // Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _leaveTypeAppService.GetAsync(created.Id);
        });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Should_Handle_Special_Characters_In_Type()
    {
        // Arrange
        var input = new CreateLeaveTypeDto
        {
            Type = "Leave & Holiday (Special) - Test",
            Color = "#FF0000"
        };

        // Act
        var result = await _leaveTypeAppService.CreateAsync(input);

        // Assert
        result.Type.ShouldBe(input.Type);
    }

    [Fact]
    public async Task Should_Handle_Various_Color_Codes()
    {
        // Arrange & Act
        var result1 = await CreateTestLeaveTypeAsync("Leave 1", "#FF0000");
        var result2 = await CreateTestLeaveTypeAsync("Leave 2", "#00FF00");
        var result3 = await CreateTestLeaveTypeAsync("Leave 3", "#0000FF");

        // Assert
        result1.Color.ShouldBe("#FF0000");
        result2.Color.ShouldBe("#00FF00");
        result3.Color.ShouldBe("#0000FF");
    }

    #endregion

    #region Helper Methods

    private async Task<LeaveTypeDto> CreateTestLeaveTypeAsync(
        string type,
        string color = "#FF5733")
    {
        var input = new CreateLeaveTypeDto
        {
            Type = type,
            Color = color
        };

        return await _leaveTypeAppService.CreateAsync(input);
    }

    #endregion
}
