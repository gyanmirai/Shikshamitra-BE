using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// End-to-End tests for Employee App Service
/// </summary>
public class EmployeeAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly IEmployeeAppService _employeeAppService;
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;

    public EmployeeAppService_Tests()
    {
        _employeeAppService = GetRequiredService<IEmployeeAppService>();
        _roleRepository = GetRequiredService<IIdentityRoleRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    #region Create Tests

    [Fact]
    public async Task Should_Create_Employee_With_Basic_Info()
    {
        // Arrange
        var roleId = await EnsureRoleExistsAsync();
        var input = new CreateEmployeeBasicInfoDto
        {
            FirstName = "John",
            MiddleName = "Robert",
            LastName = "Doe",
            MobileNo = "9876543210",
            Email = "john.doe@test.com",
            DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 5, 15)),
            GenderType = GenderType.Male,
            DateOfJoining = DateOnly.FromDateTime(new DateTime(2023, 1, 1)),
            EmployeeStatusType = EmployeeStatusType.Active,
            RoleId = roleId
        };

        // Act
        var result = await _employeeAppService.CreateBasicInfoAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        result.FirstName.ShouldBe(input.FirstName);
        result.MiddleName.ShouldBe(input.MiddleName);
        result.LastName.ShouldBe(input.LastName);
        result.MobileNo.ShouldBe(input.MobileNo);
        result.Email.ShouldBe(input.Email);
        result.EmployeeCode.ShouldNotBeNullOrEmpty();
        result.EmployeeStatusType.ShouldBe(EmployeeStatusType.Active);
    }

    [Fact]
    public async Task Should_Generate_Unique_Employee_Code()
    {
        // Arrange & Act
        var employee1 = await CreateTestEmployeeAsync("Employee", "One");
        var employee2 = await CreateTestEmployeeAsync("Employee", "Two");

        // Assert
        employee1.EmployeeCode.ShouldNotBe(employee2.EmployeeCode);
        employee1.EmployeeCode.ShouldNotBeNullOrEmpty();
        employee2.EmployeeCode.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_Not_Create_Employee_Without_Required_Fields()
    {
        // Arrange
        var roleId = await EnsureRoleExistsAsync();
        var input = new CreateEmployeeBasicInfoDto
        {
            FirstName = "", // Empty first name
            LastName = "Doe",
            MobileNo = "9876543210",
            DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
            DateOfJoining = DateOnly.FromDateTime(new DateTime(2023, 1, 1)),
            RoleId = roleId
        };

        // Act & Assert
        await Should.ThrowAsync<UserFriendlyException>(async () =>
        {
            await _employeeAppService.CreateBasicInfoAsync(input);
        });
    }

    [Fact]
    public async Task Should_Not_Create_Employee_With_Invalid_Mobile()
    {
        // Arrange
        var roleId = await EnsureRoleExistsAsync();
        var input = new CreateEmployeeBasicInfoDto
        {
            FirstName = "John",
            LastName = "Doe",
            MobileNo = "123", // Invalid mobile number
            DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
            DateOfJoining = DateOnly.FromDateTime(new DateTime(2023, 1, 1)),
            RoleId = roleId
        };

        // Act & Assert
        await Should.ThrowAsync<UserFriendlyException>(async () =>
        {
            await _employeeAppService.CreateBasicInfoAsync(input);
        });
    }

    [Fact]
    public async Task Should_Not_Create_Employee_With_Invalid_Email()
    {
        // Arrange
        var roleId = await EnsureRoleExistsAsync();
        var input = new CreateEmployeeBasicInfoDto
        {
            FirstName = "John",
            LastName = "Doe",
            MobileNo = "9876543210",
            Email = "invalid-email", // Invalid email format
            DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
            DateOfJoining = DateOnly.FromDateTime(new DateTime(2023, 1, 1)),
            RoleId = roleId
        };

        // Act & Assert
        await Should.ThrowAsync<UserFriendlyException>(async () =>
        {
            await _employeeAppService.CreateBasicInfoAsync(input);
        });
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task Should_Get_Employee_By_Id()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Test", "Employee");

        // Act
        var result = await _employeeAppService.GetAsync(created.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.FirstName.ShouldBe(created.FirstName);
        result.LastName.ShouldBe(created.LastName);
    }

    [Fact]
    public async Task Should_Throw_Exception_When_Employee_Not_Found()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _employeeAppService.GetAsync(nonExistentId);
        });
    }

    [Fact]
    public async Task Should_Get_Paginated_Employee_List()
    {
        // Arrange
        for (int i = 1; i <= 15; i++)
        {
            await CreateTestEmployeeAsync($"Employee{i}", "Test");
        }

        var input = new PagedAndSortedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 10
        };

        // Act
        var result = await _employeeAppService.GetListAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeLessThanOrEqualTo(10);
        result.TotalCount.ShouldBeGreaterThanOrEqualTo(15);
    }

    [Fact]
    public async Task Should_Filter_Employee_List_By_Search_Term()
    {
        // Arrange
        var target = await CreateTestEmployeeAsync(
            "SearchTarget",
            "Teacher",
            email: "searchtarget.teacher@test.com",
            mobileNo: "9876543211");

        await CreateTestEmployeeAsync(
            "OtherPerson",
            "Teacher",
            email: "other.teacher@test.com",
            mobileNo: "9876543212");

        var input = new GetEmployeeListInput
        {
            Search = "searchtarget",
            SkipCount = 0,
            MaxResultCount = 50
        };

        // Act
        var result = await _employeeAppService.GetFilteredListAsync(input);

        // Assert
        result.Items.ShouldContain(x => x.Id == target.Id);
        result.Items.ShouldNotContain(x => x.Email == "other.teacher@test.com");
    }

    [Fact]
    public async Task Should_Combine_Search_With_Status_And_Role_Filters()
    {
        // Arrange
        var selectedRoleId = Guid.NewGuid();
        var otherRoleId = Guid.NewGuid();

        var matchingEmployee = await CreateTestEmployeeAsync(
            "FilterAlpha",
            "One",
            email: "filter.alpha.one@test.com",
            mobileNo: "9876543213",
            roleId: selectedRoleId);

        var wrongRoleEmployee = await CreateTestEmployeeAsync(
            "FilterAlpha",
            "Two",
            email: "filter.alpha.two@test.com",
            mobileNo: "9876543214",
            roleId: otherRoleId);

        var wrongStatusEmployee = await CreateTestEmployeeAsync(
            "FilterAlpha",
            "Three",
            email: "filter.alpha.three@test.com",
            mobileNo: "9876543215",
            roleId: selectedRoleId);

        await _employeeAppService.UpdateEmployeeStatus(
            wrongStatusEmployee.Id,
            EmployeeStatusType.Left,
            "Status filter verification");

        var input = new GetEmployeeListInput
        {
            Search = "filteralpha",
            Status = nameof(EmployeeStatusType.Active),
            RoleId = selectedRoleId,
            SkipCount = 0,
            MaxResultCount = 50
        };

        // Act
        var result = await _employeeAppService.GetFilteredListAsync(input);

        // Assert
        result.Items.Count(x => x.Id == matchingEmployee.Id).ShouldBe(1);
        result.Items.ShouldNotContain(x => x.Id == wrongRoleEmployee.Id);
        result.Items.ShouldNotContain(x => x.Id == wrongStatusEmployee.Id);
    }

    [Fact]
    public async Task Whitespace_Search_Should_Behave_Like_No_Search()
    {
        // Arrange
        await CreateTestEmployeeAsync(
            "Whitespace",
            "Match",
            email: "whitespace.match@test.com",
            mobileNo: "9876543216");

        var baseInput = new GetEmployeeListInput
        {
            SkipCount = 0,
            MaxResultCount = 20
        };

        // Act
        var withoutSearch = await _employeeAppService.GetFilteredListAsync(baseInput);
        var withWhitespaceSearch = await _employeeAppService.GetFilteredListAsync(new GetEmployeeListInput
        {
            SkipCount = 0,
            MaxResultCount = 20,
            Search = "   "
        });

        // Assert
        withoutSearch.TotalCount.ShouldBe(withWhitespaceSearch.TotalCount);
        withoutSearch.Items.Select(x => x.Id).ShouldBe(withWhitespaceSearch.Items.Select(x => x.Id));
    }

    #endregion

    #region Update Basic Info Tests

    [Fact]
    public async Task Should_Update_Employee_Basic_Info()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Original", "Name");

        var updateInput = new UpdateEmployeeBasicInfoDto
        {
            FirstName = "Updated",
            MiddleName = "Middle",
            LastName = "Name",
            MobileNo = "8888888888",
            Email = "updated@test.com",
            DateOfBirth = DateOnly.FromDateTime(new DateTime(1991, 6, 20)),
            GenderType = GenderType.Female,
            DateOfJoining = DateOnly.FromDateTime(new DateTime(2023, 6, 1)),
            RoleId = created.RoleId
        };

        // Act
        var result = await _employeeAppService.UpdateBasicInfoAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.FirstName.ShouldBe("Updated");
        result.MiddleName.ShouldBe("Middle");
        result.LastName.ShouldBe("Name");
        result.MobileNo.ShouldBe("8888888888");
        result.Email.ShouldBe("updated@test.com");
    }

    #endregion

    #region Update Family Info Tests

    [Fact]
    public async Task Should_Require_HR_Access_To_Update_Employee_Family_Info()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Test", "Employee");

        var updateInput = new UpdateEmployeeFamilyDetailsDto
        {
            FatherDetails = new ContactDetailsDto
            {
                Name = "John Doe Sr."
            },
            MotherDetails = new ContactDetailsDto
            {
                Name = "Jane Doe"
            },
            SpouseDetails = new ContactDetailsDto
            {
                Name = "Mary Doe"
            }
        };

        // Act & Assert
        await Should.ThrowAsync<AbpAuthorizationException>(async () =>
        {
            await _employeeAppService.UpdateFamilyInfo(created.Id, updateInput);
        });
    }

    #endregion

    #region Update Bank Details Tests

    [Fact]
    public async Task Should_Update_Employee_Bank_Details()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Test", "Employee");

        var updateInput = new BankDetailsDto
        {
            BankName = "Test Bank",
            AccountNumber = "1234567890",
            IfscCode = "TEST0001234",
            BranchName = "Main Branch"
        };

        // Act
        var result = await _employeeAppService.UpdateBankDetails(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.BankDetails.ShouldNotBeNull();
        result.BankDetails.BankName.ShouldBe("Test Bank");
        result.BankDetails.AccountNumber.ShouldBe("1234567890");
        result.BankDetails.IfscCode.ShouldBe("TEST0001234");
    }

    [Fact]
    public async Task Should_Not_Update_With_Invalid_IFSC_Code()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Test", "Employee");

        var updateInput = new BankDetailsDto
        {
            BankName = "Test Bank",
            AccountNumber = "1234567890",
            IfscCode = "INVALID", // Invalid IFSC format
            BranchName = "Main Branch"
        };

        // Act & Assert
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _employeeAppService.UpdateBankDetails(created.Id, updateInput);
        });
    }

    #endregion

    #region Additional Details Tests

    [Fact]
    public async Task Should_Get_Employee_Additional_Details()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Test", "Employee");

        // Act
        var result = await _employeeAppService.GetAdditionalDetailsAsync(created.Id);

        // Assert
        result.ShouldNotBeNull();
        result.EmployeeCode.ShouldBe(created.EmployeeCode);
    }

    [Fact]
    public async Task Should_Update_Employee_Additional_Details()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Test", "Employee");

        var updateInput = new UpdateEmployeeAdditionalDetailsDto
        {
            BplCardNo = "BPL-12345",
            PersonWithDisability = false,
            SecondLanguage = "Hindi",
            LibraryMember = true,
            TransportMember = false
        };

        // Act
        var result = await _employeeAppService.UpdateAdditionalDetailsAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.BplCardNo.ShouldBe("BPL-12345");
        result.PersonWithDisability.ShouldBeFalse();
        result.SecondLanguage.ShouldBe("Hindi");
        result.LibraryMember.ShouldBeTrue();
        result.TransportMember.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Update_Additional_Details_With_Disability()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Test", "Employee");

        var updateInput = new UpdateEmployeeAdditionalDetailsDto
        {
            PersonWithDisability = true,
            LibraryMember = false,
            TransportMember = true
        };

        // Act
        var result = await _employeeAppService.UpdateAdditionalDetailsAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.PersonWithDisability.ShouldBeTrue();
        result.TransportMember.ShouldBeTrue();
    }

    #endregion

    #region Employee Status Tests

    [Fact]
    public async Task Should_Update_Employee_Status()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Test", "Employee");
        created.EmployeeStatusType.ShouldBe(EmployeeStatusType.Active);

        // Act
        var result = await _employeeAppService.UpdateEmployeeStatus(
            created.Id,
            EmployeeStatusType.Inactive,
            "Employee resigned"
        );

        // Assert
        result.ShouldNotBeNull();
        result.EmployeeStatusType.ShouldBe(EmployeeStatusType.Inactive);
    }

    [Fact]
    public async Task Should_Support_All_Employee_Statuses()
    {
        // Arrange
        var employee = await CreateTestEmployeeAsync("Test", "Employee");

        // Act & Assert - Test all status transitions
        var inactive = await _employeeAppService.UpdateEmployeeStatus(
            employee.Id, EmployeeStatusType.Inactive, "Employee on probation");
        inactive.EmployeeStatusType.ShouldBe(EmployeeStatusType.Inactive);

        var left = await _employeeAppService.UpdateEmployeeStatus(
            employee.Id, EmployeeStatusType.Left, "Employee resigned");
        left.EmployeeStatusType.ShouldBe(EmployeeStatusType.Left);

        var active = await _employeeAppService.UpdateEmployeeStatus(
            employee.Id, EmployeeStatusType.Active, "Back to active");
        active.EmployeeStatusType.ShouldBe(EmployeeStatusType.Active);
    }

    #endregion

    #region Payroll Details Tests

    [Fact]
    public async Task Should_Get_Employee_Payroll_Details()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Test", "Employee");

        // Act
        var result = await _employeeAppService.GetPayrollDetailsAsync(created.Id);

        // Assert
        result.ShouldNotBeNull();
        result.EmployeeId.ShouldBe(created.EmployeeCode);
    }

    [Fact]
    public async Task Should_Update_Employee_Payroll_Details()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Test", "Employee");

        var updateInput = new UpdateEmployeePayrollDetailsDto
        {
            PayrollDetails = new PayrollDetailsDto
            {
                BasicSalary = 50000,
                TdsPercentage = 10
            },
            Earnings = new List<PayrollComponentDto>
            {
                new PayrollComponentDto
                {
                    Name = "HRA",
                    ComponentType = SalaryComponentType.Fixed,
                    Amount = 15000,
                    IsFixed = false,
                    IsTemp = false
                },
                new PayrollComponentDto
                {
                    Name = "Special Allowance",
                    ComponentType = SalaryComponentType.Percentage,
                    Amount = 5000,
                    IsFixed = false,
                    IsTemp = false,
                    DependOn = SalaryComponentDependency.Basic
                }
            },
            Deductions = new List<PayrollComponentDto>
            {
                new PayrollComponentDto
                {
                    Name = "PF",
                    ComponentType = SalaryComponentType.Fixed,
                    Amount = 6000,
                    IsFixed = true,
                    IsTemp = false
                }
            }
        };

        // Act
        await _employeeAppService.UpdatePayrollDetailsAsync(created.Id, updateInput);

        // Assert
        var result = await _employeeAppService.GetPayrollDetailsAsync(created.Id);
        result.ShouldNotBeNull();
        result.PayrollDetails.ShouldNotBeNull();
        result.PayrollDetails.BasicSalary.ShouldBe(50000);
        result.Earnings.Count.ShouldBe(2);
        result.Deductions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Replace_Existing_Payroll_Components()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Test", "Employee");

        // First update with 2 earnings
        var firstUpdate = new UpdateEmployeePayrollDetailsDto
        {
            PayrollDetails = new PayrollDetailsDto
            {
                BasicSalary = 40000
            },
            Earnings = new List<PayrollComponentDto>
            {
                new PayrollComponentDto { Name = "HRA", ComponentType = SalaryComponentType.Fixed, Amount = 10000, IsFixed = false, IsTemp = false },
                new PayrollComponentDto { Name = "DA", ComponentType = SalaryComponentType.Fixed, Amount = 5000, IsFixed = false, IsTemp = false }
            },
            Deductions = new List<PayrollComponentDto>()
        };

        await _employeeAppService.UpdatePayrollDetailsAsync(created.Id, firstUpdate);

        // Second update with 1 earning (should replace, not add)
        var secondUpdate = new UpdateEmployeePayrollDetailsDto
        {
            PayrollDetails = new PayrollDetailsDto
            {
                BasicSalary = 50000
            },
            Earnings = new List<PayrollComponentDto>
            {
                new PayrollComponentDto { Name = "HRA", ComponentType = SalaryComponentType.Fixed, Amount = 15000, IsFixed = false, IsTemp = false }
            },
            Deductions = new List<PayrollComponentDto>()
        };

        // Act
        await _employeeAppService.UpdatePayrollDetailsAsync(created.Id, secondUpdate);

        // Assert
        var result = await _employeeAppService.GetPayrollDetailsAsync(created.Id);
        result.Earnings.Count.ShouldBe(1); // Should have only 1, not 3
        result.Earnings[0].Name.ShouldBe("HRA");
        result.Earnings[0].Amount.ShouldBe(15000);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Should_Delete_Employee()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Test", "Employee");

        // Act
        await _employeeAppService.DeleteAsync(created.Id);

        // Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _employeeAppService.GetAsync(created.Id);
        });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Should_Handle_Employee_With_Long_Names()
    {
        // Arrange
        var roleId = await EnsureRoleExistsAsync();
        var input = new CreateEmployeeBasicInfoDto
        {
            FirstName = "VeryLongFirstNameThatExceedsNormalLength",
            MiddleName = "VeryLongMiddleNameThatExceedsNormalLength",
            LastName = "VeryLongLastNameThatExceedsNormalLength",
            MobileNo = "9876543210",
            Email = "long.names@test.com",
            DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
            DateOfJoining = DateOnly.FromDateTime(new DateTime(2023, 1, 1)),
            RoleId = roleId
        };

        // Act & Assert - Should either succeed or throw validation error
        // This tests that the system handles long names appropriately
        try
        {
            var result = await _employeeAppService.CreateBasicInfoAsync(input);
            result.ShouldNotBeNull();
        }
        catch (AbpValidationException)
        {
            // Expected if name length validation is in place
        }
    }

    [Fact]
    public async Task Should_Handle_Null_Optional_Middle_Name()
    {
        // Arrange
        var roleId = await EnsureRoleExistsAsync();
        var input = new CreateEmployeeBasicInfoDto
        {
            FirstName = "John",
            MiddleName = null, // Optional
            LastName = "Doe",
            MobileNo = "9876543210",
            Email = "john.optional@test.com",
            DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
            DateOfJoining = DateOnly.FromDateTime(new DateTime(2023, 1, 1)),
            RoleId = roleId
        };

        // Act
        var result = await _employeeAppService.CreateBasicInfoAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.MiddleName.ShouldBeNull();
        result.Email.ShouldBe("john.optional@test.com");
    }

    [Fact]
    public async Task Should_Handle_Special_Characters_In_Names()
    {
        // Arrange
        var roleId = await EnsureRoleExistsAsync();
        var input = new CreateEmployeeBasicInfoDto
        {
            FirstName = "Jean-Paul",
            MiddleName = "O'Brien",
            LastName = "D'Angelo",
            MobileNo = "9876543210",
            Email = "jeanpaul.dangelo@test.com",
            DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
            DateOfJoining = DateOnly.FromDateTime(new DateTime(2023, 1, 1)),
            RoleId = roleId
        };

        // Act
        var result = await _employeeAppService.CreateBasicInfoAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.FirstName.ShouldBe("Jean-Paul");
        result.MiddleName.ShouldBe("O'Brien");
        result.LastName.ShouldBe("D'Angelo");
    }

    #endregion

    #region Helper Methods

    private async Task<EmployeeDto> CreateTestEmployeeAsync(
        string firstName,
        string lastName,
        string? email = null,
        string? mobileNo = null,
        Guid? roleId = null)
    {
        var resolvedRoleId = await EnsureRoleExistsAsync(roleId);

        var input = new CreateEmployeeBasicInfoDto
        {
            FirstName = firstName,
            LastName = lastName,
            MobileNo = mobileNo ?? "9876543210",
            Email = email ?? $"{firstName.ToLower()}.{lastName.ToLower()}@test.com",
            DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 5, 15)),
            GenderType = GenderType.Male,
            DateOfJoining = DateOnly.FromDateTime(DateTime.Now.Date),
            EmployeeStatusType = EmployeeStatusType.Active,
            RoleId = resolvedRoleId
        };

        return await _employeeAppService.CreateBasicInfoAsync(input);
    }

    private async Task<Guid> EnsureRoleExistsAsync(Guid? roleId = null)
    {
        var id = roleId ?? _guidGenerator.Create();

        if (await _roleRepository.FindAsync(id) != null)
        {
            return id;
        }

        var role = new IdentityRole(id, $"TestRole-{id:N}", _currentTenant.Id);
        await _roleRepository.InsertAsync(role, autoSave: true);

        return id;
    }

    #endregion
}

