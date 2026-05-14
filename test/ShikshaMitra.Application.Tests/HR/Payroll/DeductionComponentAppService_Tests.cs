using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.TenantSettings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

/// <summary>
/// End-to-End tests for Deduction Component App Service
/// </summary>
public class DeductionComponentAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly IDeductionComponentAppService _deductionComponentAppService;
    private readonly IEmployeePersonalInfoAppService _employeePersonalInfoAppService;
    private readonly IEmployeePayrollComponentRepository _employeePayrollComponentRepository;
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;

    public DeductionComponentAppService_Tests()
    {
        _deductionComponentAppService = GetRequiredService<IDeductionComponentAppService>();
        _employeePersonalInfoAppService = GetRequiredService<IEmployeePersonalInfoAppService>();
        _employeePayrollComponentRepository = GetRequiredService<IEmployeePayrollComponentRepository>();
        _roleRepository = GetRequiredService<IIdentityRoleRepository>();
        _shiftRepository = GetRequiredService<IRepository<ShiftManagement, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
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

    [Fact]
    public async Task Should_Propagate_Update_To_Linked_Employee_Deduction_Components_Only()
    {
        var employee = await CreateEmployeeAsync("deduction-sync");
        var originalComponentName = $"Linked TDS {Guid.NewGuid():N}";
        var master = await CreateTestDeductionComponentAsync(originalComponentName);
        var unlinkedEmployeeRowId = Guid.NewGuid();

        await _employeePayrollComponentRepository.InsertAsync(new EmployeePayrollComponent(
            Guid.NewGuid(),
            employee.Id,
            master.Name,
            master.Id,
            master.Description,
            master.Amount ?? 0m,
            isFixed: true,
            isTemp: false,
            applicableMonth: null,
            componentType: SalaryComponentType.Fixed,
            dependOn: SalaryComponentDependency.None,
            isEarning: false), autoSave: true);

        await _employeePayrollComponentRepository.InsertAsync(new EmployeePayrollComponent(
            unlinkedEmployeeRowId,
            employee.Id,
            master.Name,
            componentId: null,
            "Custom employee deduction",
            999m,
            isFixed: true,
            isTemp: false,
            applicableMonth: null,
            componentType: SalaryComponentType.Fixed,
            dependOn: SalaryComponentDependency.None,
            isEarning: false), autoSave: true);

        await _deductionComponentAppService.UpdateAsync(master.Id, new UpdateDeductionComponentDto
        {
            Name = "Updated TDS",
            Description = "Updated from master",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 100m,
            VisibleInPayslip = true,
            DisplayOrder = 1
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var rows = await _employeePayrollComponentRepository.GetDeductionsByEmployeeIdAsync(employee.Id);
            var linked = rows.Single(x => x.ComponentId == master.Id);
            linked.Name.ShouldBe("Updated TDS");
            linked.Description.ShouldBe("Updated from master");
            linked.Amount.ShouldBe(100m);

            var unlinked = rows.Single(x => x.Id == unlinkedEmployeeRowId);
            unlinked.Name.ShouldBe(originalComponentName);
            unlinked.Description.ShouldBe("Custom employee deduction");
            unlinked.Amount.ShouldBe(999m);
        });
    }

    [Fact]
    public async Task Should_Propagate_Percentage_Update_To_Linked_Employee_Deduction_Components()
    {
        var employee = await CreateEmployeeAsync("deduction-percentage-sync");
        var baseComponent = await CreateTestDeductionComponentAsync($"Base Deduction For Percentage {Guid.NewGuid():N}");
        var percentageComponent = await _deductionComponentAppService.CreateAsync(new CreateDeductionComponentDto
        {
            Name = $"PF Percentage {Guid.NewGuid():N}",
            Description = "Percentage deduction",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 10m,
            SelectedDeductionComponentId = baseComponent.Id,
            VisibleInPayslip = true,
            DisplayOrder = 2
        });

        await _employeePayrollComponentRepository.InsertAsync(new EmployeePayrollComponent(
            Guid.NewGuid(),
            employee.Id,
            percentageComponent.Name,
            percentageComponent.Id,
            percentageComponent.Description,
            percentageComponent.Percentage ?? 0m,
            isFixed: false,
            isTemp: false,
            applicableMonth: null,
            componentType: SalaryComponentType.Percentage,
            dependOn: SalaryComponentDependency.Basic,
            isEarning: false), autoSave: true);

        await _deductionComponentAppService.UpdateAsync(percentageComponent.Id, new UpdateDeductionComponentDto
        {
            Name = "Updated PF Percentage",
            Description = "Updated percentage deduction",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 12m,
            SelectedDeductionComponentId = baseComponent.Id,
            VisibleInPayslip = true,
            DisplayOrder = 2
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var linked = (await _employeePayrollComponentRepository.GetDeductionsByEmployeeIdAsync(employee.Id))
                .Single(x => x.ComponentId == percentageComponent.Id);
            linked.Name.ShouldBe("Updated PF Percentage");
            linked.Amount.ShouldBe(12m);
            linked.IsFixed.ShouldBeFalse();
            linked.ComponentType.ShouldBe(SalaryComponentType.Percentage);
            linked.DependOn.ShouldBe(SalaryComponentDependency.Basic);
        });
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

    #region Tier 2 — Calculation Field Validation

    [Fact]
    public async Task Should_Reject_Negative_Amount()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Negative Amount Deduction",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = -100m
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _deductionComponentAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Accept_Zero_Amount()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Zero Amount Deduction",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 0m
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.Amount.ShouldBe(0m);
    }

    [Fact]
    public async Task Should_Reject_Null_Amount_In_Amount_Mode()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Null Amount Deduction",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = null
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _deductionComponentAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Not_Require_Amount_For_Fixed_Percentage_Mode()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Fixed Percent Without Amount Deduction",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Amount = null,
            Percentage = 12m,
            SelectedDeductionComponentId = Guid.NewGuid()
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.CalculationType.ShouldBe(CalculationType.Percentage);
        result.Amount.ShouldBeNull();
        result.Percentage.ShouldBe(12m);
    }

    [Fact]
    public async Task Should_Not_Require_Amount_For_Variable_Amount_Mode()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Variable Amount Without Default Deduction",
            ComponentType = DeductionComponentType.Variable,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = null
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.ComponentType.ShouldBe(DeductionComponentType.Variable);
        result.CalculationType.ShouldBe(CalculationType.Amount);
        result.Amount.ShouldBeNull();
        result.Percentage.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Not_Require_Amount_When_Updating_To_Variable_Amount_Mode()
    {
        // Arrange
        var created = await CreateTestDeductionComponentAsync("Update Variable Without Amount Deduction");

        var updateInput = new UpdateDeductionComponentDto
        {
            Name = "Update Variable Without Amount Deduction",
            ComponentType = DeductionComponentType.Variable,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = null,
            VisibleInPayslip = true
        };

        // Act
        var result = await _deductionComponentAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        result.ComponentType.ShouldBe(DeductionComponentType.Variable);
        result.CalculationType.ShouldBe(CalculationType.Amount);
        result.Amount.ShouldBeNull();
        result.Percentage.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Reject_Percentage_Over_100()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Over 100 Percent Deduction",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 101m,
            SelectedDeductionComponentId = Guid.NewGuid()
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _deductionComponentAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Accept_Percentage_At_Zero_Boundary()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Zero Percent Deduction",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 0m,
            SelectedDeductionComponentId = Guid.NewGuid()
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.Percentage.ShouldBe(0m);
    }

    [Fact]
    public async Task Should_Accept_Percentage_At_100_Boundary()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Hundred Percent Deduction",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 100m,
            SelectedDeductionComponentId = Guid.NewGuid()
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.Percentage.ShouldBe(100m);
    }

    [Fact]
    public async Task Should_Reject_Negative_Percentage()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Negative Percent Deduction",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = -5m,
            SelectedDeductionComponentId = Guid.NewGuid()
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _deductionComponentAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_Percentage_Without_SelectedComponentId()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "No Selected Component Deduction",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 10m,
            SelectedDeductionComponentId = null
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _deductionComponentAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_Null_Percentage_In_Percentage_Mode()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Null Percentage Deduction",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = null,
            SelectedDeductionComponentId = Guid.NewGuid()
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _deductionComponentAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Clear_Amount_When_Switching_To_Percentage_On_Update()
    {
        // Arrange — create with Amount mode
        var created = await CreateTestDeductionComponentAsync("Switch To Pct Ded");
        created.Amount.ShouldBe(1000m);

        var updateInput = new UpdateDeductionComponentDto
        {
            Name = "Switch To Pct Ded",
            ComponentType = DeductionComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 12m,
            SelectedDeductionComponentId = Guid.NewGuid()
        };

        // Act
        var result = await _deductionComponentAppService.UpdateAsync(created.Id, updateInput);

        // Assert — Amount should be cleared, Percentage should be set
        result.CalculationType.ShouldBe(CalculationType.Percentage);
        result.Percentage.ShouldBe(12m);
        result.Amount.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Clear_Percentage_When_Switching_To_Amount_On_Update()
    {
        // Arrange — create with Percentage mode
        var createInput = new CreateDeductionComponentDto
        {
            Name = "Switch To Amount Ded",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 8m,
            SelectedDeductionComponentId = Guid.NewGuid()
        };
        var created = await _deductionComponentAppService.CreateAsync(createInput);
        created.Percentage.ShouldBe(8m);

        var updateInput = new UpdateDeductionComponentDto
        {
            Name = "Switch To Amount Ded",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 5000m
        };

        // Act
        var result = await _deductionComponentAppService.UpdateAsync(created.Id, updateInput);

        // Assert — Percentage should be cleared, Amount should be set
        result.CalculationType.ShouldBe(CalculationType.Amount);
        result.Amount.ShouldBe(5000m);
        result.Percentage.ShouldBeNull();
        result.SelectedDeductionComponentId.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Create_With_CustomComponent_Dependency()
    {
        // Arrange
        var input = new CreateDeductionComponentDto
        {
            Name = "Custom Dependent Deduction",
            ComponentType = DeductionComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.CustomComponent,
            Percentage = 3m,
            SelectedDeductionComponentId = Guid.NewGuid()
        };

        // Act
        var result = await _deductionComponentAppService.CreateAsync(input);

        // Assert
        result.DependOn.ShouldBe(ComponentDependency.CustomComponent);
    }

    [Fact]
    public async Task Should_Toggle_VisibleInPayslip()
    {
        // Arrange — create with VisibleInPayslip = true
        var created = await CreateTestDeductionComponentAsync("Payslip Toggle Ded");
        created.VisibleInPayslip.ShouldBeTrue();

        var updateInput = new UpdateDeductionComponentDto
        {
            Name = "Payslip Toggle Ded",
            ComponentType = created.ComponentType,
            CalculationType = created.CalculationType,
            DependOn = created.DependOn,
            Amount = created.Amount,
            VisibleInPayslip = false
        };

        // Act
        var result = await _deductionComponentAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        result.VisibleInPayslip.ShouldBeFalse();
    }

    #endregion

    #region Tier 4 — Activate/Deactivate Lifecycle

    [Fact]
    public async Task Should_Deactivate_And_Exclude_From_Active_List()
    {
        // Arrange
        var component = await CreateTestDeductionComponentAsync("Deactivate Active List Ded");
        component.IsActive.ShouldBeTrue();

        // Act — deactivate
        await _deductionComponentAppService.DeactivateAsync(component.Id);

        // Assert — should not appear in active list
        var activeList = await _deductionComponentAppService.GetAllActiveAsync(new PagedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });
        activeList.Items.ShouldNotContain(x => x.Id == component.Id);
    }

    [Fact]
    public async Task Should_Reactivate_And_Include_In_Active_List()
    {
        // Arrange — create and deactivate
        var component = await CreateTestDeductionComponentAsync("Reactivate Lifecycle Ded");
        await _deductionComponentAppService.DeactivateAsync(component.Id);

        // Verify excluded
        var beforeReactivation = await _deductionComponentAppService.GetAllActiveAsync(new PagedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });
        beforeReactivation.Items.ShouldNotContain(x => x.Id == component.Id);

        // Act — reactivate
        await _deductionComponentAppService.ActivateAsync(component.Id);

        // Assert — should appear in active list again
        var afterReactivation = await _deductionComponentAppService.GetAllActiveAsync(new PagedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });
        afterReactivation.Items.ShouldContain(x => x.Id == component.Id);
    }

    [Fact]
    public async Task Should_Retain_Data_After_Deactivate_Reactivate_Cycle()
    {
        // Arrange
        var component = await CreateTestDeductionComponentAsync("Data Retention Ded");

        // Act — deactivate then reactivate
        await _deductionComponentAppService.DeactivateAsync(component.Id);
        await _deductionComponentAppService.ActivateAsync(component.Id);

        // Assert — all original data should be preserved
        var retrieved = await _deductionComponentAppService.GetAsync(component.Id);
        retrieved.Name.ShouldBe("Data Retention Ded");
        retrieved.Amount.ShouldBe(1000m);
        retrieved.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Handle_Full_Deduction_Component_Lifecycle()
    {
        // Step 1: Create
        var created = await CreateTestDeductionComponentAsync("Full Lifecycle Deduction", DeductionComponentType.Fixed, ComponentDependency.None, 1);
        created.IsActive.ShouldBeTrue();

        // Step 2: Update
        var updateInput = new UpdateDeductionComponentDto
        {
            Name = "Full Lifecycle Deduction Updated",
            Description = "Updated description",
            ComponentType = DeductionComponentType.Variable,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 2500m,
            VisibleInPayslip = false,
            DisplayOrder = 2
        };
        var updated = await _deductionComponentAppService.UpdateAsync(created.Id, updateInput);
        updated.Name.ShouldBe("Full Lifecycle Deduction Updated");
        updated.Amount.ShouldBe(2500m);
        updated.VisibleInPayslip.ShouldBeFalse();

        // Step 3: Deactivate
        await _deductionComponentAppService.DeactivateAsync(created.Id);
        var deactivated = await _deductionComponentAppService.GetAsync(created.Id);
        deactivated.IsActive.ShouldBeFalse();

        // Step 4: Reactivate
        await _deductionComponentAppService.ActivateAsync(created.Id);
        var reactivated = await _deductionComponentAppService.GetAsync(created.Id);
        reactivated.IsActive.ShouldBeTrue();
        reactivated.Name.ShouldBe("Full Lifecycle Deduction Updated");

        // Step 5: Delete
        await _deductionComponentAppService.DeleteAsync(created.Id);
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _deductionComponentAppService.GetAsync(created.Id);
        });
    }

    [Fact]
    public async Task Should_Search_Only_Returns_Matching_Deductions()
    {
        // Arrange
        await CreateTestDeductionComponentAsync("SearchGamma Deduction One");
        await CreateTestDeductionComponentAsync("SearchGamma Deduction Two");
        await CreateTestDeductionComponentAsync("SearchDelta Deduction Three");

        // Act
        var result = await _deductionComponentAppService.GetFilteredListAsync(0, 100, "SearchGamma");

        // Assert
        result.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
        result.Items.ShouldAllBe(x => x.Name.Contains("SearchGamma"));
        result.Items.ShouldNotContain(x => x.Name.Contains("SearchDelta"));
    }

    [Fact]
    public async Task Should_Include_Deactivated_In_GetListAsync()
    {
        // Arrange
        var component = await CreateTestDeductionComponentAsync("Deactivated In Full List Ded");
        await _deductionComponentAppService.DeactivateAsync(component.Id);

        // Act
        var allList = await _deductionComponentAppService.GetListAsync(new PagedAndSortedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });

        // Assert
        allList.Items.ShouldContain(x => x.Id == component.Id);
        var found = allList.Items.First(x => x.Id == component.Id);
        found.IsActive.ShouldBeFalse();
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

    private async Task<EmployeePersonalInfoDto> CreateEmployeeAsync(string suffix)
    {
        var roleId = await EnsureRoleExistsAsync();
        var shiftId = await EnsureShiftExistsAsync();
        var unique = Guid.NewGuid().ToString("N")[..6];

        return await _employeePersonalInfoAppService.CreateAsync(new CreateEmployeePersonalInfoDto
        {
            FirstName = $"Emp{unique}",
            LastName = suffix,
            MobileNo = $"91{Random.Shared.Next(10000000, 99999999)}",
            Email = $"{suffix}.{Guid.NewGuid():N}@test.com",
            DateOfBirth = new DateOnly(1990, 5, 15),
            GenderType = GenderType.Male,
            DateOfJoining = new DateOnly(2024, 1, 1),
            EmployeeStatusType = EmployeeStatusType.Active,
            RoleId = roleId,
            ShiftId = shiftId
        });
    }

    private async Task<Guid> EnsureRoleExistsAsync()
    {
        var existing = (await _roleRepository.GetListAsync()).FirstOrDefault();
        if (existing != null)
        {
            return existing.Id;
        }

        var id = _guidGenerator.Create();
        await _roleRepository.InsertAsync(new IdentityRole(id, $"TestRole-{id:N}", _currentTenant.Id), autoSave: true);
        return id;
    }

    private async Task<Guid> EnsureShiftExistsAsync()
    {
        var existing = (await _shiftRepository.GetListAsync()).FirstOrDefault();
        if (existing != null)
        {
            return existing.Id;
        }

        var shift = new ShiftManagement
        {
            ShiftName = "Deduction Component Test Shift",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0)
        };

        await _shiftRepository.InsertAsync(shift, autoSave: true);
        return shift.Id;
    }

    #endregion
}
