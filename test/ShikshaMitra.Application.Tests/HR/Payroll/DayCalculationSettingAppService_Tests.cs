using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

/// <summary>
/// End-to-End tests for Day Calculation Setting App Service — Tier 4 Advanced Scenarios
/// Tests upsert workflow, day calculation types, ManualDays validation, and idempotent updates.
/// </summary>
public class DayCalculationSettingAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly IDayCalculationSettingAppService _dayCalculationSettingAppService;

    public DayCalculationSettingAppService_Tests()
    {
        _dayCalculationSettingAppService = GetRequiredService<IDayCalculationSettingAppService>();
    }

    #region Save — Working Days

    [Fact]
    public async Task Should_Save_WorkingDays_Setting()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "working-days"
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_Save_TotalMonthDays_Setting()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "total-month-days"
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert
        result.Success.ShouldBeTrue();
    }

    #endregion

    #region Save — Manual Days

    [Fact]
    public async Task Should_Save_ManualDays_Setting()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "manual-days",
            ManualDays = 22
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Accept_ManualDays_At_Minimum_Boundary()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "manual-days",
            ManualDays = 1
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Accept_ManualDays_At_Maximum_Boundary()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "manual-days",
            ManualDays = 31
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Reject_ManualDays_Zero()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "manual-days",
            ManualDays = 0
        };

        // Act & Assert — domain SetFields rejects ManualDays outside 1-31
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _dayCalculationSettingAppService.SaveAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_ManualDays_Over_31()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "manual-days",
            ManualDays = 32
        };

        // Act & Assert — domain SetFields rejects ManualDays > 31
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _dayCalculationSettingAppService.SaveAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_ManualDays_Null_When_Type_Is_ManualDays()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "manual-days",
            ManualDays = null
        };

        // Act & Assert — domain SetFields rejects null ManualDays for manual-days type
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _dayCalculationSettingAppService.SaveAsync(input);
        });
    }

    #endregion

    #region Save — Invalid Type

    [Fact]
    public async Task Should_Reject_Invalid_DayCalculationType()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "invalid-type"
        };

        // Act & Assert — ParseDayCalculationType throws BusinessException for unrecognized type
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _dayCalculationSettingAppService.SaveAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_Empty_DayCalculationType()
    {
        // Arrange
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = ""
        };

        // Act & Assert — ParseDayCalculationType throws BusinessException for empty string
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _dayCalculationSettingAppService.SaveAsync(input);
        });
    }

    #endregion

    #region Upsert Workflow

    [Fact]
    public async Task Should_Handle_Upsert_Create_Then_Update()
    {
        // Step 1: First save creates the setting
        var createInput = new DayCalculationRequestDto
        {
            DayCalculationType = "working-days"
        };
        var createResult = await _dayCalculationSettingAppService.SaveAsync(createInput);
        createResult.Success.ShouldBeTrue();

        // Step 2: Second save updates the existing setting
        var updateInput = new DayCalculationRequestDto
        {
            DayCalculationType = "manual-days",
            ManualDays = 25
        };
        var updateResult = await _dayCalculationSettingAppService.SaveAsync(updateInput);
        updateResult.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Handle_Multiple_Upsert_Cycles()
    {
        // Cycle through all three types — each Save should succeed
        var types = new[]
        {
            ("working-days", (int?)null),
            ("total-month-days", (int?)null),
            ("manual-days", (int?)15),
            ("working-days", (int?)null),
            ("manual-days", (int?)28)
        };

        foreach (var (calcType, manualDays) in types)
        {
            var input = new DayCalculationRequestDto
            {
                DayCalculationType = calcType,
                ManualDays = manualDays
            };

            var result = await _dayCalculationSettingAppService.SaveAsync(input);
            result.Success.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Should_Accept_Case_Insensitive_DayCalculationType()
    {
        // Arrange — FluentValidation lowercases before comparison
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "Working-Days"
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Accept_DayCalculationType_With_Whitespace()
    {
        // Arrange — FluentValidation trims before comparison
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "  total-month-days  "
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Ignore_ManualDays_When_Type_Is_Not_ManualDays()
    {
        // Arrange — ManualDays should be ignored when type is working-days
        var input = new DayCalculationRequestDto
        {
            DayCalculationType = "working-days",
            ManualDays = 22 // Should be ignored
        };

        // Act
        var result = await _dayCalculationSettingAppService.SaveAsync(input);

        // Assert — should succeed; ManualDays is silently ignored by domain
        result.Success.ShouldBeTrue();
    }

    #endregion
}
