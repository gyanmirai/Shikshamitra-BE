using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.EventTypes;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.HR.Calendar;

/// <summary>
/// End-to-End tests for Event Type App Service
/// </summary>
public class EventTypeAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly IEventTypeAppService _eventTypeAppService;

    public EventTypeAppService_Tests()
    {
        _eventTypeAppService = GetRequiredService<IEventTypeAppService>();
    }

    #region Create Tests

    [Fact]
    public async Task Should_Create_Event_Type()
    {
        // Arrange
        var input = new CreateUpdateEventTypeDto
        {
            Name = "Meeting",
            Color = "#FF5733",
            Description = "Internal team meetings"
        };

        // Act
        var result = await _eventTypeAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        result.Name.ShouldBe("Meeting");
        result.Color.ShouldBe("#FF5733");
        result.Description.ShouldBe("Internal team meetings");
    }

    [Fact]
    public async Task Should_Create_Event_Type_Without_Description()
    {
        // Arrange
        var input = new CreateUpdateEventTypeDto
        {
            Name = "Conference",
            Color = "#00FF00",
            Description = null
        };

        // Act
        var result = await _eventTypeAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Conference");
        result.Description.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Reject_Null_Name()
    {
        // Arrange
        var input = new CreateUpdateEventTypeDto
        {
            Name = null!,
            Color = "#FF0000"
        };

        // Act & Assert — domain constructor throws ArgumentNullException for null name
        await Should.ThrowAsync<ArgumentNullException>(async () =>
        {
            await _eventTypeAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_Null_Color()
    {
        // Arrange
        var input = new CreateUpdateEventTypeDto
        {
            Name = "Test Event",
            Color = null!
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _eventTypeAppService.CreateAsync(input);
        });
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task Should_Get_Event_Type_By_Id()
    {
        // Arrange
        var created = await CreateTestEventTypeAsync("Get By ID Event Type");

        // Act
        var result = await _eventTypeAppService.GetAsync(created.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.Name.ShouldBe("Get By ID Event Type");
    }

    [Fact]
    public async Task Should_Throw_When_Event_Type_Not_Found()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _eventTypeAppService.GetAsync(nonExistentId);
        });
    }

    [Fact]
    public async Task Should_Get_Paginated_List()
    {
        // Arrange
        for (int i = 1; i <= 15; i++)
        {
            await CreateTestEventTypeAsync($"Paginated Event Type {i}");
        }

        var input = new PagedAndSortedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 10
        };

        // Act
        var result = await _eventTypeAppService.GetListAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeLessThanOrEqualTo(10);
        result.TotalCount.ShouldBeGreaterThanOrEqualTo(15);
    }

    [Fact]
    public async Task Should_Get_All_Event_Types()
    {
        // Arrange
        await CreateTestEventTypeAsync("All Types 1");
        await CreateTestEventTypeAsync("All Types 2");
        await CreateTestEventTypeAsync("All Types 3");

        // Act
        var result = await _eventTypeAppService.GetAllEventTypesAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Should_Search_Event_Types_By_Name()
    {
        // Arrange
        await CreateTestEventTypeAsync("SearchableHoliday");
        await CreateTestEventTypeAsync("Regular Meeting");

        var input = new GetEventTypeListRequestDto
        {
            Search = "Searchable",
            SkipCount = 0,
            MaxResultCount = 50
        };

        // Act
        var result = await _eventTypeAppService.GetListByNameAsync(input);

        // Assert
        result.Items.ShouldContain(x => x.Name.Contains("Searchable"));
        result.Items.ShouldNotContain(x => x.Name == "Regular Meeting");
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Should_Update_Event_Type()
    {
        // Arrange
        var created = await CreateTestEventTypeAsync("Original Event Type");

        var updateInput = new CreateUpdateEventTypeDto
        {
            Name = "Updated Event Type",
            Color = "#00FF00",
            Description = "Updated description"
        };

        // Act
        var result = await _eventTypeAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Updated Event Type");
        result.Color.ShouldBe("#00FF00");
        result.Description.ShouldBe("Updated description");
    }

    [Fact]
    public async Task Should_Reject_Empty_Name_On_Update()
    {
        // Arrange
        var created = await CreateTestEventTypeAsync("Preserved Name");

        var updateInput = new CreateUpdateEventTypeDto
        {
            Name = "",
            Color = "#0000FF",
            Description = "New description"
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _eventTypeAppService.UpdateAsync(created.Id, updateInput);
        });
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Should_Delete_Event_Type()
    {
        // Arrange
        var created = await CreateTestEventTypeAsync("Event Type to Delete");

        // Act
        await _eventTypeAppService.DeleteAsync(created.Id);

        // Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _eventTypeAppService.GetAsync(created.Id);
        });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Should_Handle_Special_Characters_In_Name()
    {
        // Arrange
        var input = new CreateUpdateEventTypeDto
        {
            Name = "Event & Holiday (2024) - Special",
            Color = "#FF0000"
        };

        // Act
        var result = await _eventTypeAppService.CreateAsync(input);

        // Assert
        result.Name.ShouldBe(input.Name);
    }

    [Fact]
    public async Task Should_Accept_Short_Hex_Color()
    {
        // Arrange — HexColorAttribute accepts #RGB format
        var input = new CreateUpdateEventTypeDto
        {
            Name = "Short Color Event",
            Color = "#F00"
        };

        // Act
        var result = await _eventTypeAppService.CreateAsync(input);

        // Assert
        result.Color.ShouldBe("#F00");
    }

    [Fact]
    public async Task Should_Accept_Lowercase_Hex_Color()
    {
        // Arrange
        var input = new CreateUpdateEventTypeDto
        {
            Name = "Lowercase Color Event",
            Color = "#ff5733"
        };

        // Act
        var result = await _eventTypeAppService.CreateAsync(input);

        // Assert
        result.Color.ShouldBe("#ff5733");
    }

    #endregion

    #region Helper Methods

    private async Task<EventTypeDto> CreateTestEventTypeAsync(
        string name,
        string color = "#FF5733",
        string? description = null)
    {
        var input = new CreateUpdateEventTypeDto
        {
            Name = name,
            Color = color,
            Description = description ?? $"Test description for {name}"
        };

        return await _eventTypeAppService.CreateAsync(input);
    }

    #endregion
}
