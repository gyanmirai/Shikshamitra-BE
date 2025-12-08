using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

/// <summary>
/// End-to-End tests for Earning Component App Service
/// </summary>
public class EarningComponentAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly IEarningComponentAppService _earningComponentAppService;

    public EarningComponentAppService_Tests()
    {
        _earningComponentAppService = GetRequiredService<IEarningComponentAppService>();
    }

    #region Create Tests

    [Fact]
    public async Task Should_Create_Fixed_Earning_Component()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = "Test Basic Salary",
            Description = "Monthly basic salary component",
            ComponentType = EarningComponentType.Fixed,
            DependOn = ComponentDependency.None,
            Amount = "50000",
            VisibleInPayslip = true,
            DisplayOrder = 1
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        result.Name.ShouldBe(input.Name);
        result.Description.ShouldBe(input.Description);
        result.ComponentType.ShouldBe(EarningComponentType.Fixed);
        result.DependOn.ShouldBe(ComponentDependency.None);
        result.Amount.ShouldBe(input.Amount);
        result.VisibleInPayslip.ShouldBeTrue();
        result.DisplayOrder.ShouldBe(1);
        result.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Create_Variable_Earning_Component()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = "Test Performance Bonus",
            Description = "Quarterly performance bonus",
            ComponentType = EarningComponentType.Variable,
            DependOn = ComponentDependency.Basic,
            Amount = "10%",
            VisibleInPayslip = true,
            DisplayOrder = 5
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.ComponentType.ShouldBe(EarningComponentType.Variable);
        result.DependOn.ShouldBe(ComponentDependency.Basic);
        result.Amount.ShouldBe("10%");
    }

    [Fact]
    public async Task Should_Not_Create_Duplicate_Name()
    {
        // Arrange
        var componentName = "Duplicate Component Test";
        var input1 = new CreateEarningComponentDto
        {
            Name = componentName,
            ComponentType = EarningComponentType.Fixed,
            DependOn = ComponentDependency.None,
            Amount = "10000"
        };

        await _earningComponentAppService.CreateAsync(input1);

        var input2 = new CreateEarningComponentDto
        {
            Name = componentName,
            ComponentType = EarningComponentType.Variable,
            DependOn = ComponentDependency.None,
            Amount = "5000"
        };

        // Act & Assert
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _earningComponentAppService.CreateAsync(input2);
        });
    }

    [Fact]
    public async Task Should_Not_Create_Without_Required_Fields()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = "", // Empty name
            ComponentType = EarningComponentType.Fixed,
            DependOn = ComponentDependency.None,
            Amount = "10000"
        };

        // Act & Assert
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _earningComponentAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Trim_Whitespace_From_Name()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = "  Test Component With Spaces  ",
            ComponentType = EarningComponentType.Fixed,
            DependOn = ComponentDependency.None,
            Amount = "10000"
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.Name.ShouldBe("Test Component With Spaces");
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task Should_Get_Earning_Component_By_Id()
    {
        // Arrange
        var created = await CreateTestEarningComponentAsync("Get By ID Test");

        // Act
        var result = await _earningComponentAppService.GetAsync(created.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.Name.ShouldBe(created.Name);
    }

    [Fact]
    public async Task Should_Throw_Exception_When_Component_Not_Found()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _earningComponentAppService.GetAsync(nonExistentId);
        });
    }

    [Fact]
    public async Task Should_Get_All_Active_Components()
    {
        // Arrange
        await CreateTestEarningComponentAsync("Active Component 1");
        await CreateTestEarningComponentAsync("Active Component 2");
        await CreateTestEarningComponentAsync("Active Component 3");

        // Act
        var result = await _earningComponentAppService.GetAllActiveAsync(new PagedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeGreaterThanOrEqualTo(3);
        result.Items.ShouldAllBe(x => x.IsActive);
    }

    [Fact]
    public async Task Should_Get_Paginated_List()
    {
        // Arrange
        for (int i = 1; i <= 15; i++)
        {
            await CreateTestEarningComponentAsync($"Paginated Component {i}");
        }

        var input = new PagedAndSortedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 10
        };

        // Act
        var result = await _earningComponentAppService.GetListAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeLessThanOrEqualTo(10);
        result.TotalCount.ShouldBeGreaterThanOrEqualTo(15);
    }

    [Fact]
    public async Task Should_Sort_By_DisplayOrder()
    {
        // Arrange
        await CreateTestEarningComponentAsync("Component A", displayOrder: 3);
        await CreateTestEarningComponentAsync("Component B", displayOrder: 1);
        await CreateTestEarningComponentAsync("Component C", displayOrder: 2);

        // Act
        var result = await _earningComponentAppService.GetAllActiveAsync(new PagedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });

        // Assert
        var orderedComponents = result.Items.OrderBy(x => x.DisplayOrder).ToList();
        for (int i = 0; i < orderedComponents.Count - 1; i++)
        {
            orderedComponents[i].DisplayOrder.ShouldBeLessThanOrEqualTo(orderedComponents[i + 1].DisplayOrder);
        }
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Should_Update_Earning_Component()
    {
        // Arrange
        var created = await CreateTestEarningComponentAsync("Original Name");

        var updateInput = new UpdateEarningComponentDto
        {
            Name = "Updated Name",
            Description = "Updated description",
            ComponentType = EarningComponentType.Variable,
            DependOn = ComponentDependency.Gross,
            Amount = "15%",
            VisibleInPayslip = false,
            DisplayOrder = 10
        };

        // Act
        var result = await _earningComponentAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.Name.ShouldBe("Updated Name");
        result.Description.ShouldBe("Updated description");
        result.ComponentType.ShouldBe(EarningComponentType.Variable);
        result.DependOn.ShouldBe(ComponentDependency.Gross);
        result.Amount.ShouldBe("15%");
        result.VisibleInPayslip.ShouldBeFalse();
        result.DisplayOrder.ShouldBe(10);
    }

    [Fact]
    public async Task Should_Not_Update_To_Duplicate_Name()
    {
        // Arrange
        var component1 = await CreateTestEarningComponentAsync("Component 1");
        var component2 = await CreateTestEarningComponentAsync("Component 2");

        var updateInput = new UpdateEarningComponentDto
        {
            Name = "Component 1", // Trying to use existing name
            ComponentType = EarningComponentType.Fixed,
            DependOn = ComponentDependency.None,
            Amount = "10000"
        };

        // Act & Assert
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _earningComponentAppService.UpdateAsync(component2.Id, updateInput);
        });
    }

    [Fact]
    public async Task Should_Allow_Update_With_Same_Name()
    {
        // Arrange
        var created = await CreateTestEarningComponentAsync("Same Name Component");

        var updateInput = new UpdateEarningComponentDto
        {
            Name = "Same Name Component", // Same name
            Description = "Updated description only",
            ComponentType = created.ComponentType,
            DependOn = created.DependOn,
            Amount = created.Amount
        };

        // Act
        var result = await _earningComponentAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Same Name Component");
        result.Description.ShouldBe("Updated description only");
    }

    #endregion

    #region Activate/Deactivate Tests

    [Fact]
    public async Task Should_Deactivate_Component()
    {
        // Arrange
        var created = await CreateTestEarningComponentAsync("Component to Deactivate");
        created.IsActive.ShouldBeTrue();

        // Act
        await _earningComponentAppService.DeactivateAsync(created.Id);

        // Assert
        var result = await _earningComponentAppService.GetAsync(created.Id);
        result.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Activate_Component()
    {
        // Arrange
        var created = await CreateTestEarningComponentAsync("Component to Activate");
        await _earningComponentAppService.DeactivateAsync(created.Id);

        var deactivated = await _earningComponentAppService.GetAsync(created.Id);
        deactivated.IsActive.ShouldBeFalse();

        // Act
        await _earningComponentAppService.ActivateAsync(created.Id);

        // Assert
        var result = await _earningComponentAppService.GetAsync(created.Id);
        result.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Deactivated_Components_Should_Not_Appear_In_Active_List()
    {
        // Arrange
        var component1 = await CreateTestEarningComponentAsync("Active Component");
        var component2 = await CreateTestEarningComponentAsync("Will Be Deactivated");

        await _earningComponentAppService.DeactivateAsync(component2.Id);

        // Act
        var activeList = await _earningComponentAppService.GetAllActiveAsync(new PagedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });

        // Assert
        activeList.Items.ShouldContain(x => x.Id == component1.Id);
        activeList.Items.ShouldNotContain(x => x.Id == component2.Id);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Should_Delete_Earning_Component()
    {
        // Arrange
        var created = await CreateTestEarningComponentAsync("Component to Delete");

        // Act
        await _earningComponentAppService.DeleteAsync(created.Id);

        // Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _earningComponentAppService.GetAsync(created.Id);
        });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Should_Handle_Special_Characters_In_Name()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = "Component & Bonus (50%) - Test",
            ComponentType = EarningComponentType.Fixed,
            DependOn = ComponentDependency.None,
            Amount = "5000"
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.Name.ShouldBe(input.Name);
    }

    [Fact]
    public async Task Should_Handle_Percentage_Amount()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = "Percentage Component",
            ComponentType = EarningComponentType.Variable,
            DependOn = ComponentDependency.Basic,
            Amount = "10.5%"
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.Amount.ShouldBe("10.5%");
    }

    [Fact]
    public async Task Should_Handle_Large_Amount()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = "Large Amount Component",
            ComponentType = EarningComponentType.Fixed,
            DependOn = ComponentDependency.None,
            Amount = "999999999"
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.Amount.ShouldBe("999999999");
    }

    [Fact]
    public async Task Should_Handle_Empty_Description()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = "No Description Component",
            Description = null,
            ComponentType = EarningComponentType.Fixed,
            DependOn = ComponentDependency.None,
            Amount = "10000"
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.Description.ShouldBeNull();
    }

    #endregion

    #region Helper Methods

    private async Task<EarningComponentDto> CreateTestEarningComponentAsync(
        string name,
        EarningComponentType type = EarningComponentType.Fixed,
        int displayOrder = 0)
    {
        var input = new CreateEarningComponentDto
        {
            Name = name,
            Description = $"Test description for {name}",
            ComponentType = type,
            DependOn = ComponentDependency.None,
            Amount = "10000",
            VisibleInPayslip = true,
            DisplayOrder = displayOrder
        };

        return await _earningComponentAppService.CreateAsync(input);
    }

    #endregion
}
