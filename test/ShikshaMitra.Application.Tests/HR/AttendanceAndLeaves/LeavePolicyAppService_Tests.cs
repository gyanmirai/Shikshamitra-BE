using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

/// <summary>
/// End-to-End tests for Leave Policy App Service
/// </summary>
public class LeavePolicyAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly ILeavePolicyAppService _leavePolicyAppService;
    private readonly ILeaveTypeAppService _leaveTypeAppService;
    private static readonly Guid TestMediumStageId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public LeavePolicyAppService_Tests()
    {
        _leavePolicyAppService = GetRequiredService<ILeavePolicyAppService>();
        _leaveTypeAppService = GetRequiredService<ILeaveTypeAppService>();

        // Seed required FK entities: AcademicYear → EducationBoard → EducationMedium → EducationMediumStage
        SeedRequiredEntities();
    }

    private void SeedRequiredEntities()
    {
        WithUnitOfWorkAsync(async () =>
        {
            var dbContext = ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();

            var academicYearId = Guid.Parse("00000000-0000-0000-0000-000000000010");
            var educationBoardId = Guid.Parse("00000000-0000-0000-0000-000000000011");
            var educationMediumId = Guid.Parse("00000000-0000-0000-0000-000000000012");
            var now = DateTime.UtcNow.ToString("o");
            var stamp = Guid.NewGuid().ToString();

            // Insert AcademicYear (root entity — no FK dependencies)
            await dbContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO AppAcademicYears (Id, Name, StartDate, EndDate, IsDeleted, ConcurrencyStamp, ExtraProperties, CreationTime) VALUES ({0}, {1}, {2}, {3}, 0, {4}, {5}, {6})",
                academicYearId, "Test Year 2024", now, DateTime.UtcNow.AddYears(1).ToString("o"), stamp, "{}", now);

            // Insert EducationBoard (FK → AcademicYear)
            await dbContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO AppEducationBoards (Id, Type, AcademicYearId, IsDeleted, ConcurrencyStamp, ExtraProperties, CreationTime) VALUES ({0}, {1}, {2}, 0, {3}, {4}, {5})",
                educationBoardId, 0, academicYearId, stamp, "{}", now);

            // Insert EducationMedium (FK → EducationBoard)
            await dbContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO AppEducationMediums (Id, Name, EducationBoardId, IsDeleted, ConcurrencyStamp, ExtraProperties, CreationTime) VALUES ({0}, {1}, {2}, 0, {3}, {4}, {5})",
                educationMediumId, "Test Medium", educationBoardId, stamp, "{}", now);

            // Insert EducationMediumStage (FK → EducationMedium) — this is the TestMediumStageId
            await dbContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO AppEducationMediumStages (Id, Name, EducationMediumId, IsDeleted, ConcurrencyStamp, ExtraProperties, CreationTime) VALUES ({0}, {1}, {2}, 0, {3}, {4}, {5})",
                TestMediumStageId, "Test Stage", educationMediumId, stamp, "{}", now);
        }).GetAwaiter().GetResult();
    }

    #region Create Tests

    [Fact]
    public async Task Should_Create_Leave_Policy()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Annual Leave");

        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Test Leave Policy",
            Description = "Standard leave policy for all employees",
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 20,
            PaidType = "Paid",
            CarryForward = true,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act
        var result = await _leavePolicyAppService.CreateLeavePolicyAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldNotBeNullOrEmpty();
        result.PolicyTitle.ShouldBe(input.PolicyTitle);
        result.Description.ShouldBe(input.Description);
        result.LeaveType.ShouldBe(leaveType.Id);
        result.MaxLeavePerYear.ShouldBe(20);
        result.CarryForward.ShouldBeTrue();
        result.EncashmentAllowed.ShouldBeFalse();
        result.RequiresApproval.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Create_Policy_Without_CarryForward()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Sick Leave");

        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "No Carry Forward Policy",
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 10,
            PaidType = "Paid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act
        var result = await _leavePolicyAppService.CreateLeavePolicyAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.CarryForward.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Not_Create_Duplicate_PolicyName()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Test Leave");
        var policyName = "Duplicate Policy Test";

        var input1 = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = policyName,
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 15,
            PaidType = "Paid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        await _leavePolicyAppService.CreateLeavePolicyAsync(input1);

        var input2 = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = policyName,
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 10,
            PaidType = "Unpaid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act & Assert
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _leavePolicyAppService.CreateLeavePolicyAsync(input2);
        });
    }

    [Fact]
    public async Task Should_Not_Create_Without_Required_Fields()
    {
        // Arrange — CreateAsync is deprecated and throws UserFriendlyException
        var input = new CreateLeavePolicyDto
        {
            PolicyName = "", // Empty name
            LeaveType = "Some Leave",
            MaximumDaysPerYear = 10,
            PaidType = LeavePaidType.Paid
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _leavePolicyAppService.CreateAsync(input);
        });
    }

    [Fact]
    public async Task Should_Not_Create_With_Negative_MaxDays()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Test Leave");

        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Negative Days Policy",
            LeaveType = leaveType.Id,
            MaxLeavePerYear = -5, // Invalid
            PaidType = "Paid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act & Assert — [Range(0, 365)] DataAnnotation on MaxLeavePerYear triggers ABP validation
        await Should.ThrowAsync<Volo.Abp.Validation.AbpValidationException>(async () =>
        {
            await _leavePolicyAppService.CreateLeavePolicyAsync(input);
        });
    }

    [Fact]
    public async Task Should_Trim_Whitespace_From_PolicyName()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Test Leave");

        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "  Test Policy With Spaces  ",
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 10,
            PaidType = "Paid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act
        var result = await _leavePolicyAppService.CreateLeavePolicyAsync(input);

        // Assert
        result.PolicyTitle.ShouldBe("Test Policy With Spaces");
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task Should_Get_Leave_Policy_By_Id()
    {
        // Arrange
        var created = await CreateTestLeavePolicyAsync("Get By ID Policy");

        // Act
        var result = await _leavePolicyAppService.GetAsync(Guid.Parse(created.Id));

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(Guid.Parse(created.Id));
        result.PolicyName.ShouldBe(created.PolicyTitle);
    }

    [Fact]
    public async Task Should_Throw_Exception_When_Policy_Not_Found()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _leavePolicyAppService.GetAsync(nonExistentId);
        });
    }

    [Fact]
    public async Task Should_Get_All_Active_Policies()
    {
        // Arrange
        await CreateTestLeavePolicyAsync("Active Policy 1");
        await CreateTestLeavePolicyAsync("Active Policy 2");
        await CreateTestLeavePolicyAsync("Active Policy 3");

        var input = new PagedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        };

        // Act
        var result = await _leavePolicyAppService.GetAllActiveAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Should_Get_Paginated_List()
    {
        // Arrange
        for (int i = 1; i <= 15; i++)
        {
            await CreateTestLeavePolicyAsync($"Paginated Policy {i}");
        }

        var input = new PagedAndSortedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 10
        };

        // Act
        var result = await _leavePolicyAppService.GetListAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeLessThanOrEqualTo(10);
        result.TotalCount.ShouldBeGreaterThanOrEqualTo(15);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Should_Update_Leave_Policy()
    {
        // Arrange
        var created = await CreateTestLeavePolicyAsync("Original Policy");
        var newLeaveType = await CreateTestLeaveTypeAsync("New Leave Type");

        var updateInput = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Updated Policy",
            Description = "Updated description",
            LeaveType = newLeaveType.Id,
            MaxLeavePerYear = 25,
            PaidType = "Unpaid",
            CarryForward = false,
            EncashmentAllowed = true,
            RequiresApproval = true
        };

        // Act
        var result = await _leavePolicyAppService.UpdateLeavePolicyAsync(Guid.Parse(created.Id), updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.PolicyTitle.ShouldBe("Updated Policy");
        result.Description.ShouldBe("Updated description");
        result.MaxLeavePerYear.ShouldBe(25);
        result.CarryForward.ShouldBeFalse();
        result.EncashmentAllowed.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Not_Update_To_Duplicate_PolicyName()
    {
        // Arrange
        var policy1 = await CreateTestLeavePolicyAsync("Policy 1");
        var policy2 = await CreateTestLeavePolicyAsync("Policy 2");

        var updateInput = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Policy 1", // Trying to use existing name
            LeaveType = Guid.Parse(policy2.LeaveType.ToString()),
            MaxLeavePerYear = 10,
            PaidType = "Paid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act & Assert
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _leavePolicyAppService.UpdateLeavePolicyAsync(Guid.Parse(policy2.Id), updateInput);
        });
    }

    [Fact]
    public async Task Should_Allow_Update_With_Same_PolicyName()
    {
        // Arrange
        var created = await CreateTestLeavePolicyAsync("Same Name Policy");

        var updateInput = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Same Name Policy", // Same name
            Description = "Updated description only",
            LeaveType = created.LeaveType,
            MaxLeavePerYear = created.MaxLeavePerYear,
            PaidType = "Paid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act
        var result = await _leavePolicyAppService.UpdateLeavePolicyAsync(Guid.Parse(created.Id), updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.PolicyTitle.ShouldBe("Same Name Policy");
        result.Description.ShouldBe("Updated description only");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Should_Delete_Leave_Policy()
    {
        // Arrange
        var created = await CreateTestLeavePolicyAsync("Policy to Delete");

        // Act — DeleteAsync performs soft-delete (deactivation)
        await _leavePolicyAppService.DeleteAsync(Guid.Parse(created.Id));

        // Assert — entity still exists but is deactivated
        var result = await _leavePolicyAppService.GetAsync(Guid.Parse(created.Id));
        result.ShouldNotBeNull();
        result.IsActive.ShouldBeFalse();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Should_Handle_Special_Characters_In_PolicyName()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Test Leave");

        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Policy & Rules (2024) - Test",
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 10,
            PaidType = "Paid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act
        var result = await _leavePolicyAppService.CreateLeavePolicyAsync(input);

        // Assert
        result.PolicyTitle.ShouldBe(input.PolicyTitle);
    }

    [Fact]
    public async Task Should_Handle_Large_MaxDays()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Extended Leave");

        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Long Duration Policy",
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 365,
            PaidType = "Unpaid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act
        var result = await _leavePolicyAppService.CreateLeavePolicyAsync(input);

        // Assert
        result.MaxLeavePerYear.ShouldBe(365);
    }

    [Fact]
    public async Task Should_Handle_Empty_Description()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Test Leave");

        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "No Description Policy",
            Description = null,
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 10,
            PaidType = "Paid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act
        var result = await _leavePolicyAppService.CreateLeavePolicyAsync(input);

        // Assert
        result.Description.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Create_With_All_PaidTypes()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Test Leave");

        // Act
        var paidPolicy = await CreateTestLeavePolicyAsync("Paid Policy", leaveType.Id, "Paid");
        var unpaidPolicy = await CreateTestLeavePolicyAsync("Unpaid Policy", leaveType.Id, "Unpaid");

        // Assert
        paidPolicy.PaidType.ShouldBe("Paid");
        unpaidPolicy.PaidType.ShouldBe("Unpaid");
    }

    #endregion

    #region Tier 2 — Boundary & Business Rule Validation

    [Fact]
    public async Task Should_Accept_MaxDays_At_Minimum_Boundary()
    {
        // Arrange — MinMaximumDaysPerYear = 1
        var leaveType = await CreateTestLeaveTypeAsync("Min Days Leave");

        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Min Days Policy",
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 1,
            PaidType = "Paid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act
        var result = await _leavePolicyAppService.CreateLeavePolicyAsync(input);

        // Assert
        result.MaxLeavePerYear.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Reject_MaxDays_Zero()
    {
        // Arrange — 0 passes DTO [Range(0, 365)] but fails domain SetMaximumDaysPerYear (min=1)
        var leaveType = await CreateTestLeaveTypeAsync("Zero Days Leave");

        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Zero Days Policy",
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 0,
            PaidType = "Paid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act & Assert — domain SetMaximumDaysPerYear throws BusinessException for value < 1
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _leavePolicyAppService.CreateLeavePolicyAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_MaxDays_Over_365()
    {
        // Arrange — 366 exceeds DTO [Range(0, 365)]
        var leaveType = await CreateTestLeaveTypeAsync("Over Max Leave");

        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Over Max Days Policy",
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 366,
            PaidType = "Paid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act & Assert — DTO Range attribute triggers ABP validation
        await Should.ThrowAsync<Volo.Abp.Validation.AbpValidationException>(async () =>
        {
            await _leavePolicyAppService.CreateLeavePolicyAsync(input);
        });
    }

    [Fact]
    public async Task Should_Reject_Inactive_LeaveType()
    {
        // Arrange — create and then deactivate a leave type
        var leaveType = await CreateTestLeaveTypeAsync("Inactive Leave Type");
        await _leaveTypeAppService.DeleteAsync(leaveType.Id); // soft-delete → deactivates

        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Policy With Inactive Type",
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 10,
            PaidType = "Paid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act & Assert — LeavePolicyManager.ValidateLeaveTypeExistsAsync rejects inactive
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _leavePolicyAppService.CreateLeavePolicyAsync(input);
        });
    }

    [Fact]
    public async Task Should_Create_Policy_With_CarryForward_And_Encashment()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("Both Flags Leave");

        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "CarryForward And Encashment Policy",
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 20,
            PaidType = "Paid",
            CarryForward = true,
            EncashmentAllowed = true,
            RequiresApproval = true
        };

        // Act
        var result = await _leavePolicyAppService.CreateLeavePolicyAsync(input);

        // Assert
        result.CarryForward.ShouldBeTrue();
        result.EncashmentAllowed.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Create_Policy_Without_Approval()
    {
        // Arrange
        var leaveType = await CreateTestLeaveTypeAsync("No Approval Leave");

        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "No Approval Policy",
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 5,
            PaidType = "Unpaid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = false
        };

        // Act
        var result = await _leavePolicyAppService.CreateLeavePolicyAsync(input);

        // Assert
        result.RequiresApproval.ShouldBeFalse();
        result.PaidType.ShouldBe("Unpaid");
    }

    [Fact]
    public async Task Should_Reject_Inactive_LeaveType_On_Update()
    {
        // Arrange — create a valid policy first
        var activeLeaveType = await CreateTestLeaveTypeAsync("Active For Update");
        var policy = await CreateTestLeavePolicyAsync("Update Inactive Type Policy", activeLeaveType.Id);

        // Now create and deactivate another leave type
        var inactiveLeaveType = await CreateTestLeaveTypeAsync("Will Be Inactive");
        await _leaveTypeAppService.DeleteAsync(inactiveLeaveType.Id);

        var updateInput = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Update Inactive Type Policy",
            LeaveType = inactiveLeaveType.Id, // Switching to inactive type
            MaxLeavePerYear = 10,
            PaidType = "Paid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act & Assert
        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _leavePolicyAppService.UpdateLeavePolicyAsync(Guid.Parse(policy.Id), updateInput);
        });
    }

    [Fact]
    public async Task Should_Accept_MaxDays_At_Maximum_Boundary()
    {
        // Arrange — MaxMaximumDaysPerYear = 365 (already tested above but verifying domain acceptance)
        var leaveType = await CreateTestLeaveTypeAsync("Max Days Leave");

        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Max Days Boundary Policy",
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 365,
            PaidType = "Paid",
            CarryForward = true,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        // Act
        var result = await _leavePolicyAppService.CreateLeavePolicyAsync(input);

        // Assert
        result.MaxLeavePerYear.ShouldBe(365);
    }

    #endregion

    #region Tier 4 — Cross-Entity Lifecycle

    [Fact]
    public async Task Should_Block_Policy_Creation_After_LeaveType_Deactivation()
    {
        // Arrange — create a leave type, then deactivate it
        var leaveType = await CreateTestLeaveTypeAsync("Will Deactivate Type");
        await _leaveTypeAppService.DeleteAsync(leaveType.Id); // soft-delete → deactivates

        // Act & Assert — creating a policy with deactivated type should fail
        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Policy With Deactivated Type",
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 10,
            PaidType = "Paid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _leavePolicyAppService.CreateLeavePolicyAsync(input);
        });
    }

    [Fact]
    public async Task Should_Keep_Existing_Policy_Accessible_After_LeaveType_Deactivation()
    {
        // Arrange — create a valid policy with an active type
        var leaveType = await CreateTestLeaveTypeAsync("Type For Existing Policy");
        var policy = await CreateTestLeavePolicyAsync("Existing Policy Before Deactivation", leaveType.Id);

        // Act — deactivate the leave type AFTER policy was created
        await _leaveTypeAppService.DeleteAsync(leaveType.Id);

        // Assert — the existing policy should still be retrievable by ID
        var retrieved = await _leavePolicyAppService.GetAsync(Guid.Parse(policy.Id));
        retrieved.ShouldNotBeNull();
        retrieved.PolicyName.ShouldBe("Existing Policy Before Deactivation");
    }

    [Fact]
    public async Task Should_Exclude_Deactivated_Policy_From_Active_List()
    {
        // Arrange — create policies then deactivate one
        var policy1 = await CreateTestLeavePolicyAsync("Active List Policy 1");
        var policy2 = await CreateTestLeavePolicyAsync("Active List Policy 2");
        var policyToDeactivate = await CreateTestLeavePolicyAsync("Will Be Deactivated Policy");

        // Act — soft-delete (deactivate) one policy
        await _leavePolicyAppService.DeleteAsync(Guid.Parse(policyToDeactivate.Id));

        // Assert — GetAllActiveAsync should exclude the deactivated policy
        var activeResult = await _leavePolicyAppService.GetAllActiveAsync(new PagedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });

        activeResult.Items.ShouldContain(x => x.Id == Guid.Parse(policy1.Id));
        activeResult.Items.ShouldContain(x => x.Id == Guid.Parse(policy2.Id));
        activeResult.Items.ShouldNotContain(x => x.Id == Guid.Parse(policyToDeactivate.Id));
    }

    [Fact]
    public async Task Should_Allow_Update_With_Different_Active_LeaveType()
    {
        // Arrange — create policy with one type, then switch to another active type
        var type1 = await CreateTestLeaveTypeAsync("Original Type For Swap");
        var type2 = await CreateTestLeaveTypeAsync("New Type For Swap");
        var policy = await CreateTestLeavePolicyAsync("Swap LeaveType Policy", type1.Id);

        // Act — update policy to use the second leave type
        var updateInput = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Swap LeaveType Policy",
            LeaveType = type2.Id,
            MaxLeavePerYear = 20,
            PaidType = "Paid",
            CarryForward = true,
            EncashmentAllowed = false,
            RequiresApproval = true
        };
        var updated = await _leavePolicyAppService.UpdateLeavePolicyAsync(Guid.Parse(policy.Id), updateInput);

        // Assert
        updated.LeaveType.ShouldBe(type2.Id);
        updated.CarryForward.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Block_Update_When_Switching_To_Inactive_LeaveType()
    {
        // Arrange — create policy with active type, then try switching to an inactive one
        var activeType = await CreateTestLeaveTypeAsync("Active Type For Block");
        var inactiveType = await CreateTestLeaveTypeAsync("Inactive Type For Block");
        var policy = await CreateTestLeavePolicyAsync("Block Switch Policy", activeType.Id);

        // Deactivate the target type
        await _leaveTypeAppService.DeleteAsync(inactiveType.Id);

        // Act & Assert — update should fail when switching to inactive type
        var updateInput = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Block Switch Policy",
            LeaveType = inactiveType.Id,
            MaxLeavePerYear = 10,
            PaidType = "Paid",
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _leavePolicyAppService.UpdateLeavePolicyAsync(Guid.Parse(policy.Id), updateInput);
        });
    }

    [Fact]
    public async Task Should_Create_Multiple_Policies_With_Same_LeaveType()
    {
        // Arrange — one leave type can be used by multiple policies
        var sharedType = await CreateTestLeaveTypeAsync("Shared Leave Type");

        // Act
        var policy1 = await CreateTestLeavePolicyAsync("Shared Type Policy 1", sharedType.Id);
        var policy2 = await CreateTestLeavePolicyAsync("Shared Type Policy 2", sharedType.Id);

        // Assert — both policies created successfully with same leave type
        policy1.LeaveType.ShouldBe(sharedType.Id);
        policy2.LeaveType.ShouldBe(sharedType.Id);
        policy1.Id.ShouldNotBe(policy2.Id);
    }

    [Fact]
    public async Task Should_Deactivate_And_Verify_GetListAsync_Still_Returns_All()
    {
        // Arrange — GetListAsync returns all policies (active + inactive)
        var policy = await CreateTestLeavePolicyAsync("Deactivate List Check Policy");
        await _leavePolicyAppService.DeleteAsync(Guid.Parse(policy.Id));

        // Act
        var allPolicies = await _leavePolicyAppService.GetListAsync(new PagedAndSortedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });

        // Assert — deactivated policy should still appear in GetListAsync (not filtered)
        allPolicies.Items.ShouldContain(x => x.Id == Guid.Parse(policy.Id));
        var found = allPolicies.Items.First(x => x.Id == Guid.Parse(policy.Id));
        found.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Handle_Full_Policy_Lifecycle()
    {
        // Step 1: Create leave type
        var leaveType = await CreateTestLeaveTypeAsync("Lifecycle Leave Type");

        // Step 2: Create policy
        var policy = await CreateTestLeavePolicyAsync("Full Lifecycle Policy", leaveType.Id, "Paid", 20);
        policy.MaxLeavePerYear.ShouldBe(20);
        policy.PaidType.ShouldBe("Paid");

        // Step 3: Update policy — change max days, paid type, flags
        var updateInput = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = "Full Lifecycle Policy Updated",
            LeaveType = leaveType.Id,
            MaxLeavePerYear = 30,
            PaidType = "Unpaid",
            CarryForward = true,
            EncashmentAllowed = true,
            RequiresApproval = false
        };
        var updated = await _leavePolicyAppService.UpdateLeavePolicyAsync(Guid.Parse(policy.Id), updateInput);
        updated.PolicyTitle.ShouldBe("Full Lifecycle Policy Updated");
        updated.MaxLeavePerYear.ShouldBe(30);
        updated.PaidType.ShouldBe("Unpaid");
        updated.CarryForward.ShouldBeTrue();
        updated.EncashmentAllowed.ShouldBeTrue();
        updated.RequiresApproval.ShouldBeFalse();

        // Step 4: Deactivate policy
        await _leavePolicyAppService.DeleteAsync(Guid.Parse(policy.Id));
        var deactivated = await _leavePolicyAppService.GetAsync(Guid.Parse(policy.Id));
        deactivated.IsActive.ShouldBeFalse();

        // Step 5: Verify excluded from active list
        var activeList = await _leavePolicyAppService.GetAllActiveAsync(new PagedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 100
        });
        activeList.Items.ShouldNotContain(x => x.Id == Guid.Parse(policy.Id));
    }

    #endregion

    #region Helper Methods

    private async Task<LeavePolicyResponse> CreateTestLeavePolicyAsync(
        string policyName,
        Guid? leaveTypeId = null,
        string paidType = "Paid",
        int maxLeavePerYear = 15)
    {
        // Create a leave type if not provided
        if (!leaveTypeId.HasValue)
        {
            var leaveType = await CreateTestLeaveTypeAsync($"Leave Type for {policyName}");
            leaveTypeId = leaveType.Id;
        }

        var input = new LeavePolicyCreateRequest
        {
            MediumStageId = TestMediumStageId,
            PolicyTitle = policyName,
            Description = $"Test description for {policyName}",
            LeaveType = leaveTypeId.Value,
            MaxLeavePerYear = maxLeavePerYear,
            PaidType = paidType,
            CarryForward = false,
            EncashmentAllowed = false,
            RequiresApproval = true
        };

        return await _leavePolicyAppService.CreateLeavePolicyAsync(input);
    }

    private async Task<LeaveTypeResponse> CreateTestLeaveTypeAsync(string type)
    {
        var input = new LeaveTypeCreateRequest
        {
            MediumStageId = TestMediumStageId,
            Name = type,
            Color = "#FF5733"
        };

        return await _leaveTypeAppService.CreateLeaveTypeAsync(input);
    }

    #endregion
}
