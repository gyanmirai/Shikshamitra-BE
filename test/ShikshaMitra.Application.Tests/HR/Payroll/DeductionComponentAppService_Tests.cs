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
    public async Task Should_Create_Statutory_Deduction_Component()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Test Provident Fund",
            Description = "Employee PF contribution",
            ComponentType = DeductionComponentType.Statutory,
            DependOn = ComponentDependency.Basic,
            Amount = "12%",
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
        result.ComponentType.ShouldBe(DeductionComponentType.Statutory);
        result.DependOn.ShouldBe(ComponentDependency.Basic);
        result.Amount.ShouldBe(input.Amount);
        result.VisibleInPayslip.ShouldBeTrue();
        result.DisplayOrder.ShouldBe(1);
        result.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Create_Voluntary_Deduction_Component()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Test Life Insurance Premium",
            Description = "Monthly insurance premium",
            ComponentType = DeductionComponentType.Voluntary,
            DependOn = ComponentDependency.None,
            Amount = "500",
            VisibleInPayslip = true,
            DisplayOrder = 5
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.ComponentType.ShouldBe(DeductionComponentType.Voluntary);
        result.DependOn.ShouldBe(ComponentDependency.None);
        result.Amount.ShouldBe("500");
    }

    [Fact]
    public async Task Should_Create_Loan_Deduction_Component()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Test Home Loan EMI",
            Description = "Monthly home loan deduction",
            ComponentType = DeductionComponentType.Loan,
            DependOn = ComponentDependency.None,
            Amount = "15000",
            VisibleInPayslip = true,
            DisplayOrder = 3
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.ComponentType.ShouldBe(DeductionComponentType.Loan);
        result.Amount.ShouldBe("15000");
    }

    [Fact]
    public async Task Should_Not_Create_Duplicate_Name()
    {
        // Arrange
        var componentName = "Duplicate Deduction Test";
        var input1 = new CreateDeductionComponentDto
        {
            Name = componentName,
            ComponentType = DeductionComponentType.Statutory,
            DependOn = ComponentDependency.None,
            Amount = "1000"
        };

        await _deductionComponentAppService.CreateAsync(input1);

        var input2 = new CreateDeductionComponentDto
        {
            Name = componentName,
            ComponentType = DeductionComponentType.Voluntary,
            DependOn = ComponentDependency.None,
            Amount = "500"
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
            ComponentType = DeductionComponentType.Statutory,
            DependOn = ComponentDependency.None,
            Amount = "1000"
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
            ComponentType = DeductionComponentType.Statutory,
            DependOn = ComponentDependency.None,
            Amount = "1000"
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
        var result = await _deductionComponentAppService.GetAllActiveAsync();

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
        await CreateTestDeductionComponentAsync("Statutory 1", DeductionComponentType.Statutory);
        await CreateTestDeductionComponentAsync("Statutory 2", DeductionComponentType.Statutory);
        await CreateTestDeductionComponentAsync("Voluntary 1", DeductionComponentType.Voluntary);
        await CreateTestDeductionComponentAsync("Loan 1", DeductionComponentType.Loan);

        // Act
        var allActive = await _deductionComponentAppService.GetAllActiveAsync();

        // Assert
        var statutory = allActive.Items.Where(x => x.ComponentType == DeductionComponentType.Statutory).ToList();
        var voluntary = allActive.Items.Where(x => x.ComponentType == DeductionComponentType.Voluntary).ToList();
        var loan = allActive.Items.Where(x => x.ComponentType == DeductionComponentType.Loan).ToList();

        statutory.Count.ShouldBeGreaterThanOrEqualTo(2);
        voluntary.Count.ShouldBeGreaterThanOrEqualTo(1);
        loan.Count.ShouldBeGreaterThanOrEqualTo(1);
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
            ComponentType = DeductionComponentType.Voluntary,
            DependOn = ComponentDependency.Gross,
            Amount = "5%",
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
        result.ComponentType.ShouldBe(DeductionComponentType.Voluntary);
        result.DependOn.ShouldBe(ComponentDependency.Gross);
        result.Amount.ShouldBe("5%");
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
            ComponentType = DeductionComponentType.Statutory,
            DependOn = ComponentDependency.None,
            Amount = "1000"
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
            DependOn = created.DependOn,
            Amount = created.Amount
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
        var activeList = await _deductionComponentAppService.GetAllActiveAsync();

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
            ComponentType = DeductionComponentType.Statutory,
            DependOn = ComponentDependency.None,
            Amount = "1000"
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
            ComponentType = DeductionComponentType.Statutory,
            DependOn = ComponentDependency.Basic,
            Amount = "12.5%"
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.Amount.ShouldBe("12.5%");
    }

    [Fact]
    public async Task Should_Handle_Large_Amount()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Large Deduction",
            ComponentType = DeductionComponentType.Loan,
            DependOn = ComponentDependency.None,
            Amount = "500000"
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.Amount.ShouldBe("500000");
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
        DeductionComponentType type = DeductionComponentType.Statutory,
        ComponentDependency dependOn = ComponentDependency.None,
        int displayOrder = 0)
    {
        var input = new CreateDeductionComponentDto
        {
            Name = name,
            Description = $"Test description for {name}",
            ComponentType = type,
            DependOn = dependOn,
            Amount = "1000",
            VisibleInPayslip = true,
            DisplayOrder = displayOrder
        };

        return await _deductionComponentAppService.CreateAsync(input);
    }

    #endregion
}
