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
    private static readonly Guid TestMediumStageId = Guid.NewGuid();

    public LeaveTypeAppService_Tests()
    {
        _leaveTypeAppService = GetRequiredService<ILeaveTypeAppService>();
    }

    #region Create Tests

    [Fact]
    public async Task Should_Create_Leave_Type()
    {
        // Arrange
        var input = new LeaveTypeCreateRequest
        {
            MediumStageId = TestMediumStageId,
            Name = "Test Annual Leave",
            Color = "#FF5733"
        };

        // Act
        var result = await _leaveTypeAppService.CreateLeaveTypeAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        result.Name.ShouldBe(input.Name);
        result.Color.ShouldBe(input.Color);
    }

    [Fact]
    public async Task Should_Not_Create_Duplicate_Type()
    {
        // Arrange
        var leaveTypeName = "Duplicate Leave Type";
        var input1 = new LeaveTypeCreateRequest
        {
            MediumStageId = TestMediumStageId,
            Name = leaveTypeName,
            Color = "#FF0000"
        };

        await _leaveTypeAppService.CreateLeaveTypeAsync(input1);

        var input2 = new LeaveTypeCreateRequest
        {
            MediumStageId = TestMediumStageId,
            Name = leaveTypeName,
            Color = "#00FF00"
        };

        // Act & Assert
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _leaveTypeAppService.CreateLeaveTypeAsync(input2);
        });
    }

    [Fact]
    public async Task Should_Not_Create_Without_Required_Fields()
    {
        // Arrange
        var input = new LeaveTypeCreateRequest
        {
            MediumStageId = TestMediumStageId,
            Name = "", // Empty name
            Color = "#FF0000"
        };

        // Act & Assert — ABP validation interceptor catches empty Name via DTO validation
        await Should.ThrowAsync<Volo.Abp.Validation.AbpValidationException>(async () =>
        {
            await _leaveTypeAppService.CreateLeaveTypeAsync(input);
        });
    }

    [Fact]
    public async Task Should_Trim_Whitespace_From_Type()
    {
        // Arrange
        var input = new LeaveTypeCreateRequest
        {
            MediumStageId = TestMediumStageId,
            Name = "  Test Leave Type With Spaces  ",
            Color = "#FF0000"
        };

        // Act
        var result = await _leaveTypeAppService.CreateLeaveTypeAsync(input);

        // Assert
        result.Name.ShouldBe("Test Leave Type With Spaces");
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
        result.Type.ShouldBe(created.Name);
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

        // Act — DeleteAsync performs soft-delete (deactivation)
        await _leaveTypeAppService.DeleteAsync(created.Id);

        // Assert — entity still exists but is deactivated
        var result = await _leaveTypeAppService.GetAsync(created.Id);
        result.ShouldNotBeNull();
        result.IsActive.ShouldBeFalse();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Should_Handle_Special_Characters_In_Type()
    {
        // Arrange
        var input = new LeaveTypeCreateRequest
        {
            MediumStageId = TestMediumStageId,
            Name = "Leave & Holiday (Special) - Test",
            Color = "#FF0000"
        };

        // Act
        var result = await _leaveTypeAppService.CreateLeaveTypeAsync(input);

        // Assert
        result.Name.ShouldBe(input.Name);
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

    #region Tier 2 — Color Validation & Normalization

    [Fact]
    public async Task Should_Reject_Invalid_Hex_Color()
    {
        // Arrange
        var input = new LeaveTypeCreateRequest
        {
            MediumStageId = TestMediumStageId,
            Name = "Invalid Color Leave",
            Color = "not-a-color"
        };

        // Act & Assert — DTO RegularExpression attribute rejects invalid hex
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _leaveTypeAppService.CreateLeaveTypeAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_Short_Hex_Color()
    {
        // Arrange — 3-digit hex is not valid per regex ^#([A-Fa-f0-9]{6})$
        var input = new LeaveTypeCreateRequest
        {
            MediumStageId = TestMediumStageId,
            Name = "Short Color Leave",
            Color = "#F00"
        };

        // Act & Assert
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _leaveTypeAppService.CreateLeaveTypeAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_Color_Without_Hash()
    {
        // Arrange
        var input = new LeaveTypeCreateRequest
        {
            MediumStageId = TestMediumStageId,
            Name = "No Hash Color Leave",
            Color = "FF5733"
        };

        // Act & Assert
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _leaveTypeAppService.CreateLeaveTypeAsync(input);
        });
    }

    [Fact]
    public async Task Should_Normalize_Lowercase_Color_To_Uppercase()
    {
        // Arrange
        var input = new LeaveTypeCreateRequest
        {
            MediumStageId = TestMediumStageId,
            Name = "Lowercase Color Leave",
            Color = "#ff5733"
        };

        // Act
        var result = await _leaveTypeAppService.CreateLeaveTypeAsync(input);

        // Assert — domain SetColor normalizes to uppercase
        result.Color.ShouldBe("#FF5733");
    }

    [Fact]
    public async Task Should_Normalize_MixedCase_Color_To_Uppercase()
    {
        // Arrange
        var input = new LeaveTypeCreateRequest
        {
            MediumStageId = TestMediumStageId,
            Name = "MixedCase Color Leave",
            Color = "#aAbBcC"
        };

        // Act
        var result = await _leaveTypeAppService.CreateLeaveTypeAsync(input);

        // Assert
        result.Color.ShouldBe("#AABBCC");
    }

    [Fact]
    public async Task Should_Soft_Delete_And_Become_Inactive()
    {
        // Arrange
        var created = await CreateTestLeaveTypeAsync("Soft Delete Test");

        // Act
        await _leaveTypeAppService.DeleteAsync(created.Id);

        // Assert
        var result = await _leaveTypeAppService.GetAsync(created.Id);
        result.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Update_Color_On_Existing_Leave_Type()
    {
        // Arrange
        var created = await CreateTestLeaveTypeAsync("Color Update Test", "#FF0000");

        var updateInput = new UpdateLeaveTypeDto
        {
            Type = "Color Update Test",
            Color = "#00ff00" // lowercase — should normalize
        };

        // Act
        var result = await _leaveTypeAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        result.Color.ShouldBe("#00FF00");
    }

    [Fact]
    public async Task Should_Reject_Empty_Color_On_Update()
    {
        // Arrange
        var created = await CreateTestLeaveTypeAsync("Empty Color Update");

        var updateInput = new UpdateLeaveTypeDto
        {
            Type = "Empty Color Update",
            Color = "" // Empty
        };

        // Act & Assert — domain SetColor calls Check.NotNullOrWhiteSpace
        await Should.ThrowAsync<ArgumentException>(async () =>
        {
            await _leaveTypeAppService.UpdateAsync(created.Id, updateInput);
        });
    }

    #endregion

    #region Helper Methods

    private async Task<LeaveTypeResponse> CreateTestLeaveTypeAsync(
        string type,
        string color = "#FF5733")
    {
        var input = new LeaveTypeCreateRequest
        {
            MediumStageId = TestMediumStageId,
            Name = type,
            Color = color
        };

        return await _leaveTypeAppService.CreateLeaveTypeAsync(input);
    }

    #endregion
}
