using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Calendar;

/// <summary>
/// End-to-End tests for Holiday Type App Service
/// </summary>
public class HolidayTypeAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly IHolidayTypeAppService _holidayTypeAppService;

    public HolidayTypeAppService_Tests()
    {
        _holidayTypeAppService = GetRequiredService<IHolidayTypeAppService>();
    }

    #region Create Tests

    [Fact]
    public async Task Should_Create_Holiday_Type()
    {
        // Arrange
        var input = new CreateUpdateHolidayTypeDto
        {
            Name = "National Holiday",
            Color = "#FF0000",
            Description = "Government declared national holidays"
        };

        // Act
        var result = await _holidayTypeAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        result.Name.ShouldBe("National Holiday");
        result.Color.ShouldBe("#FF0000");
        result.Description.ShouldBe("Government declared national holidays");
    }

    [Fact]
    public async Task Should_Create_Holiday_Type_Without_Description()
    {
        // Arrange
        var input = new CreateUpdateHolidayTypeDto
        {
            Name = "Festival",
            Color = "#00FF00",
            Description = null
        };

        // Act
        var result = await _holidayTypeAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Festival");
        result.Description.ShouldBeNull();
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task Should_Get_Holiday_Type_By_Id()
    {
        // Arrange
        var created = await CreateTestHolidayTypeAsync("Get By ID Holiday");

        // Act
        var result = await _holidayTypeAppService.GetAsync(created.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.Name.ShouldBe("Get By ID Holiday");
    }

    [Fact]
    public async Task Should_Throw_When_Holiday_Type_Not_Found()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _holidayTypeAppService.GetAsync(nonExistentId);
        });
    }

    [Fact]
    public async Task Should_Get_Paginated_List()
    {
        // Arrange
        for (int i = 1; i <= 15; i++)
        {
            await CreateTestHolidayTypeAsync($"Paginated Holiday Type {i}");
        }

        var input = new GetHolidayTypeListRequestDto
        {
            PageNumber = 1,
            PageSize = 10
        };

        // Act
        var result = await _holidayTypeAppService.GetListAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeLessThanOrEqualTo(10);
        result.TotalCount.ShouldBeGreaterThanOrEqualTo(15);
    }

    [Fact]
    public async Task Should_Filter_By_Keyword()
    {
        // Arrange
        await CreateTestHolidayTypeAsync("Independence Day");
        await CreateTestHolidayTypeAsync("Republic Day");
        await CreateTestHolidayTypeAsync("Christmas Holiday");

        var input = new GetHolidayTypeListRequestDto
        {
            Keyword = "Day",
            PageNumber = 1,
            PageSize = 50
        };

        // Act
        var result = await _holidayTypeAppService.GetListAsync(input);

        // Assert
        result.Items.ShouldAllBe(x => x.Name.Contains("Day"));
        result.Items.ShouldNotContain(x => x.Name == "Christmas Holiday");
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Should_Update_Holiday_Type()
    {
        // Arrange
        var created = await CreateTestHolidayTypeAsync("Original Holiday");

        var updateInput = new CreateUpdateHolidayTypeDto
        {
            Name = "Updated Holiday",
            Color = "#0000FF",
            Description = "Updated description"
        };

        // Act
        var result = await _holidayTypeAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Updated Holiday");
        result.Color.ShouldBe("#0000FF");
        result.Description.ShouldBe("Updated description");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Should_Delete_Holiday_Type()
    {
        // Arrange
        var created = await CreateTestHolidayTypeAsync("Holiday to Delete");

        // Act
        await _holidayTypeAppService.DeleteAsync(created.Id);

        // Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _holidayTypeAppService.GetAsync(created.Id);
        });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Should_Handle_Special_Characters_In_Name()
    {
        // Arrange
        var input = new CreateUpdateHolidayTypeDto
        {
            Name = "Holiday & Festival (Special) - 2024",
            Color = "#FF0000"
        };

        // Act
        var result = await _holidayTypeAppService.CreateAsync(input);

        // Assert
        result.Name.ShouldBe(input.Name);
    }

    [Fact]
    public async Task Should_Return_Empty_For_Non_Matching_Keyword()
    {
        // Arrange
        await CreateTestHolidayTypeAsync("Test Holiday");

        var input = new GetHolidayTypeListRequestDto
        {
            Keyword = "NonExistentKeyword12345",
            PageNumber = 1,
            PageSize = 50
        };

        // Act
        var result = await _holidayTypeAppService.GetListAsync(input);

        // Assert
        result.Items.Count.ShouldBe(0);
    }

    #endregion

    #region Helper Methods

    private async Task<HolidayTypeDto> CreateTestHolidayTypeAsync(
        string name,
        string color = "#FF5733",
        string? description = null)
    {
        var input = new CreateUpdateHolidayTypeDto
        {
            Name = name,
            Color = color,
            Description = description ?? $"Test description for {name}"
        };

        return await _holidayTypeAppService.CreateAsync(input);
    }

    #endregion
}
