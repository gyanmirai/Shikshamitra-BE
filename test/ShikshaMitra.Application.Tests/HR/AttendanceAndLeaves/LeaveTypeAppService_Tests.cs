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

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _leaveTypeAppService.UpdateAsync(created.Id, updateInput);
        });
    }

    #endregion

    #region Tier 4 — Deactivation & Active List Lifecycle

    [Fact]
    public async Task Should_Exclude_Deactivated_LeaveType_From_Active_List()
    {
        // Arrange
        var type1 = await CreateTestLeaveTypeAsync("Active Type For List");
        var type2 = await CreateTestLeaveTypeAsync("Will Deactivate For List");

        // Act — deactivate one
        await _leaveTypeAppService.DeleteAsync(type2.Id);

        // Assert — active list should exclude the deactivated type
        var activeList = await _leaveTypeAppService.GetAllActiveAsync(new PagedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });

        activeList.Items.ShouldContain(x => x.Id == type1.Id);
        activeList.Items.ShouldNotContain(x => x.Id == type2.Id);
    }

    [Fact]
    public async Task Should_Include_Deactivated_LeaveType_In_GetListAsync()
    {
        // Arrange — GetListAsync returns all (active + inactive)
        var type = await CreateTestLeaveTypeAsync("Deactivated In FullList");
        await _leaveTypeAppService.DeleteAsync(type.Id);

        // Act
        var allList = await _leaveTypeAppService.GetListAsync(new PagedAndSortedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });

        // Assert — deactivated type should still appear
        allList.Items.ShouldContain(x => x.Id == type.Id);
        var found = allList.Items.First(x => x.Id == type.Id);
        found.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Handle_Full_LeaveType_Lifecycle()
    {
        // Step 1: Create
        var created = await CreateTestLeaveTypeAsync("Full Lifecycle Type", "#AABB00");
        created.Name.ShouldBe("Full Lifecycle Type");
        created.Color.ShouldBe("#AABB00");

        // Step 2: Update — change name and color
        var updateInput = new UpdateLeaveTypeDto
        {
            Type = "Full Lifecycle Type Updated",
            Color = "#00CCDD",
            Description = "Updated via lifecycle test"
        };
        var updated = await _leaveTypeAppService.UpdateAsync(created.Id, updateInput);
        updated.Type.ShouldBe("Full Lifecycle Type Updated");
        updated.Color.ShouldBe("#00CCDD");

        // Step 3: Verify via GetAsync
        var retrieved = await _leaveTypeAppService.GetAsync(created.Id);
        retrieved.Type.ShouldBe("Full Lifecycle Type Updated");
        retrieved.IsActive.ShouldBeTrue();

        // Step 4: Soft-delete (deactivate)
        await _leaveTypeAppService.DeleteAsync(created.Id);
        var deactivated = await _leaveTypeAppService.GetAsync(created.Id);
        deactivated.IsActive.ShouldBeFalse();
        deactivated.Type.ShouldBe("Full Lifecycle Type Updated"); // Data preserved
    }

    [Fact]
    public async Task Should_Allow_Multiple_Sequential_Updates_To_Same_LeaveType()
    {
        // Arrange
        var created = await CreateTestLeaveTypeAsync("Sequential Update Type");

        // Act — update multiple times in sequence
        for (int i = 1; i <= 5; i++)
        {
            var updateInput = new UpdateLeaveTypeDto
            {
                Type = $"Sequential Update Type V{i}",
                Color = $"#00{i:D2}{i:D2}00".Substring(0, 7) // generates valid hex
            };
            await _leaveTypeAppService.UpdateAsync(created.Id, updateInput);
        }

        // Assert — should reflect the last update
        var final = await _leaveTypeAppService.GetAsync(created.Id);
        final.Type.ShouldBe("Sequential Update Type V5");
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
