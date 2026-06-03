using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Payroll.SalarySlips;
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
/// End-to-End tests for Earning Component App Service
/// </summary>
public class EarningComponentAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly IEarningComponentAppService _earningComponentAppService;
    private readonly IDeductionComponentAppService _deductionComponentAppService;
    private readonly IEmployeePersonalInfoAppService _employeePersonalInfoAppService;
    private readonly IEmployeePayrollAppService _employeePayrollAppService;
    private readonly IEmployeePayrollComponentRepository _employeePayrollComponentRepository;
    private readonly ISalarySlipRepository _salarySlipRepository;
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;

    public EarningComponentAppService_Tests()
    {
        _earningComponentAppService = GetRequiredService<IEarningComponentAppService>();
        _deductionComponentAppService = GetRequiredService<IDeductionComponentAppService>();
        _employeePersonalInfoAppService = GetRequiredService<IEmployeePersonalInfoAppService>();
        _employeePayrollAppService = GetRequiredService<IEmployeePayrollAppService>();
        _employeePayrollComponentRepository = GetRequiredService<IEmployeePayrollComponentRepository>();
        _salarySlipRepository = GetRequiredService<ISalarySlipRepository>();
        _roleRepository = GetRequiredService<IIdentityRoleRepository>();
        _shiftRepository = GetRequiredService<IRepository<ShiftManagement, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
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
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 50000m,
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
        // Arrange - create a base component first to reference
        var baseComponent = await CreateTestEarningComponentAsync("Base Salary For Variable");

        var input = new CreateEarningComponentDto
        {
            Name = "Test Performance Bonus",
            Description = "Quarterly performance bonus",
            ComponentType = EarningComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Amount = null,
            Percentage = 10m,
            SelectedEarningComponentId = baseComponent.Id,
            VisibleInPayslip = true,
            DisplayOrder = 5
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.ComponentType.ShouldBe(EarningComponentType.Variable);
        result.DependOn.ShouldBe(ComponentDependency.Basic);
        result.CalculationType.ShouldBe(CalculationType.Percentage);
        result.Percentage.ShouldBe(10m);
        result.Amount.ShouldBeNull();
        result.SelectedEarningComponentId.ShouldBe(baseComponent.Id);
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
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 10000m
        };

        await _earningComponentAppService.CreateAsync(input1);

        var input2 = new CreateEarningComponentDto
        {
            Name = componentName,
            ComponentType = EarningComponentType.Variable,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 5000m
        };

        // Act & Assert
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _earningComponentAppService.CreateAsync(input2);
        });
    }

    [Fact]
    public async Task Should_Retire_Component_After_Delete()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = "Reusable Deleted Name",
            ComponentType = EarningComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 1200m
        };

        var created = await _earningComponentAppService.CreateAsync(input);
        await DeleteEarningComponentWithPreviewAsync(created.Id);

        // Assert
        var retired = await _earningComponentAppService.GetAsync(created.Id);
        retired.IsActive.ShouldBeFalse();
        var active = await _earningComponentAppService.GetAllActiveAsync(new PagedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });
        active.Items.ShouldNotContain(x => x.Id == created.Id);
    }

    [Fact]
    public async Task Should_Allow_Same_Name_As_Deduction_Component()
    {
        // Arrange
        const string sharedName = "Shared Name Across Modules";

        await _deductionComponentAppService.CreateAsync(new CreateDeductionComponentDto
        {
            Name = sharedName,
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 250m
        });

        // Act
        var createdEarning = await _earningComponentAppService.CreateAsync(new CreateEarningComponentDto
        {
            Name = sharedName,
            ComponentType = EarningComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 1250m
        });

        // Assert
        createdEarning.Name.ShouldBe(sharedName);
    }

    [Fact]
    public async Task Should_Not_Create_Without_Required_Fields()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = "", // Empty name
            ComponentType = EarningComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 10000m
        };

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
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 10000m
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
        var baseComponent = await CreateTestEarningComponentAsync("Base For Percentage Update");

        var updateInput = new UpdateEarningComponentDto
        {
            Name = "Updated Name",
            Description = "Updated description",
            ComponentType = EarningComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Gross,
            Amount = null,
            Percentage = 15m,
            SelectedEarningComponentId = baseComponent.Id,
            VisibleInPayslip = false,
            DisplayOrder = 10
        };

        // Act
        var result = await UpdateEarningComponentWithPreviewAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.Name.ShouldBe("Updated Name");
        result.Description.ShouldBe("Updated description");
        result.ComponentType.ShouldBe(EarningComponentType.Variable);
        result.DependOn.ShouldBe(ComponentDependency.Gross);
        result.CalculationType.ShouldBe(CalculationType.Percentage);
        result.Percentage.ShouldBe(15m);
        result.Amount.ShouldBeNull();
        result.VisibleInPayslip.ShouldBeFalse();
        result.DisplayOrder.ShouldBe(10);
    }

    [Fact]
    public async Task Update_Should_Require_Current_Preview_Hash()
    {
        var created = await CreateTestEarningComponentAsync($"Preview Required Earning {Guid.NewGuid():N}");
        var updateInput = new UpdateEarningComponentDto
        {
            Name = created.Name,
            Description = "Preview gate test",
            ComponentType = created.ComponentType,
            CalculationType = created.CalculationType,
            DependOn = created.DependOn,
            Amount = created.Amount,
            Percentage = created.Percentage,
            SelectedEarningComponentId = created.SelectedEarningComponentId
        };

        var missingHash = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _earningComponentAppService.UpdateAsync(created.Id, updateInput);
        });
        missingHash.Code.ShouldBe("ShikshaMitra:PayrollComponentPreviewRequired");

        updateInput.PreviewHash = "stale-preview";
        var staleHash = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _earningComponentAppService.UpdateAsync(created.Id, updateInput);
        });
        staleHash.Code.ShouldBe("ShikshaMitra:PayrollComponentPreviewExpired");
    }

    [Fact]
    public async Task Update_Preview_Hash_Should_Expire_When_Linked_Slip_Status_Changes()
    {
        var employee = await CreateEmployeeAsync("earning-update-slip-status");
        var master = await CreateTestEarningComponentAsync($"Update Slip Status Earning {Guid.NewGuid():N}");
        var effectiveMonth = DateTime.UtcNow.AddMonths(1).ToString("yyyy-MM");

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
            isEarning: true), autoSave: true);

        var draftSlip = new SalarySlip(
            _guidGenerator.Create(),
            _currentTenant.Id,
            employee.Id,
            effectiveMonth,
            new(),
            new(),
            DateTime.UtcNow);
        await _salarySlipRepository.InsertAsync(draftSlip, autoSave: true);

        var updateInput = new UpdateEarningComponentDto
        {
            Name = master.Name,
            Description = master.Description,
            ComponentType = master.ComponentType,
            CalculationType = master.CalculationType,
            DependOn = master.DependOn,
            Amount = master.Amount,
            Percentage = master.Percentage,
            SelectedEarningComponentId = master.SelectedEarningComponentId,
            VisibleInPayslip = master.VisibleInPayslip,
            EffectiveMonth = effectiveMonth
        };
        updateInput.PreviewHash = (await _earningComponentAppService.PreviewUpdateImpactAsync(master.Id, updateInput)).PreviewHash;

        draftSlip.FinalizeSlip();
        await _salarySlipRepository.UpdateAsync(draftSlip, autoSave: true);

        var staleHash = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _earningComponentAppService.UpdateAsync(master.Id, updateInput);
        });
        staleHash.Code.ShouldBe("ShikshaMitra:PayrollComponentPreviewExpired");
    }

    [Fact]
    public async Task Update_And_Preview_Should_Reject_Inactive_Earning_Component()
    {
        var created = await CreateTestEarningComponentAsync($"Inactive Update Earning {Guid.NewGuid():N}");
        await _earningComponentAppService.DeactivateAsync(created.Id);
        var updateInput = new UpdateEarningComponentDto
        {
            Name = created.Name,
            Description = created.Description,
            ComponentType = created.ComponentType,
            CalculationType = created.CalculationType,
            DependOn = created.DependOn,
            Amount = created.Amount,
            Percentage = created.Percentage,
            SelectedEarningComponentId = created.SelectedEarningComponentId,
            VisibleInPayslip = created.VisibleInPayslip,
            PreviewHash = "any-preview-hash"
        };

        var previewError = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _earningComponentAppService.PreviewUpdateImpactAsync(created.Id, updateInput);
        });
        previewError.Code.ShouldBe("ShikshaMitra:PayrollComponentInactive");

        var updateError = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _earningComponentAppService.UpdateAsync(created.Id, updateInput);
        });
        updateError.Code.ShouldBe("ShikshaMitra:PayrollComponentInactive");
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
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 10000m
        };

        // Act & Assert
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await UpdateEarningComponentWithPreviewAsync(component2.Id, updateInput);
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
            CalculationType = created.CalculationType,
            DependOn = created.DependOn,
            Amount = created.Amount,
            Percentage = created.Percentage,
            SelectedEarningComponentId = created.SelectedEarningComponentId
        };

        // Act
        var result = await UpdateEarningComponentWithPreviewAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Same Name Component");
        result.Description.ShouldBe("Updated description only");
    }

    [Fact]
    public async Task Should_Propagate_Update_To_Linked_Employee_Earning_Components_Only()
    {
        var employee = await CreateEmployeeAsync("earning-sync");
        var originalComponentName = $"Linked Basic Salary {Guid.NewGuid():N}";
        var master = await CreateTestEarningComponentAsync(originalComponentName);
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
            isEarning: true), autoSave: true);

        await _employeePayrollComponentRepository.InsertAsync(new EmployeePayrollComponent(
            unlinkedEmployeeRowId,
            employee.Id,
            master.Name,
            componentId: null,
            "Custom employee value",
            999m,
            isFixed: true,
            isTemp: false,
            applicableMonth: null,
            componentType: SalaryComponentType.Fixed,
            dependOn: SalaryComponentDependency.None,
            isEarning: true), autoSave: true);

        await UpdateEarningComponentWithPreviewAsync(master.Id, new UpdateEarningComponentDto
        {
            Name = "Updated Basic Salary",
            Description = "Updated from master",
            ComponentType = EarningComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 25000m,
            VisibleInPayslip = true,
            DisplayOrder = 1,
            EffectiveMonth = DateTime.Now.ToString("yyyy-MM")
        });

        var rows = await _employeePayrollComponentRepository.GetEarningsByEmployeeIdAsync(employee.Id);
        var linked = rows.Single(x => x.ComponentId == master.Id);
        linked.Name.ShouldBe("Updated Basic Salary");
        linked.Description.ShouldBe("Updated from master");
        linked.Amount.ShouldBe(25000m);

        var unlinked = rows.Single(x => x.Id == unlinkedEmployeeRowId);
        unlinked.Name.ShouldBe(originalComponentName);
        unlinked.Description.ShouldBe("Custom employee value");
        unlinked.Amount.ShouldBe(999m);
    }

    [Fact]
    public async Task Should_Propagate_Percentage_Update_To_Linked_Employee_Earning_Components()
    {
        var employee = await CreateEmployeeAsync("earning-percentage-sync");
        var baseComponent = await CreateTestEarningComponentAsync($"Base For Percentage {Guid.NewGuid():N}");
        var percentageComponent = await _earningComponentAppService.CreateAsync(new CreateEarningComponentDto
        {
            Name = $"HRA Percentage {Guid.NewGuid():N}",
            Description = "Percentage earning",
            ComponentType = EarningComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 10m,
            SelectedEarningComponentId = baseComponent.Id,
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
            isEarning: true), autoSave: true);

        await UpdateEarningComponentWithPreviewAsync(percentageComponent.Id, new UpdateEarningComponentDto
        {
            Name = "Updated HRA Percentage",
            Description = "Updated percentage earning",
            ComponentType = EarningComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 15m,
            SelectedEarningComponentId = baseComponent.Id,
            VisibleInPayslip = true,
            DisplayOrder = 2,
            EffectiveMonth = DateTime.Now.ToString("yyyy-MM")
        });

        var linked = (await _employeePayrollComponentRepository.GetEarningsByEmployeeIdAsync(employee.Id))
            .Single(x => x.ComponentId == percentageComponent.Id);
        linked.Name.ShouldBe("Updated HRA Percentage");
        linked.Amount.ShouldBe(15m);
        linked.IsFixed.ShouldBeFalse();
        linked.ComponentType.ShouldBe(SalaryComponentType.Percentage);
        linked.DependOn.ShouldBe(SalaryComponentDependency.Basic);
    }

    [Fact]
    public async Task Should_Save_Percentage_Master_Components_Without_Explicit_Line_Amount()
    {
        var employee = await CreateEmployeeAsync("earning-percentage-payroll");
        var baseEarning = await CreateTestEarningComponentAsync($"Base Payroll Percentage {Guid.NewGuid():N}");
        var percentageEarning = await _earningComponentAppService.CreateAsync(new CreateEarningComponentDto
        {
            Name = $"Payroll HRA Percentage {Guid.NewGuid():N}",
            Description = "Percentage earning used by payroll save",
            ComponentType = EarningComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 12m,
            SelectedEarningComponentId = baseEarning.Id,
            VisibleInPayslip = true,
            DisplayOrder = 2
        });

        await _employeePayrollAppService.UpdatePayrollContractAsync(employee.Id, new PayrollContract
        {
            BankDetails = new BankDetailsContract
            {
                BankName = "State Bank of India",
                BranchName = "Main",
                AccountHolderName = employee.FullName,
                AccountNumber = "1234567890",
                IfscCode = "SBIN0001234"
            },
            SalaryDetails = new SalaryDetailsContract
            {
                TotalWorkingHourPerMonth = 176m
            },
            EarningComponents =
            [
                new SalaryComponentContract
                {
                    ComponentId = baseEarning.Id,
                    Name = baseEarning.Name,
                    Amount = 50000m
                },
                new SalaryComponentContract
                {
                    ComponentId = percentageEarning.Id,
                    Name = percentageEarning.Name
                }
            ]
        });

        var saved = (await _employeePayrollComponentRepository.GetEarningsByEmployeeIdAsync(employee.Id))
            .Single(x => x.ComponentId == percentageEarning.Id);
        saved.Amount.ShouldBe(6000m);
        saved.ComponentType.ShouldBe(SalaryComponentType.Percentage);
        saved.IsFixed.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Preview_And_Force_Apply_Earning_Component_Manual_Overrides()
    {
        var syncedEmployee = await CreateEmployeeAsync("earning-preview-synced");
        var overriddenEmployee = await CreateEmployeeAsync("earning-preview-override");
        var master = await CreateTestEarningComponentAsync($"Preview Basic {Guid.NewGuid():N}");
        var effectiveMonth = DateTime.Now.AddMonths(1).ToString("yyyy-MM");
        var forceEffectiveMonth = DateTime.Now.AddMonths(2).ToString("yyyy-MM");

        await _employeePayrollComponentRepository.InsertAsync(new EmployeePayrollComponent(
            Guid.NewGuid(), syncedEmployee.Id, master.Name, master.Id, master.Description, master.Amount ?? 0m,
            isFixed: true, isTemp: false, applicableMonth: null, componentType: SalaryComponentType.Fixed,
            dependOn: SalaryComponentDependency.None, isEarning: true, usesMasterValue: true), autoSave: true);

        await _employeePayrollComponentRepository.InsertAsync(new EmployeePayrollComponent(
            Guid.NewGuid(), overriddenEmployee.Id, master.Name, master.Id, master.Description, 7777m,
            isFixed: true, isTemp: false, applicableMonth: null, componentType: SalaryComponentType.Fixed,
            dependOn: SalaryComponentDependency.None, isEarning: true, usesMasterValue: false), autoSave: true);

        var update = new UpdateEarningComponentDto
        {
            Name = "Preview Basic Updated",
            Description = "Preview update",
            ComponentType = EarningComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 22000m,
            EffectiveMonth = effectiveMonth
        };

        var preview = await _earningComponentAppService.PreviewUpdateImpactAsync(master.Id, update);
        preview.SyncedEmployeeCount.ShouldBe(1);
        preview.ManualOverrideEmployeeCount.ShouldBe(1);
        preview.EffectiveMonth.ShouldBe(effectiveMonth);
        preview.PreviewHash.ShouldNotBeNullOrWhiteSpace();

        update.PreviewHash = preview.PreviewHash;
        await UpdateEarningComponentWithPreviewAsync(master.Id, update);
        var normalRows = await _employeePayrollComponentRepository.GetByComponentIdAsync(master.Id, isEarning: true);
        normalRows.Single(x => x.EmployeeId == syncedEmployee.Id).Amount.ShouldBe(master.Amount ?? 0m);
        normalRows.Single(x => x.EmployeeId == overriddenEmployee.Id).Amount.ShouldBe(7777m);
        normalRows.Single(x => x.EmployeeId == overriddenEmployee.Id).UsesMasterValue.ShouldBeFalse();

        var forceUpdate = new UpdateEarningComponentDto
        {
            Name = "Preview Basic Force",
            Description = "Force update",
            ComponentType = EarningComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 24000m,
            EffectiveMonth = forceEffectiveMonth,
            ApplyMode = PayrollComponentApplyMode.ForceApply
        };
        forceUpdate.PreviewHash = (await _earningComponentAppService.PreviewUpdateImpactAsync(master.Id, forceUpdate)).PreviewHash;

        await UpdateEarningComponentWithPreviewAsync(master.Id, forceUpdate);
        var forceRows = await _employeePayrollComponentRepository.GetByComponentIdAsync(master.Id, isEarning: true);
        forceRows.Single(x => x.EmployeeId == syncedEmployee.Id).Amount.ShouldBe(master.Amount ?? 0m);
        var forcedOverride = forceRows.Single(x => x.EmployeeId == overriddenEmployee.Id);
        forcedOverride.Amount.ShouldBe(7777m);
        forcedOverride.UsesMasterValue.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Save_ReAdded_Percentage_Deduction_When_Line_Amount_Is_Zero()
    {
        var employee = await CreateEmployeeAsync("deduction-percentage-readd-payroll");
        var baseEarning = await CreateTestEarningComponentAsync($"Base Payroll Deduction Percentage {Guid.NewGuid():N}");
        var percentageDeduction = await _deductionComponentAppService.CreateAsync(new CreateDeductionComponentDto
        {
            Name = $"Payroll PF Percentage {Guid.NewGuid():N}",
            Description = "Percentage deduction used by payroll re-add save",
            ComponentType = DeductionComponentType.Fixed,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 12m,
            SelectedDeductionComponentId = Guid.NewGuid(),
            VisibleInPayslip = true,
            DisplayOrder = 3
        });

        await _employeePayrollAppService.UpdatePayrollContractAsync(employee.Id, new PayrollContract
        {
            EarningComponents =
            [
                new SalaryComponentContract
                {
                    ComponentId = baseEarning.Id,
                    Name = baseEarning.Name,
                    Amount = 50000m
                }
            ],
            DeductionComponents =
            [
                new SalaryComponentContract
                {
                    ComponentId = percentageDeduction.Id,
                    Name = percentageDeduction.Name
                }
            ]
        });

        await _employeePayrollAppService.UpdatePayrollContractAsync(employee.Id, new PayrollContract
        {
            EarningComponents =
            [
                new SalaryComponentContract
                {
                    ComponentId = baseEarning.Id,
                    Name = baseEarning.Name,
                    Amount = 50000m
                }
            ],
            DeductionComponents = []
        });

        await _employeePayrollAppService.UpdatePayrollContractAsync(employee.Id, new PayrollContract
        {
            EarningComponents =
            [
                new SalaryComponentContract
                {
                    ComponentId = baseEarning.Id,
                    Name = baseEarning.Name,
                    Amount = 50000m
                }
            ],
            DeductionComponents =
            [
                new SalaryComponentContract
                {
                    ComponentId = percentageDeduction.Id,
                    Name = percentageDeduction.Name,
                    Amount = 0m,
                    IsFixed = true
                }
            ]
        });

        var saved = (await _employeePayrollComponentRepository.GetDeductionsByEmployeeIdAsync(employee.Id))
            .Single(x => x.ComponentId == percentageDeduction.Id);
        saved.Amount.ShouldBe(6000m);
        saved.ComponentType.ShouldBe(SalaryComponentType.Percentage);
        saved.DependOn.ShouldBe(SalaryComponentDependency.Basic);
        saved.IsFixed.ShouldBeFalse();
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
        await DeleteEarningComponentWithPreviewAsync(created.Id);

        // Assert
        var retired = await _earningComponentAppService.GetAsync(created.Id);
        retired.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_Should_Remove_Linked_Employee_Earnings_But_Keep_Custom_Rows()
    {
        var employee = await CreateEmployeeAsync("earning-delete");
        var master = await CreateTestEarningComponentAsync($"Delete Linked Earning {Guid.NewGuid():N}");
        var customRowId = Guid.NewGuid();

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
            isEarning: true), autoSave: true);

        await _employeePayrollComponentRepository.InsertAsync(new EmployeePayrollComponent(
            customRowId,
            employee.Id,
            "Custom Earning",
            componentId: null,
            "Employee custom earning",
            123m,
            isFixed: true,
            isTemp: true,
            applicableMonth: null,
            componentType: SalaryComponentType.Fixed,
            dependOn: SalaryComponentDependency.None,
            isEarning: true,
            usesMasterValue: false), autoSave: true);

        var preview = await _earningComponentAppService.PreviewDeleteImpactAsync(master.Id, includeEmployees: true);
        preview.LinkedEmployeeCount.ShouldBe(1);
        preview.SyncedEmployeeCount.ShouldBe(1);
        preview.SyncedEmployees.ShouldContain(x => x.EmployeeId == employee.Id);

        await DeleteEarningComponentWithPreviewAsync(master.Id);

        var rows = await _employeePayrollComponentRepository.GetEarningsByEmployeeIdAsync(employee.Id);
        rows.ShouldNotContain(x => x.ComponentId == master.Id);
        rows.ShouldContain(x => x.Id == customRowId);

        var retired = await _earningComponentAppService.GetAsync(master.Id);
        retired.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_Should_Not_Remove_Other_Tenant_Employee_Payroll_Rows()
    {
        var currentTenantEmployee = await CreateEmployeeAsync("earning-delete-tenant-current");
        var master = await CreateTestEarningComponentAsync($"Delete Tenant Scoped Earning {Guid.NewGuid():N}");
        var otherTenantId = Guid.NewGuid();
        Guid otherTenantEmployeeId;

        await _employeePayrollComponentRepository.InsertAsync(new EmployeePayrollComponent(
            Guid.NewGuid(),
            currentTenantEmployee.Id,
            master.Name,
            master.Id,
            master.Description,
            master.Amount ?? 0m,
            isFixed: true,
            isTemp: false,
            applicableMonth: null,
            componentType: SalaryComponentType.Fixed,
            dependOn: SalaryComponentDependency.None,
            isEarning: true), autoSave: true);

        using (_currentTenant.Change(otherTenantId))
        {
            var otherTenantEmployee = await CreateEmployeeAsync("earning-delete-tenant-other");
            otherTenantEmployeeId = otherTenantEmployee.Id;
            await _employeePayrollComponentRepository.InsertAsync(new EmployeePayrollComponent(
                Guid.NewGuid(),
                otherTenantEmployee.Id,
                master.Name,
                master.Id,
                master.Description,
                master.Amount ?? 0m,
                isFixed: true,
                isTemp: false,
                applicableMonth: null,
                componentType: SalaryComponentType.Fixed,
                dependOn: SalaryComponentDependency.None,
                isEarning: true), autoSave: true);
        }

        await DeleteEarningComponentWithPreviewAsync(master.Id);

        var currentTenantRows = await _employeePayrollComponentRepository.GetEarningsByEmployeeIdAsync(currentTenantEmployee.Id);
        currentTenantRows.ShouldNotContain(x => x.ComponentId == master.Id);

        using (_currentTenant.Change(otherTenantId))
        {
            var otherTenantRows = await _employeePayrollComponentRepository.GetEarningsByEmployeeIdAsync(otherTenantEmployeeId);
            otherTenantRows.ShouldContain(x => x.ComponentId == master.Id);
        }
    }

    [Fact]
    public async Task Delete_Should_Require_Current_Preview_Hash()
    {
        var component = await CreateTestEarningComponentAsync($"Delete Preview Required Earning {Guid.NewGuid():N}");

        var missingHash = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _earningComponentAppService.DeleteAsync(component.Id);
        });
        missingHash.Code.ShouldBe("ShikshaMitra:PayrollComponentPreviewRequired");

        var staleHash = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _earningComponentAppService.DeleteConfirmedAsync(component.Id, "stale-preview");
        });
        staleHash.Code.ShouldBe("ShikshaMitra:PayrollComponentPreviewExpired");
    }

    [Fact]
    public async Task Delete_Preview_Should_Count_Historical_Slips_For_Linked_Employees()
    {
        var employee = await CreateEmployeeAsync("earning-delete-history");
        var master = await CreateTestEarningComponentAsync($"Delete Historical Earning {Guid.NewGuid():N}");

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
            isEarning: true), autoSave: true);

        var finalizedSlip = new SalarySlip(
            _guidGenerator.Create(),
            _currentTenant.Id,
            employee.Id,
            "2024-01",
            new(),
            new(),
            DateTime.UtcNow);
        finalizedSlip.FinalizeSlip();
        await _salarySlipRepository.InsertAsync(finalizedSlip, autoSave: true);
        await _salarySlipRepository.InsertAsync(new SalarySlip(
            _guidGenerator.Create(),
            _currentTenant.Id,
            employee.Id,
            "2024-02",
            new(),
            new(),
            DateTime.UtcNow), autoSave: true);

        var preview = await _earningComponentAppService.PreviewDeleteImpactAsync(master.Id);

        preview.FinalizedSlipCount.ShouldBeGreaterThanOrEqualTo(1);
        preview.DraftSlipCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Delete_Preview_Hash_Should_Change_When_Historical_Slip_Status_Counts_Change()
    {
        var employee = await CreateEmployeeAsync("earning-delete-status-hash");
        var master = await CreateTestEarningComponentAsync($"Delete Status Hash Earning {Guid.NewGuid():N}");

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
            isEarning: true), autoSave: true);

        var draftSlip = new SalarySlip(
            _guidGenerator.Create(),
            _currentTenant.Id,
            employee.Id,
            "2024-03",
            new(),
            new(),
            DateTime.UtcNow);
        await _salarySlipRepository.InsertAsync(draftSlip, autoSave: true);

        var preview = await _earningComponentAppService.PreviewDeleteImpactAsync(master.Id);

        draftSlip.FinalizeSlip();
        await _salarySlipRepository.UpdateAsync(draftSlip, autoSave: true);

        var refreshedPreview = await _earningComponentAppService.PreviewDeleteImpactAsync(master.Id);
        refreshedPreview.PreviewHash.ShouldNotBe(preview.PreviewHash);
    }

    [Fact]
    public async Task Salary_Slip_Status_Counts_Should_Respect_Current_Tenant()
    {
        var currentTenantEmployee = await CreateEmployeeAsync("earning-slip-count-current");
        var otherTenantId = Guid.NewGuid();
        Guid otherTenantEmployeeId;

        var currentSlip = new SalarySlip(
            _guidGenerator.Create(),
            _currentTenant.Id,
            currentTenantEmployee.Id,
            "2024-04",
            new(),
            new(),
            DateTime.UtcNow);
        currentSlip.FinalizeSlip();
        await _salarySlipRepository.InsertAsync(currentSlip, autoSave: true);

        using (_currentTenant.Change(otherTenantId))
        {
            var otherTenantEmployee = await CreateEmployeeAsync("earning-slip-count-other");
            otherTenantEmployeeId = otherTenantEmployee.Id;
            var otherTenantSlip = new SalarySlip(
                _guidGenerator.Create(),
                otherTenantId,
                otherTenantEmployee.Id,
                "2024-04",
                new(),
                new(),
                DateTime.UtcNow);
            otherTenantSlip.FinalizeSlip();
            await _salarySlipRepository.InsertAsync(otherTenantSlip, autoSave: true);
        }

        var counts = await _salarySlipRepository.GetStatusCountsByEmployeeIdsAsync(
            new[] { currentTenantEmployee.Id, otherTenantEmployeeId });

        counts.FinalizedSlipCount.ShouldBe(1);
        counts.DraftSlipCount.ShouldBe(0);
    }

    [Fact]
    public async Task Delete_Should_Remove_Manual_Override_Earning_Rows()
    {
        var employee = await CreateEmployeeAsync("earning-delete-override");
        var master = await CreateTestEarningComponentAsync($"Delete Override Earning {Guid.NewGuid():N}");

        await _employeePayrollComponentRepository.InsertAsync(new EmployeePayrollComponent(
            Guid.NewGuid(),
            employee.Id,
            master.Name,
            master.Id,
            master.Description,
            999m,
            isFixed: true,
            isTemp: false,
            applicableMonth: null,
            componentType: SalaryComponentType.Fixed,
            dependOn: SalaryComponentDependency.None,
            isEarning: true,
            usesMasterValue: false), autoSave: true);

        var preview = await _earningComponentAppService.PreviewDeleteImpactAsync(master.Id);
        preview.LinkedEmployeeCount.ShouldBe(1);
        preview.ManualOverrideEmployeeCount.ShouldBe(1);

        await DeleteEarningComponentWithPreviewAsync(master.Id);

        var rows = await _employeePayrollComponentRepository.GetEarningsByEmployeeIdAsync(employee.Id);
        rows.ShouldNotContain(x => x.ComponentId == master.Id);
    }

    [Fact]
    public async Task Delete_Should_Exclude_Earning_From_Filtered_List()
    {
        var component = await CreateTestEarningComponentAsync($"Filtered Delete Earning {Guid.NewGuid():N}");
        await DeleteEarningComponentWithPreviewAsync(component.Id);

        var filtered = await _earningComponentAppService.GetFilteredListAsync(0, 100, component.Name);
        filtered.Items.ShouldNotContain(x => x.Id == component.Id);
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
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 5000m
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.Name.ShouldBe(input.Name);
    }

    [Fact]
    public async Task Should_Handle_Percentage_Amount()
    {
        // Arrange - create a base component first to reference
        var baseComponent = await CreateTestEarningComponentAsync("Base For Percentage Test");

        var input = new CreateEarningComponentDto
        {
            Name = "Percentage Component",
            ComponentType = EarningComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Amount = null,
            Percentage = 10.5m,
            SelectedEarningComponentId = baseComponent.Id
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.CalculationType.ShouldBe(CalculationType.Percentage);
        result.Percentage.ShouldBe(10.5m);
        result.Amount.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Handle_Large_Amount()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = "Large Amount Component",
            ComponentType = EarningComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 999999999m
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.Amount.ShouldBe(999999999m);
    }

    [Fact]
    public async Task Should_Handle_Empty_Description()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = $"No Description Component {Guid.NewGuid():N}",
            Description = null,
            ComponentType = EarningComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 10000m
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.Description.ShouldBeNull();
    }

    #endregion

    #region Tier 2 — Calculation Field Validation

    [Fact]
    public async Task Should_Reject_Negative_Amount()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = "Negative Amount Component",
            ComponentType = EarningComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = -100m
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _earningComponentAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Accept_Zero_Amount()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = "Zero Amount Component",
            ComponentType = EarningComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 0m
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.Amount.ShouldBe(0m);
    }

    [Fact]
    public async Task Should_Reject_Null_Amount_In_Amount_Mode()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = "Null Amount Component",
            ComponentType = EarningComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = null
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _earningComponentAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_Percentage_Over_100()
    {
        // Arrange
        var baseComponent = await CreateTestEarningComponentAsync("Base For Over 100");

        var input = new CreateEarningComponentDto
        {
            Name = "Over 100 Percent Component",
            ComponentType = EarningComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 101m,
            SelectedEarningComponentId = baseComponent.Id
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _earningComponentAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Accept_Percentage_At_Zero_Boundary()
    {
        // Arrange
        var baseComponent = await CreateTestEarningComponentAsync("Base For Zero Pct");

        var input = new CreateEarningComponentDto
        {
            Name = "Zero Percent Component",
            ComponentType = EarningComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 0m,
            SelectedEarningComponentId = baseComponent.Id
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.Percentage.ShouldBe(0m);
    }

    [Fact]
    public async Task Should_Accept_Percentage_At_100_Boundary()
    {
        // Arrange
        var baseComponent = await CreateTestEarningComponentAsync("Base For 100 Pct");

        var input = new CreateEarningComponentDto
        {
            Name = "Hundred Percent Component",
            ComponentType = EarningComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 100m,
            SelectedEarningComponentId = baseComponent.Id
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.Percentage.ShouldBe(100m);
    }

    [Fact]
    public async Task Should_Reject_Negative_Percentage()
    {
        // Arrange
        var baseComponent = await CreateTestEarningComponentAsync("Base For Neg Pct");

        var input = new CreateEarningComponentDto
        {
            Name = "Negative Percent Component",
            ComponentType = EarningComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = -5m,
            SelectedEarningComponentId = baseComponent.Id
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _earningComponentAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_Percentage_Without_SelectedComponentId()
    {
        // Arrange
        var input = new CreateEarningComponentDto
        {
            Name = "No Selected Component",
            ComponentType = EarningComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 10m,
            SelectedEarningComponentId = null
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _earningComponentAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_Null_Percentage_In_Percentage_Mode()
    {
        // Arrange
        var baseComponent = await CreateTestEarningComponentAsync("Base For Null Pct");

        var input = new CreateEarningComponentDto
        {
            Name = "Null Percentage Component",
            ComponentType = EarningComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = null,
            SelectedEarningComponentId = baseComponent.Id
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _earningComponentAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Clear_Amount_When_Switching_To_Percentage_On_Update()
    {
        // Arrange — create with Amount mode
        var created = await CreateTestEarningComponentAsync("Switch To Pct");
        created.Amount.ShouldBe(10000m);

        var baseComponent = await CreateTestEarningComponentAsync("Base For Switch");

        var updateInput = new UpdateEarningComponentDto
        {
            Name = "Switch To Pct",
            ComponentType = EarningComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 25m,
            SelectedEarningComponentId = baseComponent.Id
        };

        // Act
        var result = await UpdateEarningComponentWithPreviewAsync(created.Id, updateInput);

        // Assert — Amount should be cleared, Percentage should be set
        result.CalculationType.ShouldBe(CalculationType.Percentage);
        result.Percentage.ShouldBe(25m);
        result.Amount.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Clear_Percentage_When_Switching_To_Amount_On_Update()
    {
        // Arrange — create with Percentage mode
        var baseComponent = await CreateTestEarningComponentAsync("Base For Reverse Switch");

        var createInput = new CreateEarningComponentDto
        {
            Name = "Switch To Amount",
            ComponentType = EarningComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Basic,
            Percentage = 20m,
            SelectedEarningComponentId = baseComponent.Id
        };
        var created = await _earningComponentAppService.CreateAsync(createInput);
        created.Percentage.ShouldBe(20m);

        var updateInput = new UpdateEarningComponentDto
        {
            Name = "Switch To Amount",
            ComponentType = EarningComponentType.Fixed,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 50000m
        };

        // Act
        var result = await UpdateEarningComponentWithPreviewAsync(created.Id, updateInput);

        // Assert — Percentage should be cleared, Amount should be set
        result.CalculationType.ShouldBe(CalculationType.Amount);
        result.Amount.ShouldBe(50000m);
        result.Percentage.ShouldBeNull();
        result.SelectedEarningComponentId.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Create_With_Gross_Dependency()
    {
        // Arrange
        var baseComponent = await CreateTestEarningComponentAsync("Base For Gross Dep");

        var input = new CreateEarningComponentDto
        {
            Name = "Gross Dependent Component",
            ComponentType = EarningComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.Gross,
            Percentage = 5m,
            SelectedEarningComponentId = baseComponent.Id
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.DependOn.ShouldBe(ComponentDependency.Gross);
    }

    [Fact]
    public async Task Should_Create_With_CustomComponent_Dependency()
    {
        // Arrange
        var baseComponent = await CreateTestEarningComponentAsync("Base For Custom Dep");

        var input = new CreateEarningComponentDto
        {
            Name = "Custom Dependent Component",
            ComponentType = EarningComponentType.Variable,
            CalculationType = CalculationType.Percentage,
            DependOn = ComponentDependency.CustomComponent,
            Percentage = 8m,
            SelectedEarningComponentId = baseComponent.Id
        };

        // Act
        var result = await _earningComponentAppService.CreateAsync(input);

        // Assert
        result.DependOn.ShouldBe(ComponentDependency.CustomComponent);
    }

    [Fact]
    public async Task Should_Toggle_VisibleInPayslip()
    {
        // Arrange — create with VisibleInPayslip = true
        var created = await CreateTestEarningComponentAsync("Payslip Toggle");
        created.VisibleInPayslip.ShouldBeTrue();

        var updateInput = new UpdateEarningComponentDto
        {
            Name = "Payslip Toggle",
            ComponentType = created.ComponentType,
            CalculationType = created.CalculationType,
            DependOn = created.DependOn,
            Amount = created.Amount,
            VisibleInPayslip = false
        };

        // Act
        var result = await UpdateEarningComponentWithPreviewAsync(created.Id, updateInput);

        // Assert
        result.VisibleInPayslip.ShouldBeFalse();
    }

    #endregion

    #region Tier 4 — Activate/Deactivate Lifecycle

    [Fact]
    public async Task Should_Deactivate_And_Exclude_From_Active_List()
    {
        // Arrange
        var component = await CreateTestEarningComponentAsync("Deactivate Active List");
        component.IsActive.ShouldBeTrue();

        // Act — deactivate
        await _earningComponentAppService.DeactivateAsync(component.Id);

        // Assert — should not appear in active list
        var activeList = await _earningComponentAppService.GetAllActiveAsync(new PagedResultRequestDto
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
        var component = await CreateTestEarningComponentAsync("Reactivate Lifecycle");
        await _earningComponentAppService.DeactivateAsync(component.Id);

        // Verify excluded from active list
        var beforeReactivation = await _earningComponentAppService.GetAllActiveAsync(new PagedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });
        beforeReactivation.Items.ShouldNotContain(x => x.Id == component.Id);

        // Act — reactivate
        await _earningComponentAppService.ActivateAsync(component.Id);

        // Assert — should appear in active list again
        var afterReactivation = await _earningComponentAppService.GetAllActiveAsync(new PagedResultRequestDto
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
        var component = await CreateTestEarningComponentAsync("Data Retention");

        // Act — deactivate then reactivate
        await _earningComponentAppService.DeactivateAsync(component.Id);
        await _earningComponentAppService.ActivateAsync(component.Id);

        // Assert — all original data should be preserved
        var retrieved = await _earningComponentAppService.GetAsync(component.Id);
        retrieved.Name.ShouldBe("Data Retention");
        retrieved.Amount.ShouldBe(10000m);
        retrieved.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Handle_Full_Component_Lifecycle()
    {
        // Step 1: Create
        var created = await CreateTestEarningComponentAsync("Full Lifecycle Earning", EarningComponentType.Fixed, 1);
        created.IsActive.ShouldBeTrue();

        // Step 2: Update
        var updateInput = new UpdateEarningComponentDto
        {
            Name = "Full Lifecycle Earning Updated",
            Description = "Updated description",
            ComponentType = EarningComponentType.Variable,
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 25000m,
            VisibleInPayslip = false,
            DisplayOrder = 2
        };
        var updated = await UpdateEarningComponentWithPreviewAsync(created.Id, updateInput);
        updated.Name.ShouldBe("Full Lifecycle Earning Updated");
        updated.Amount.ShouldBe(25000m);
        updated.VisibleInPayslip.ShouldBeFalse();

        // Step 3: Deactivate
        await _earningComponentAppService.DeactivateAsync(created.Id);
        var deactivated = await _earningComponentAppService.GetAsync(created.Id);
        deactivated.IsActive.ShouldBeFalse();

        // Step 4: Reactivate
        await _earningComponentAppService.ActivateAsync(created.Id);
        var reactivated = await _earningComponentAppService.GetAsync(created.Id);
        reactivated.IsActive.ShouldBeTrue();
        reactivated.Name.ShouldBe("Full Lifecycle Earning Updated"); // Data preserved

        // Step 5: Delete
        await DeleteEarningComponentWithPreviewAsync(created.Id);
        var deleted = await _earningComponentAppService.GetAsync(created.Id);
        deleted.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Search_Only_Returns_Matching_Components()
    {
        // Arrange — create components with distinct names
        await CreateTestEarningComponentAsync("SearchAlpha Earning One");
        await CreateTestEarningComponentAsync("SearchAlpha Earning Two");
        await CreateTestEarningComponentAsync("SearchBeta Earning Three");

        // Act — search for "SearchAlpha"
        var result = await _earningComponentAppService.GetFilteredListAsync(0, 100, "SearchAlpha");

        // Assert — should only return the two Alpha matches
        result.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
        result.Items.ShouldAllBe(x => x.Name.Contains("SearchAlpha"));
        result.Items.ShouldNotContain(x => x.Name.Contains("SearchBeta"));
    }

    [Fact]
    public async Task Should_Exclude_Deactivated_Components_In_GetListAsync()
    {
        // Arrange — create and deactivate a component
        var component = await CreateTestEarningComponentAsync("Deactivated In Full List");
        await _earningComponentAppService.DeactivateAsync(component.Id);

        // Act — normal lists should hide inactive components
        var allList = await _earningComponentAppService.GetListAsync(new PagedAndSortedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });

        // Assert
        allList.Items.ShouldNotContain(x => x.Id == component.Id);
    }

    #endregion

    #region Helper Methods

    private async Task<EarningComponentDto> UpdateEarningComponentWithPreviewAsync(
        Guid id,
        UpdateEarningComponentDto input)
    {
        input.PreviewHash = (await _earningComponentAppService.PreviewUpdateImpactAsync(id, input)).PreviewHash;
        return await _earningComponentAppService.UpdateAsync(id, input);
    }

    private async Task DeleteEarningComponentWithPreviewAsync(Guid id)
    {
        var preview = await _earningComponentAppService.PreviewDeleteImpactAsync(id);
        await _earningComponentAppService.DeleteConfirmedAsync(id, preview.PreviewHash);
    }

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
            CalculationType = CalculationType.Amount,
            DependOn = ComponentDependency.None,
            Amount = 10000m,
            VisibleInPayslip = true,
            DisplayOrder = displayOrder
        };

        return await _earningComponentAppService.CreateAsync(input);
    }

    private async Task<EmployeePersonalInfoDto> CreateEmployeeAsync(string suffix)
    {
        var roleId = await EnsureRoleExistsAsync();
        var shiftId = await EnsureShiftExistsAsync();
        var organizationId = await Employees.EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
        var unique = Guid.NewGuid().ToString("N")[..6];

        return await _employeePersonalInfoAppService.CreateAsync(new CreateEmployeePersonalInfoDto
        {
            FirstName = $"Emp{unique}",
            LastName = suffix,
            MobileNo = $"91{Random.Shared.Next(10000000, 99999999)}",
            Email = $"{suffix}.{Guid.NewGuid():N}@test.com",
            PanNo = UniquePan(),
            AdharNo = UniqueAdhar(),
            DateOfBirth = new DateOnly(1990, 5, 15),
            GenderType = GenderType.Male,
            DateOfJoining = new DateOnly(2024, 1, 1),
            EmployeeStatusType = EmployeeStatusType.Active,
            RoleId = roleId,
            ShiftId = shiftId,
            OrganizationIds = [organizationId]
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
            ShiftName = "Earning Component Test Shift",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0)
        };

        await _shiftRepository.InsertAsync(shift, autoSave: true);
        return shift.Id;
    }

    #endregion
}

