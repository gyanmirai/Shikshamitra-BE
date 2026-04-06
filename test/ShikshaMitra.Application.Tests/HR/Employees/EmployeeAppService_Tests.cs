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

    #region Tier 2 — Date Validation

    [Fact]
    public async Task Should_Reject_Future_Date_Of_Birth()
    {
        // Arrange
        var roleId = await EnsureRoleExistsAsync();
        var input = new CreateEmployeeBasicInfoDto
        {
            FirstName = "Future",
            LastName = "DOB",
            MobileNo = "9876543210",
            Email = "future.dob@test.com",
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            DateOfJoining = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            RoleId = roleId
        };

        // Act & Assert — DTO IValidatableObject rejects future DOB
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _employeeAppService.CreateBasicInfoAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_Underage_Employee()
    {
        // Arrange — employee must be at least 18 years old at joining
        var roleId = await EnsureRoleExistsAsync();
        var input = new CreateEmployeeBasicInfoDto
        {
            FirstName = "Young",
            LastName = "Employee",
            MobileNo = "9876543210",
            Email = "young.employee@test.com",
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-16)),
            DateOfJoining = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            RoleId = roleId
        };

        // Act & Assert — DTO IValidatableObject rejects age < 18
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _employeeAppService.CreateBasicInfoAsync(input);
        });
    }

    #endregion

    #region Tier 2 — Duplicate & Missing Email

    [Fact]
    public async Task Should_Reject_Duplicate_Email()
    {
        // Arrange
        var email = "duplicate.email@test.com";
        await CreateTestEmployeeAsync("First", "Employee", email: email, mobileNo: "9876543217");

        // Act & Assert — second employee with same email should fail
        await Should.ThrowAsync<UserFriendlyException>(async () =>
        {
            await CreateTestEmployeeAsync("Second", "Employee", email: email, mobileNo: "9876543218");
        });
    }

    [Fact]
    public async Task Should_Reject_Null_Email()
    {
        // Arrange
        var roleId = await EnsureRoleExistsAsync();
        var input = new CreateEmployeeBasicInfoDto
        {
            FirstName = "No",
            LastName = "Email",
            MobileNo = "9876543210",
            Email = null, // Missing email
            DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
            DateOfJoining = DateOnly.FromDateTime(new DateTime(2023, 1, 1)),
            RoleId = roleId
        };

        // Act & Assert — app service rejects null email
        await Should.ThrowAsync<UserFriendlyException>(async () =>
        {
            await _employeeAppService.CreateBasicInfoAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_Whitespace_Only_Mobile()
    {
        // Arrange
        var roleId = await EnsureRoleExistsAsync();
        var input = new CreateEmployeeBasicInfoDto
        {
            FirstName = "Blank",
            LastName = "Mobile",
            MobileNo = "   ", // Whitespace only
            Email = "blank.mobile@test.com",
            DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
            DateOfJoining = DateOnly.FromDateTime(new DateTime(2023, 1, 1)),
            RoleId = roleId
        };

        // Act & Assert — app service rejects whitespace mobile
        await Should.ThrowAsync<UserFriendlyException>(async () =>
        {
            await _employeeAppService.CreateBasicInfoAsync(input);
        });
    }

    #endregion

    #region Tier 2 — Bank Details Validation

    [Fact]
    public async Task Should_Accept_Valid_IFSC_Code_Format()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("IFSC", "Valid", email: "ifsc.valid@test.com", mobileNo: "9876543219");

        var updateInput = new BankDetailsDto
        {
            BankName = "HDFC Bank",
            AccountNumber = "123456789012",
            IfscCode = "HDFC0001234",
            BranchName = "Mumbai Main"
        };

        // Act
        var result = await _employeeAppService.UpdateBankDetails(created.Id, updateInput);

        // Assert
        result.BankDetails.IfscCode.ShouldBe("HDFC0001234");
    }

    [Fact]
    public async Task Should_Reject_IFSC_Without_Zero_At_Fifth_Position()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("IFSC", "NoZero", email: "ifsc.nozero@test.com", mobileNo: "9876543220");

        var updateInput = new BankDetailsDto
        {
            BankName = "Test Bank",
            AccountNumber = "123456789012",
            IfscCode = "HDFC1001234", // 5th char should be 0
            BranchName = "Branch"
        };

        // Act & Assert
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _employeeAppService.UpdateBankDetails(created.Id, updateInput);
        });
    }

    [Fact]
    public async Task Should_Reject_Short_IFSC_Code()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("IFSC", "Short", email: "ifsc.short@test.com", mobileNo: "9876543221");

        var updateInput = new BankDetailsDto
        {
            BankName = "Test Bank",
            AccountNumber = "123456789012",
            IfscCode = "HDFC", // Too short
            BranchName = "Branch"
        };

        // Act & Assert
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _employeeAppService.UpdateBankDetails(created.Id, updateInput);
        });
    }

    [Fact]
    public async Task Should_Accept_Null_Bank_Details_Fields()
    {
        // Arrange — all bank fields are optional
        var created = await CreateTestEmployeeAsync("Bank", "Optional", email: "bank.optional@test.com", mobileNo: "9876543222");

        var updateInput = new BankDetailsDto
        {
            BankName = null,
            AccountNumber = null,
            IfscCode = null,
            BranchName = null
        };

        // Act
        var result = await _employeeAppService.UpdateBankDetails(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
    }

    #endregion

    #region Tier 2 — Payroll Validation

    [Fact]
    public async Task Should_Accept_Zero_BasicSalary()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Payroll", "ZeroSalary", email: "payroll.zero@test.com", mobileNo: "9876543223");

        var updateInput = new UpdateEmployeePayrollDetailsDto
        {
            PayrollDetails = new PayrollDetailsDto
            {
                BasicSalary = 0
            },
            Earnings = new List<PayrollComponentDto>(),
            Deductions = new List<PayrollComponentDto>()
        };

        // Act
        await _employeeAppService.UpdatePayrollDetailsAsync(created.Id, updateInput);

        // Assert
        var result = await _employeeAppService.GetPayrollDetailsAsync(created.Id);
        result.PayrollDetails.BasicSalary.ShouldBe(0);
    }

    [Fact]
    public async Task Should_Reject_Negative_Component_Amount_In_Payroll()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Payroll", "NegComp", email: "payroll.negcomp@test.com", mobileNo: "9876543224");

        var updateInput = new UpdateEmployeePayrollDetailsDto
        {
            PayrollDetails = new PayrollDetailsDto
            {
                BasicSalary = 50000
            },
            Earnings = new List<PayrollComponentDto>
            {
                new PayrollComponentDto
                {
                    Name = "Bad Earning",
                    ComponentType = SalaryComponentType.Fixed,
                    Amount = -500, // Negative
                    IsFixed = false,
                    IsTemp = false
                }
            },
            Deductions = new List<PayrollComponentDto>()
        };

        // Act & Assert — DTO validation rejects negative component amount
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _employeeAppService.UpdatePayrollDetailsAsync(created.Id, updateInput);
        });
    }

    [Fact]
    public async Task Should_Reject_Percentage_Component_Without_DependOn()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Payroll", "NoDep", email: "payroll.nodep@test.com", mobileNo: "9876543225");

        var updateInput = new UpdateEmployeePayrollDetailsDto
        {
            PayrollDetails = new PayrollDetailsDto
            {
                BasicSalary = 50000
            },
            Earnings = new List<PayrollComponentDto>
            {
                new PayrollComponentDto
                {
                    Name = "HRA No DependOn",
                    ComponentType = SalaryComponentType.Percentage,
                    Amount = 5000,
                    DependOn = null, // Missing DependOn for Percentage type
                    IsFixed = false,
                    IsTemp = false
                }
            },
            Deductions = new List<PayrollComponentDto>()
        };

        // Act & Assert — DTO validation rejects percentage without DependOn
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _employeeAppService.UpdatePayrollDetailsAsync(created.Id, updateInput);
        });
    }

    [Fact]
    public async Task Should_Reject_TDS_Over_100()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Payroll", "TDS", email: "payroll.tds@test.com", mobileNo: "9876543226");

        var updateInput = new UpdateEmployeePayrollDetailsDto
        {
            PayrollDetails = new PayrollDetailsDto
            {
                BasicSalary = 50000,
                TdsPercentage = 150 // Over 100
            },
            Earnings = new List<PayrollComponentDto>(),
            Deductions = new List<PayrollComponentDto>()
        };

        // Act & Assert — DTO validation rejects TDS > 100
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _employeeAppService.UpdatePayrollDetailsAsync(created.Id, updateInput);
        });
    }

    [Fact]
    public async Task Should_Accept_Zero_TDS()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Payroll", "ZeroTDS", email: "payroll.zerotds@test.com", mobileNo: "9876543227");

        var updateInput = new UpdateEmployeePayrollDetailsDto
        {
            PayrollDetails = new PayrollDetailsDto
            {
                BasicSalary = 50000,
                TdsPercentage = 0
            },
            Earnings = new List<PayrollComponentDto>(),
            Deductions = new List<PayrollComponentDto>()
        };

        // Act
        await _employeeAppService.UpdatePayrollDetailsAsync(created.Id, updateInput);

        // Assert
        var result = await _employeeAppService.GetPayrollDetailsAsync(created.Id);
        result.PayrollDetails.TdsPercentage.ShouldBe(0);
    }

    [Fact]
    public async Task Should_Update_Payroll_With_Multiple_Earnings_And_Deductions()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Payroll", "Multi", email: "payroll.multi@test.com", mobileNo: "9876543228");

        var updateInput = new UpdateEmployeePayrollDetailsDto
        {
            PayrollDetails = new PayrollDetailsDto
            {
                BasicSalary = 60000,
                TdsPercentage = 10
            },
            Earnings = new List<PayrollComponentDto>
            {
                new PayrollComponentDto { Name = "HRA", ComponentType = SalaryComponentType.Fixed, Amount = 15000, IsFixed = true, IsTemp = false },
                new PayrollComponentDto { Name = "DA", ComponentType = SalaryComponentType.Percentage, Amount = 3000, DependOn = SalaryComponentDependency.Basic, IsFixed = false, IsTemp = false },
                new PayrollComponentDto { Name = "Special", ComponentType = SalaryComponentType.Fixed, Amount = 5000, IsFixed = false, IsTemp = true }
            },
            Deductions = new List<PayrollComponentDto>
            {
                new PayrollComponentDto { Name = "PF", ComponentType = SalaryComponentType.Fixed, Amount = 7200, IsFixed = true, IsTemp = false },
                new PayrollComponentDto { Name = "Tax", ComponentType = SalaryComponentType.Percentage, Amount = 2000, DependOn = SalaryComponentDependency.Gross, IsFixed = false, IsTemp = false }
            }
        };

        // Act
        await _employeeAppService.UpdatePayrollDetailsAsync(created.Id, updateInput);

        // Assert
        var result = await _employeeAppService.GetPayrollDetailsAsync(created.Id);
        result.Earnings.Count.ShouldBe(3);
        result.Deductions.Count.ShouldBe(2);
        result.PayrollDetails.BasicSalary.ShouldBe(60000);
        result.PayrollDetails.TdsPercentage.ShouldBe(10);
    }

    #endregion

    #region Tier 2 — Employee Without Email/Mobile Edge Cases

    [Fact]
    public async Task Should_Reject_Whitespace_Only_Email()
    {
        // Arrange
        var roleId = await EnsureRoleExistsAsync();
        var input = new CreateEmployeeBasicInfoDto
        {
            FirstName = "Test",
            LastName = "Doe",
            MobileNo = "9876543210",
            Email = "   ", // Whitespace only
            DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
            DateOfJoining = DateOnly.FromDateTime(new DateTime(2023, 1, 1)),
            RoleId = roleId
        };

        // Act & Assert — app service checks IsNullOrWhiteSpace for email
        await Should.ThrowAsync<UserFriendlyException>(async () =>
        {
            await _employeeAppService.CreateBasicInfoAsync(input);
        });
    }

    #endregion

    #region Tier 4 — Full Employee Lifecycle

    [Fact]
    public async Task Should_Complete_Full_Employee_Lifecycle()
    {
        // Step 1: Create employee
        var created = await CreateTestEmployeeAsync("Lifecycle", "Employee",
            email: "lifecycle.employee@test.com", mobileNo: "9876543250");
        created.ShouldNotBeNull();
        created.EmployeeStatusType.ShouldBe(EmployeeStatusType.Active);
        var employeeId = created.Id;

        // Step 2: Update basic info
        var updateBasic = new UpdateEmployeeBasicInfoDto
        {
            FirstName = "LifecycleUpdated",
            LastName = "EmployeeUpdated",
            MobileNo = "8888888888",
            Email = "lifecycle.updated@test.com",
            DateOfBirth = DateOnly.FromDateTime(new DateTime(1992, 3, 15)),
            GenderType = GenderType.Female,
            DateOfJoining = DateOnly.FromDateTime(new DateTime(2023, 6, 1)),
            RoleId = created.RoleId
        };
        var updated = await _employeeAppService.UpdateBasicInfoAsync(employeeId, updateBasic);
        updated.FirstName.ShouldBe("LifecycleUpdated");
        updated.GenderType.ShouldBe(GenderType.Female);

        // Step 3: Update bank details
        var bankInput = new BankDetailsDto
        {
            BankName = "Lifecycle Bank",
            AccountNumber = "9876543210",
            IfscCode = "LIFE0001234",
            BranchName = "Main Branch"
        };
        var withBank = await _employeeAppService.UpdateBankDetails(employeeId, bankInput);
        withBank.BankDetails.BankName.ShouldBe("Lifecycle Bank");

        // Step 4: Update payroll details with earnings and deductions
        var payrollInput = new UpdateEmployeePayrollDetailsDto
        {
            PayrollDetails = new PayrollDetailsDto
            {
                BasicSalary = 75000,
                TdsPercentage = 15
            },
            Earnings = new List<PayrollComponentDto>
            {
                new PayrollComponentDto { Name = "HRA", ComponentType = SalaryComponentType.Fixed, Amount = 20000, IsFixed = true, IsTemp = false },
                new PayrollComponentDto { Name = "DA", ComponentType = SalaryComponentType.Percentage, Amount = 5000, DependOn = SalaryComponentDependency.Basic, IsFixed = false, IsTemp = false }
            },
            Deductions = new List<PayrollComponentDto>
            {
                new PayrollComponentDto { Name = "PF", ComponentType = SalaryComponentType.Fixed, Amount = 9000, IsFixed = true, IsTemp = false }
            }
        };
        await _employeeAppService.UpdatePayrollDetailsAsync(employeeId, payrollInput);
        var payroll = await _employeeAppService.GetPayrollDetailsAsync(employeeId);
        payroll.PayrollDetails.BasicSalary.ShouldBe(75000);
        payroll.Earnings.Count.ShouldBe(2);
        payroll.Deductions.Count.ShouldBe(1);

        // Step 5: Update additional details
        var additionalInput = new UpdateEmployeeAdditionalDetailsDto
        {
            PersonWithDisability = false,
            SecondLanguage = "Sanskrit",
            LibraryMember = true,
            TransportMember = true
        };
        var additional = await _employeeAppService.UpdateAdditionalDetailsAsync(employeeId, additionalInput);
        additional.SecondLanguage.ShouldBe("Sanskrit");
        additional.LibraryMember.ShouldBeTrue();

        // Step 6: Status transitions: Active → Inactive → Left → Active
        var inactive = await _employeeAppService.UpdateEmployeeStatus(employeeId, EmployeeStatusType.Inactive, "On leave");
        inactive.EmployeeStatusType.ShouldBe(EmployeeStatusType.Inactive);

        var left = await _employeeAppService.UpdateEmployeeStatus(employeeId, EmployeeStatusType.Left, "Resigned");
        left.EmployeeStatusType.ShouldBe(EmployeeStatusType.Left);

        var reactivated = await _employeeAppService.UpdateEmployeeStatus(employeeId, EmployeeStatusType.Active, "Rejoined");
        reactivated.EmployeeStatusType.ShouldBe(EmployeeStatusType.Active);

        // Step 7: Final verification — Get employee and verify all data persisted
        var finalState = await _employeeAppService.GetAsync(employeeId);
        finalState.FirstName.ShouldBe("LifecycleUpdated");
        finalState.LastName.ShouldBe("EmployeeUpdated");
        finalState.EmployeeStatusType.ShouldBe(EmployeeStatusType.Active);
    }

    [Fact]
    public async Task Should_Generate_Sequential_Usernames_For_Same_Name()
    {
        // Arrange & Act — create multiple employees with same first+last name
        var emp1 = await CreateTestEmployeeAsync("Samename", "User",
            email: "samename.user1@test.com", mobileNo: "9876543251");
        var emp2 = await CreateTestEmployeeAsync("Samename", "User",
            email: "samename.user2@test.com", mobileNo: "9876543252");
        var emp3 = await CreateTestEmployeeAsync("Samename", "User",
            email: "samename.user3@test.com", mobileNo: "9876543253");

        // Assert — all three should have unique employee codes
        emp1.EmployeeCode.ShouldNotBe(emp2.EmployeeCode);
        emp2.EmployeeCode.ShouldNotBe(emp3.EmployeeCode);
        emp1.Id.ShouldNotBe(emp2.Id);
        emp2.Id.ShouldNotBe(emp3.Id);
    }

    [Fact]
    public async Task Should_Handle_Payroll_Replacement_Across_Multiple_Updates()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("PayrollReplace", "Multi",
            email: "payroll.replace.multi@test.com", mobileNo: "9876543254");

        // Update 1: 3 earnings, 2 deductions
        var update1 = new UpdateEmployeePayrollDetailsDto
        {
            PayrollDetails = new PayrollDetailsDto { BasicSalary = 40000 },
            Earnings = new List<PayrollComponentDto>
            {
                new PayrollComponentDto { Name = "HRA", ComponentType = SalaryComponentType.Fixed, Amount = 10000, IsFixed = true, IsTemp = false },
                new PayrollComponentDto { Name = "DA", ComponentType = SalaryComponentType.Fixed, Amount = 5000, IsFixed = false, IsTemp = false },
                new PayrollComponentDto { Name = "Bonus", ComponentType = SalaryComponentType.Fixed, Amount = 3000, IsFixed = false, IsTemp = true }
            },
            Deductions = new List<PayrollComponentDto>
            {
                new PayrollComponentDto { Name = "PF", ComponentType = SalaryComponentType.Fixed, Amount = 4800, IsFixed = true, IsTemp = false },
                new PayrollComponentDto { Name = "ESI", ComponentType = SalaryComponentType.Fixed, Amount = 750, IsFixed = true, IsTemp = false }
            }
        };
        await _employeeAppService.UpdatePayrollDetailsAsync(created.Id, update1);
        var after1 = await _employeeAppService.GetPayrollDetailsAsync(created.Id);
        after1.Earnings.Count.ShouldBe(3);
        after1.Deductions.Count.ShouldBe(2);

        // Update 2: Reduce to 1 earning, 0 deductions — complete replacement
        var update2 = new UpdateEmployeePayrollDetailsDto
        {
            PayrollDetails = new PayrollDetailsDto { BasicSalary = 50000 },
            Earnings = new List<PayrollComponentDto>
            {
                new PayrollComponentDto { Name = "Consolidated", ComponentType = SalaryComponentType.Fixed, Amount = 50000, IsFixed = true, IsTemp = false }
            },
            Deductions = new List<PayrollComponentDto>()
        };
        await _employeeAppService.UpdatePayrollDetailsAsync(created.Id, update2);
        var after2 = await _employeeAppService.GetPayrollDetailsAsync(created.Id);
        after2.Earnings.Count.ShouldBe(1);
        after2.Earnings[0].Name.ShouldBe("Consolidated");
        after2.Deductions.Count.ShouldBe(0);
        after2.PayrollDetails.BasicSalary.ShouldBe(50000);

        // Update 3: Back to multiple — previous components should be gone
        var update3 = new UpdateEmployeePayrollDetailsDto
        {
            PayrollDetails = new PayrollDetailsDto { BasicSalary = 60000, TdsPercentage = 20 },
            Earnings = new List<PayrollComponentDto>
            {
                new PayrollComponentDto { Name = "NewHRA", ComponentType = SalaryComponentType.Fixed, Amount = 15000, IsFixed = true, IsTemp = false },
                new PayrollComponentDto { Name = "NewDA", ComponentType = SalaryComponentType.Percentage, Amount = 8000, DependOn = SalaryComponentDependency.Basic, IsFixed = false, IsTemp = false }
            },
            Deductions = new List<PayrollComponentDto>
            {
                new PayrollComponentDto { Name = "NewPF", ComponentType = SalaryComponentType.Fixed, Amount = 7200, IsFixed = true, IsTemp = false }
            }
        };
        await _employeeAppService.UpdatePayrollDetailsAsync(created.Id, update3);
        var after3 = await _employeeAppService.GetPayrollDetailsAsync(created.Id);
        after3.Earnings.Count.ShouldBe(2);
        after3.Deductions.Count.ShouldBe(1);
        after3.PayrollDetails.TdsPercentage.ShouldBe(20);
        // Verify old "Consolidated" component is gone
        after3.Earnings.ShouldNotContain(e => e.Name == "Consolidated");
    }

    [Fact]
    public async Task Should_Maintain_Employee_Code_After_Updates()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("CodeStable", "Employee",
            email: "code.stable@test.com", mobileNo: "9876543255");
        var originalCode = created.EmployeeCode;

        // Act — update various fields
        var updateInput = new UpdateEmployeeBasicInfoDto
        {
            FirstName = "CodeStableUpdated",
            LastName = "EmployeeUpdated",
            MobileNo = "7777777777",
            Email = "code.stable.updated@test.com",
            DateOfBirth = DateOnly.FromDateTime(new DateTime(1991, 1, 1)),
            GenderType = GenderType.Male,
            DateOfJoining = DateOnly.FromDateTime(new DateTime(2023, 6, 1)),
            RoleId = created.RoleId
        };
        var updated = await _employeeAppService.UpdateBasicInfoAsync(created.Id, updateInput);

        // Assert — employee code should not change after updates
        updated.EmployeeCode.ShouldBe(originalCode);
    }

    [Fact]
    public async Task Should_Handle_Additional_Details_Upsert_Idempotently()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Upsert", "AdditionalDetails",
            email: "upsert.additional@test.com", mobileNo: "9876543256");

        // Act 1 — first update creates the record
        var input1 = new UpdateEmployeeAdditionalDetailsDto
        {
            BplCardNo = "BPL-001",
            PersonWithDisability = true,
            SecondLanguage = "Hindi",
            LibraryMember = false,
            TransportMember = true
        };
        var result1 = await _employeeAppService.UpdateAdditionalDetailsAsync(created.Id, input1);
        result1.BplCardNo.ShouldBe("BPL-001");
        result1.PersonWithDisability.ShouldBeTrue();

        // Act 2 — second update modifies the existing record
        var input2 = new UpdateEmployeeAdditionalDetailsDto
        {
            BplCardNo = "BPL-002",
            PersonWithDisability = false,
            SecondLanguage = "Gujarati",
            LibraryMember = true,
            TransportMember = false
        };
        var result2 = await _employeeAppService.UpdateAdditionalDetailsAsync(created.Id, input2);
        result2.BplCardNo.ShouldBe("BPL-002");
        result2.PersonWithDisability.ShouldBeFalse();
        result2.SecondLanguage.ShouldBe("Gujarati");
        result2.LibraryMember.ShouldBeTrue();
        result2.TransportMember.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Filter_Employees_By_Status_After_Status_Change()
    {
        // Arrange — create employees and change some statuses
        var active1 = await CreateTestEmployeeAsync("StatusFilter", "Active1",
            email: "statusfilter.active1@test.com", mobileNo: "9876543257");
        var active2 = await CreateTestEmployeeAsync("StatusFilter", "Active2",
            email: "statusfilter.active2@test.com", mobileNo: "9876543258");
        var willLeave = await CreateTestEmployeeAsync("StatusFilter", "WillLeave",
            email: "statusfilter.willleave@test.com", mobileNo: "9876543259");

        await _employeeAppService.UpdateEmployeeStatus(willLeave.Id, EmployeeStatusType.Left, "Left the company");

        // Act — filter by Active status
        var activeFilter = new GetEmployeeListInput
        {
            Status = nameof(EmployeeStatusType.Active),
            Search = "statusfilter",
            SkipCount = 0,
            MaxResultCount = 50
        };
        var activeResult = await _employeeAppService.GetFilteredListAsync(activeFilter);

        // Assert
        activeResult.Items.ShouldContain(x => x.Id == active1.Id);
        activeResult.Items.ShouldContain(x => x.Id == active2.Id);
        activeResult.Items.ShouldNotContain(x => x.Id == willLeave.Id);

        // Also verify Left filter returns the correct employee
        var leftFilter = new GetEmployeeListInput
        {
            Status = nameof(EmployeeStatusType.Left),
            Search = "statusfilter",
            SkipCount = 0,
            MaxResultCount = 50
        };
        var leftResult = await _employeeAppService.GetFilteredListAsync(leftFilter);
        leftResult.Items.ShouldContain(x => x.Id == willLeave.Id);
        leftResult.Items.ShouldNotContain(x => x.Id == active1.Id);
    }

    [Fact]
    public async Task Should_Delete_Employee_And_Reject_Subsequent_Operations()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("DeleteOps", "Employee",
            email: "delete.ops@test.com", mobileNo: "9876543260");

        // Act — delete the employee
        await _employeeAppService.DeleteAsync(created.Id);

        // Assert — subsequent Get should throw EntityNotFoundException
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _employeeAppService.GetAsync(created.Id);
        });

        // Payroll operations should also fail
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _employeeAppService.GetPayrollDetailsAsync(created.Id);
        });
    }

    [Fact]
    public async Task Should_Handle_Concurrent_BasicInfo_Updates()
    {
        // Arrange
        var created = await CreateTestEmployeeAsync("Concurrent", "Update",
            email: "concurrent.update@test.com", mobileNo: "9876543261");

        // Act — perform multiple sequential updates (simulating concurrent access)
        for (int i = 1; i <= 5; i++)
        {
            var updateInput = new UpdateEmployeeBasicInfoDto
            {
                FirstName = $"Iteration{i}",
                LastName = "Update",
                MobileNo = "8888888888",
                Email = "concurrent.update@test.com",
                DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
                GenderType = GenderType.Male,
                DateOfJoining = DateOnly.FromDateTime(new DateTime(2023, 1, 1)),
                RoleId = created.RoleId
            };
            await _employeeAppService.UpdateBasicInfoAsync(created.Id, updateInput);
        }

        // Assert — final state should reflect the last update
        var finalState = await _employeeAppService.GetAsync(created.Id);
        finalState.FirstName.ShouldBe("Iteration5");
    }

    [Fact]
    public async Task Should_Maintain_Payroll_Details_After_BasicInfo_Update()
    {
        // Arrange — create employee with payroll
        var created = await CreateTestEmployeeAsync("PayrollPersist", "Test",
            email: "payroll.persist@test.com", mobileNo: "9876543262");

        var payrollInput = new UpdateEmployeePayrollDetailsDto
        {
            PayrollDetails = new PayrollDetailsDto { BasicSalary = 45000, TdsPercentage = 12 },
            Earnings = new List<PayrollComponentDto>
            {
                new PayrollComponentDto { Name = "HRA", ComponentType = SalaryComponentType.Fixed, Amount = 12000, IsFixed = true, IsTemp = false }
            },
            Deductions = new List<PayrollComponentDto>()
        };
        await _employeeAppService.UpdatePayrollDetailsAsync(created.Id, payrollInput);

        // Act — update basic info (should NOT affect payroll)
        var basicUpdate = new UpdateEmployeeBasicInfoDto
        {
            FirstName = "PayrollPersistUpdated",
            LastName = "TestUpdated",
            MobileNo = "7777777777",
            Email = "payroll.persist.updated@test.com",
            DateOfBirth = DateOnly.FromDateTime(new DateTime(1990, 1, 1)),
            GenderType = GenderType.Female,
            DateOfJoining = DateOnly.FromDateTime(new DateTime(2023, 1, 1)),
            RoleId = created.RoleId
        };
        await _employeeAppService.UpdateBasicInfoAsync(created.Id, basicUpdate);

        // Assert — payroll should remain unchanged
        var payrollAfter = await _employeeAppService.GetPayrollDetailsAsync(created.Id);
        payrollAfter.PayrollDetails.BasicSalary.ShouldBe(45000);
        payrollAfter.PayrollDetails.TdsPercentage.ShouldBe(12);
        payrollAfter.Earnings.Count.ShouldBe(1);
        payrollAfter.Earnings[0].Name.ShouldBe("HRA");
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

