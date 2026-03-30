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
/// End-to-End tests for Deduction Component App Service
/// </summary>
public class DeductionComponentAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly IDeductionComponentAppService _deductionComponentAppService;

    public DeductionComponentAppService_Tests()
    {
        _deductionComponentAppService = GetRequiredService<IDeductionComponentAppService>();
    }

    #region Create Tests

    [Fact]
    public async Task Should_Create_Fixed_Deduction_Component()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Test Provident Fund",
            Description = "Employee PF contribution",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            Percentage = 12m,
            SelectedDeductionComponentId = Guid.NewGuid(),
            DependOn = ComponentDependency.Basic,
            VisibleInPayslip = true,
            DisplayOrder = 1
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        result.Name.ShouldBe(input.Name);
        result.Description.ShouldBe(input.Description);
        result.ComponentType.ShouldBe(DeductionComponentType.Fixed);
        result.CalculationType.ShouldBe(CalculationType.Percentage);
        result.Percentage.ShouldBe(12m);
        result.Amount.ShouldBeNull();
        result.DependOn.ShouldBe(ComponentDependency.Basic);
        result.VisibleInPayslip.ShouldBeTrue();
        result.DisplayOrder.ShouldBe(1);
        result.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Create_Variable_Deduction_Component()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Test Life Insurance Premium",
            Description = "Monthly insurance premium",
            ComponentType = DeductionComponentType.Variable,
            CalculationType = CalculationType.Amount,
            Amount = 500m,
            DependOn = ComponentDependency.None,
            VisibleInPayslip = true,
            DisplayOrder = 5
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.ComponentType.ShouldBe(DeductionComponentType.Variable);
        result.DependOn.ShouldBe(ComponentDependency.None);
        result.Amount.ShouldBe(500m);
    }

    [Fact]
    public async Task Should_Not_Create_Duplicate_Name()
    {
        // Arrange
        var componentName = "Duplicate Deduction Test";
        var input1 = new CreateDeductionComponentDto
        {
            Name = componentName,
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            Amount = 1000m,
            DependOn = ComponentDependency.None
        };

        await _deductionComponentAppService.CreateAsync(input1);

        var input2 = new CreateDeductionComponentDto
        {
            Name = componentName,
            ComponentType = DeductionComponentType.Variable,
            CalculationType = CalculationType.Amount,
            Amount = 500m,
            DependOn = ComponentDependency.None
        };

        // Act & Assert
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _deductionComponentAppService.CreateAsync(input2);
        });
    }

    [Fact]
    public async Task Should_Not_Create_Without_Required_Fields()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "", // Empty name
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            Amount = 1000m,
            DependOn = ComponentDependency.None
        };

        // Act & Assert
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _deductionComponentAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Trim_Whitespace_From_Name()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "  Test Deduction With Spaces  ",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            Amount = 1000m,
            DependOn = ComponentDependency.None
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.Name.ShouldBe("Test Deduction With Spaces");
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task Should_Get_Deduction_Component_By_Id()
    {
        // Arrange
        var created = await CreateTestDeductionComponentAsync("Get By ID Test");

        // Act
        var result = await _deductionComponentAppService.GetAsync(created.Id);

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
            await _deductionComponentAppService.GetAsync(nonExistentId);
        });
    }

    [Fact]
    public async Task Should_Get_All_Active_Components()
    {
        // Arrange
        await CreateTestDeductionComponentAsync("Active Deduction 1");
        await CreateTestDeductionComponentAsync("Active Deduction 2");
        await CreateTestDeductionComponentAsync("Active Deduction 3");

        // Act
        var result = await _deductionComponentAppService.GetAllActiveAsync(new PagedResultRequestDto
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
            await CreateTestDeductionComponentAsync($"Paginated Deduction {i}");
        }

        var input = new PagedAndSortedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 10
        };

        // Act
        var result = await _deductionComponentAppService.GetListAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeLessThanOrEqualTo(10);
        result.TotalCount.ShouldBeGreaterThanOrEqualTo(15);
    }

    [Fact]
    public async Task Should_Filter_By_Component_Type()
    {
        // Arrange
        await CreateTestDeductionComponentAsync("Fixed 1", DeductionComponentType.Fixed);
        await CreateTestDeductionComponentAsync("Fixed 2", DeductionComponentType.Fixed);
        await CreateTestDeductionComponentAsync("Variable 1", DeductionComponentType.Variable);
        await CreateTestDeductionComponentAsync("Variable 2", DeductionComponentType.Variable);

        // Act
        var allActive = await _deductionComponentAppService.GetAllActiveAsync(new PagedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });

        // Assert
        var fixedComponents = allActive.Items.Where(x => x.ComponentType == DeductionComponentType.Fixed).ToList();
        var variable = allActive.Items.Where(x => x.ComponentType == DeductionComponentType.Variable).ToList();

        fixedComponents.Count.ShouldBeGreaterThanOrEqualTo(2);
        variable.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Should_Update_Deduction_Component()
    {
        // Arrange
        var created = await CreateTestDeductionComponentAsync("Original Deduction");

        var updateInput = new UpdateDeductionComponentDto
        {
            Name = "Updated Deduction",
            Description = "Updated description",
            ComponentType = DeductionComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            Percentage = 5m,
            SelectedDeductionComponentId = Guid.NewGuid(),
            DependOn = ComponentDependency.Gross,
            VisibleInPayslip = false,
            DisplayOrder = 10
        };

        // Act
        var result = await _deductionComponentAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.Name.ShouldBe("Updated Deduction");
        result.Description.ShouldBe("Updated description");
        result.ComponentType.ShouldBe(DeductionComponentType.Variable);
        result.DependOn.ShouldBe(ComponentDependency.Gross);
        result.CalculationType.ShouldBe(CalculationType.Percentage);
        result.Percentage.ShouldBe(5m);
        result.Amount.ShouldBeNull();
        result.VisibleInPayslip.ShouldBeFalse();
        result.DisplayOrder.ShouldBe(10);
    }

    [Fact]
    public async Task Should_Not_Update_To_Duplicate_Name()
    {
        // Arrange
        var component1 = await CreateTestDeductionComponentAsync("Deduction 1");
        var component2 = await CreateTestDeductionComponentAsync("Deduction 2");

        var updateInput = new UpdateDeductionComponentDto
        {
            Name = "Deduction 1", // Trying to use existing name
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            Amount = 1000m,
            DependOn = ComponentDependency.None
        };

        // Act & Assert
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _deductionComponentAppService.UpdateAsync(component2.Id, updateInput);
        });
    }

    [Fact]
    public async Task Should_Allow_Update_With_Same_Name()
    {
        // Arrange
        var created = await CreateTestDeductionComponentAsync("Same Name Deduction");

        var updateInput = new UpdateDeductionComponentDto
        {
            Name = "Same Name Deduction", // Same name
            Description = "Updated description only",
            ComponentType = created.ComponentType,
            CalculationType = created.CalculationType,
            Amount = created.Amount,
            Percentage = created.Percentage,
            SelectedDeductionComponentId = created.SelectedDeductionComponentId,
            DependOn = created.DependOn
        };

        // Act
        var result = await _deductionComponentAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Same Name Deduction");
        result.Description.ShouldBe("Updated description only");
    }

    #endregion

    #region Activate/Deactivate Tests

    [Fact]
    public async Task Should_Deactivate_Component()
    {
        // Arrange
        var created = await CreateTestDeductionComponentAsync("Deduction to Deactivate");
        created.IsActive.ShouldBeTrue();

        // Act
        await _deductionComponentAppService.DeactivateAsync(created.Id);

        // Assert
        var result = await _deductionComponentAppService.GetAsync(created.Id);
        result.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Activate_Component()
    {
        // Arrange
        var created = await CreateTestDeductionComponentAsync("Deduction to Activate");
        await _deductionComponentAppService.DeactivateAsync(created.Id);

        var deactivated = await _deductionComponentAppService.GetAsync(created.Id);
        deactivated.IsActive.ShouldBeFalse();

        // Act
        await _deductionComponentAppService.ActivateAsync(created.Id);

        // Assert
        var result = await _deductionComponentAppService.GetAsync(created.Id);
        result.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Deactivated_Components_Should_Not_Appear_In_Active_List()
    {
        // Arrange
        var component1 = await CreateTestDeductionComponentAsync("Active Deduction");
        var component2 = await CreateTestDeductionComponentAsync("Will Be Deactivated");

        await _deductionComponentAppService.DeactivateAsync(component2.Id);

        // Act
        var activeList = await _deductionComponentAppService.GetAllActiveAsync(new PagedResultRequestDto
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
    public async Task Should_Delete_Deduction_Component()
    {
        // Arrange
        var created = await CreateTestDeductionComponentAsync("Deduction to Delete");

        // Act
        await _deductionComponentAppService.DeleteAsync(created.Id);

        // Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _deductionComponentAppService.GetAsync(created.Id);
        });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Should_Handle_Special_Characters_In_Name()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Deduction & Tax (10%) - Test",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            Amount = 1000m,
            DependOn = ComponentDependency.None
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.Name.ShouldBe(input.Name);
    }

    [Fact]
    public async Task Should_Handle_Percentage_Amount()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Percentage Deduction",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            Percentage = 12.5m,
            SelectedDeductionComponentId = Guid.NewGuid(),
            DependOn = ComponentDependency.Basic
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.Percentage.ShouldBe(12.5m);
        result.CalculationType.ShouldBe(CalculationType.Percentage);
    }

    [Fact]
    public async Task Should_Handle_Large_Amount()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Large Deduction",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            Amount = 500000m,
            DependOn = ComponentDependency.None
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.Amount.ShouldBe(500000m);
    }

    [Fact]
    public async Task Should_Handle_All_Dependency_Types()
    {
        // Arrange & Act
        var noneDependent = await CreateTestDeductionComponentAsync("None Dependent", dependOn: ComponentDependency.None);
        var basicDependent = await CreateTestDeductionComponentAsync("Basic Dependent", dependOn: ComponentDependency.Basic);
        var grossDependent = await CreateTestDeductionComponentAsync("Gross Dependent", dependOn: ComponentDependency.Gross);

        // Assert
        noneDependent.DependOn.ShouldBe(ComponentDependency.None);
        basicDependent.DependOn.ShouldBe(ComponentDependency.Basic);
        grossDependent.DependOn.ShouldBe(ComponentDependency.Gross);
    }

    #endregion

    #region Helper Methods

    private async Task<DeductionComponentDto> CreateTestDeductionComponentAsync(
        string name,
        DeductionComponentType type = DeductionComponentType.Fixed,
        ComponentDependency dependOn = ComponentDependency.None,
        int displayOrder = 0)
    {
        var input = new CreateDeductionComponentDto
        {
            Name = name,
            Description = $"Test description for {name}",
            ComponentType = type,
            CalculationType = CalculationType.Amount,
            Amount = 1000m,
            DependOn = dependOn,
            VisibleInPayslip = true,
            DisplayOrder = displayOrder
        };

        return await _deductionComponentAppService.CreateAsync(input);
    }

    #endregion
}
