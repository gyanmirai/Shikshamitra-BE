using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Security.UserScope;

public class UserScopeAppService_BulkAssign_Tests
    : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private static readonly Guid TestTenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly IUserScopeAppService _userScopeAppService;
    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly EmployeeManager _employeeManager;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IRepository<EmployeeOrganizationAssignment, Guid> _employeeOrganizationAssignmentRepository;
    private readonly AcademicYearManager _academicYearManager;
    private readonly EducationBoardManager _boardManager;
    private readonly EducationMediumManager _mediumManager;
    private readonly EducationMediumStageManager _stageManager;
    private readonly StandardManager _standardManager;
    private readonly DivisionManager _divisionManager;
    private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
    private readonly IRepository<EducationBoard, Guid> _boardRepository;
    private readonly IRepository<EducationMedium, Guid> _mediumRepository;
    private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
    private readonly IRepository<Standard, Guid> _standardRepository;
    private readonly IRepository<Division, Guid> _divisionRepository;
    private readonly IRepository<UserScopeAssignment, Guid> _scopeRepository;
    private readonly ICurrentTenant _currentTenant;

    public UserScopeAppService_BulkAssign_Tests()
    {
        _userScopeAppService = GetRequiredService<IUserScopeAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _roleManager = GetRequiredService<IdentityRoleManager>();
        _employeeManager = GetRequiredService<EmployeeManager>();
        _employeeRepository = GetRequiredService<IEmployeeRepository>();
        _employeeOrganizationAssignmentRepository = GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
        _academicYearManager = GetRequiredService<AcademicYearManager>();
        _boardManager = GetRequiredService<EducationBoardManager>();
        _mediumManager = GetRequiredService<EducationMediumManager>();
        _stageManager = GetRequiredService<EducationMediumStageManager>();
        _standardManager = GetRequiredService<StandardManager>();
        _divisionManager = GetRequiredService<DivisionManager>();
        _academicYearRepository = GetRequiredService<IRepository<AcademicYear, Guid>>();
        _boardRepository = GetRequiredService<IRepository<EducationBoard, Guid>>();
        _mediumRepository = GetRequiredService<IRepository<EducationMedium, Guid>>();
        _stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
        _standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
        _divisionRepository = GetRequiredService<IRepository<Division, Guid>>();
        _scopeRepository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Preserve_Other_Academic_Years()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var yearA = await CreateAcademicYearWithBoardsAsync();
            var yearB = await CreateAcademicYearWithBoardsAsync();

            await BulkAssignBoardAsync(user.Id, yearA.AcademicYearId, yearA.CbseBoardId);
            await BulkAssignBoardAsync(user.Id, yearB.AcademicYearId, yearB.GsebBoardId);

            var assignments = await GetUserAssignmentsAsync(user.Id);
            assignments.Count.ShouldBe(2);
            assignments.ShouldContain(x =>
                x.AcademicYearId == yearA.AcademicYearId &&
                x.EducationBoardId == yearA.CbseBoardId);
            assignments.ShouldContain(x =>
                x.AcademicYearId == yearB.AcademicYearId &&
                x.EducationBoardId == yearB.GsebBoardId);

            var yearAHierarchy = await WithUnitOfWorkAsync(() =>
                _userScopeAppService.GetUserScopeHierarchyAsync(user.Id, yearA.AcademicYearId));
            var yearANode = yearAHierarchy.AccessibleHierarchy.Single();
            yearANode.AcademicYearId.ShouldBe(yearA.AcademicYearId);
            yearANode.Boards.Single(x => x.BoardId == yearA.CbseBoardId).HasFullMediumAccess.ShouldBeTrue();
            yearANode.Boards.Single(x => x.BoardId == yearA.GsebBoardId).HasFullMediumAccess.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Replace_Only_Target_Academic_Year()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var yearA = await CreateAcademicYearWithBoardsAsync();
            var yearB = await CreateAcademicYearWithBoardsAsync();

            await BulkAssignBoardAsync(user.Id, yearA.AcademicYearId, yearA.CbseBoardId);
            await BulkAssignBoardAsync(user.Id, yearB.AcademicYearId, yearB.GsebBoardId);
            await BulkAssignBoardAsync(user.Id, yearA.AcademicYearId, yearA.GsebBoardId);

            var assignments = await GetUserAssignmentsAsync(user.Id);
            assignments.Count.ShouldBe(2);
            assignments.ShouldNotContain(x =>
                x.AcademicYearId == yearA.AcademicYearId &&
                x.EducationBoardId == yearA.CbseBoardId);
            assignments.ShouldContain(x =>
                x.AcademicYearId == yearA.AcademicYearId &&
                x.EducationBoardId == yearA.GsebBoardId);
            assignments.ShouldContain(x =>
                x.AcademicYearId == yearB.AcademicYearId &&
                x.EducationBoardId == yearB.GsebBoardId);
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Save_Mixed_Valid_Path_Rows()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var cbse = await CreateAcademicHierarchyAsync();
            var gseb = await CreateGsebHierarchyBranchAsync(cbse.AcademicYearId);

            var result = await WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
            {
                UserIds = [user.Id],
                AcademicYearId = cbse.AcademicYearId,
                Scopes =
                [
                    new BulkScopeAssignmentScopeDto
                    {
                        EducationBoardId = cbse.BoardId,
                        EducationMediumId = cbse.MediumId
                    },
                    new BulkScopeAssignmentScopeDto
                    {
                        EducationBoardId = gseb.BoardId,
                        EducationMediumId = gseb.MediumId,
                        EducationMediumStageId = gseb.StageId
                    }
                ]
            }));

            result.AssignmentsCreated.ShouldBe(2);
            result.TotalCombinations.ShouldBe(2);

            var assignments = await GetUserAssignmentsAsync(user.Id);
            assignments.Count.ShouldBe(2);
            assignments.ShouldContain(x =>
                x.AcademicYearId == cbse.AcademicYearId &&
                x.EducationBoardId == cbse.BoardId &&
                x.EducationMediumId == cbse.MediumId &&
                x.EducationMediumStageId == null);
            assignments.ShouldContain(x =>
                x.AcademicYearId == gseb.AcademicYearId &&
                x.EducationBoardId == gseb.BoardId &&
                x.EducationMediumId == gseb.MediumId &&
                x.EducationMediumStageId == gseb.StageId);
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Create_Assignments_For_Each_User_And_Scope_Row()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var userA = await CreateUserAsync();
            var userB = await CreateUserAsync();
            var cbse = await CreateAcademicHierarchyAsync();
            var gseb = await CreateGsebHierarchyBranchAsync(cbse.AcademicYearId);

            var result = await WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
            {
                UserIds = [userA.Id, userB.Id],
                AcademicYearId = cbse.AcademicYearId,
                Scopes =
                [
                    new BulkScopeAssignmentScopeDto
                    {
                        EducationBoardId = cbse.BoardId
                    },
                    new BulkScopeAssignmentScopeDto
                    {
                        EducationBoardId = gseb.BoardId,
                        EducationMediumId = gseb.MediumId,
                        EducationMediumStageId = gseb.StageId
                    }
                ]
            }));

            result.AssignmentsCreated.ShouldBe(4);
            result.TotalCombinations.ShouldBe(4);
            (await GetUserAssignmentsAsync(userA.Id)).Count.ShouldBe(2);
            (await GetUserAssignmentsAsync(userB.Id)).Count.ShouldBe(2);
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Save_Parent_Medium_Node_As_Full_Stage_Access()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var hierarchy = await CreateAcademicHierarchyAsync();

            await WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
            {
                UserIds = [user.Id],
                AcademicYearId = hierarchy.AcademicYearId,
                Scopes =
                [
                    new BulkScopeAssignmentScopeDto
                    {
                        EducationBoardId = hierarchy.BoardId,
                        EducationMediumId = hierarchy.MediumId
                    }
                ]
            }));

            var assignments = await GetUserAssignmentsAsync(user.Id);
            var assignment = assignments.Single();
            assignment.AcademicYearId.ShouldBe(hierarchy.AcademicYearId);
            assignment.EducationBoardId.ShouldBe(hierarchy.BoardId);
            assignment.EducationMediumId.ShouldBe(hierarchy.MediumId);
            assignment.EducationMediumStageId.ShouldBeNull();
            assignment.GrantsAllMediumStagesInMedium().ShouldBeTrue();

            var hierarchyResult = await WithUnitOfWorkAsync(() =>
                _userScopeAppService.GetUserScopeHierarchyAsync(user.Id, hierarchy.AcademicYearId));
            var mediumNode = hierarchyResult.AccessibleHierarchy
                .Single()
                .Boards.Single(x => x.BoardId == hierarchy.BoardId)
                .Mediums.Single(x => x.MediumId == hierarchy.MediumId);

            mediumNode.HasFullMediumStageAccess.ShouldBeTrue();
            mediumNode.MediumStages.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Save_Parent_Medium_Only_Payload_And_Sync_Employee_Organizations()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var employee = await CreateEmployeeForUserAsync(user.Id);
            var hierarchy = await CreateAcademicHierarchyAsync();

            await WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
            {
                UserIds = [user.Id],
                AcademicYearId = hierarchy.AcademicYearId,
                Scopes =
                [
                    new BulkScopeAssignmentScopeDto
                    {
                        EducationBoardId = hierarchy.BoardId,
                        EducationMediumId = hierarchy.MediumId
                    }
                ]
            }));

            var assignments = await GetUserAssignmentsAsync(user.Id);
            var assignment = assignments.Single();
            assignment.AcademicYearId.ShouldBe(hierarchy.AcademicYearId);
            assignment.EducationBoardId.ShouldBe(hierarchy.BoardId);
            assignment.EducationMediumId.ShouldBe(hierarchy.MediumId);
            assignment.EducationMediumStageId.ShouldBeNull();
            assignment.GrantsAllMediumStagesInMedium().ShouldBeTrue();

            var hierarchyResult = await WithUnitOfWorkAsync(() =>
                _userScopeAppService.GetUserScopeHierarchyAsync(user.Id, hierarchy.AcademicYearId));
            var mediumNode = hierarchyResult.AccessibleHierarchy
                .Single()
                .Boards.Single(x => x.BoardId == hierarchy.BoardId)
                .Mediums.Single(x => x.MediumId == hierarchy.MediumId);
            mediumNode.HasFullMediumStageAccess.ShouldBeTrue();
            mediumNode.MediumStages.ShouldBeEmpty();

            var employeeAssignments = await GetEmployeeOrganizationAssignmentsAsync(employee.Id);
            employeeAssignments.Select(x => x.EducationMediumStageId).ShouldBe([hierarchy.StageId]);
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Reject_Mixed_Row_When_Path_Is_Not_In_Academic_Year()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var yearA = await CreateAcademicHierarchyAsync();
            var yearB = await CreateAcademicHierarchyAsync();

            var exception = await Should.ThrowAsync<BusinessException>(() =>
                WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
                {
                    UserIds = [user.Id],
                    AcademicYearId = yearA.AcademicYearId,
                    Scopes =
                    [
                        new BulkScopeAssignmentScopeDto
                        {
                            EducationBoardId = yearA.BoardId,
                            EducationMediumId = yearB.MediumId
                        }
                    ]
                })));

            exception.Code.ShouldBe(UserScopeErrorCodes.InvalidHierarchy);
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Sync_Employee_Organization_Assignments_From_Academic_Permission()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var employee = await CreateEmployeeForUserAsync(user.Id);
            var hierarchy = await CreateAcademicHierarchyAsync();

            await WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
            {
                UserIds = [user.Id],
                AcademicYearId = hierarchy.AcademicYearId,
                Scopes =
                [
                    new BulkScopeAssignmentScopeDto
                    {
                        EducationBoardId = hierarchy.BoardId,
                        EducationMediumId = hierarchy.MediumId,
                        EducationMediumStageId = hierarchy.StageId
                    }
                ]
            }));

            var employeeAssignments = await GetEmployeeOrganizationAssignmentsAsync(employee.Id);
            employeeAssignments.Select(x => x.EducationMediumStageId).ShouldBe([hierarchy.StageId]);
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Clear_Target_Academic_Year_When_ClearExisting_Is_True()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var employee = await CreateEmployeeForUserAsync(user.Id);
            var hierarchy = await CreateAcademicHierarchyAsync();

            await BulkAssignStageAsync(user.Id, hierarchy.AcademicYearId, hierarchy.StageId);

            var result = await WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
            {
                UserIds = [user.Id],
                AcademicYearId = hierarchy.AcademicYearId,
                Scopes = [],
                ClearExisting = true
            }));

            result.AssignmentsCreated.ShouldBe(0);
            result.TotalCombinations.ShouldBe(0);

            var assignments = await GetUserAssignmentsAsync(user.Id);
            assignments.ShouldNotContain(x => x.AcademicYearId == hierarchy.AcademicYearId);

            var employeeAssignments = await GetEmployeeOrganizationAssignmentsAsync(employee.Id);
            employeeAssignments.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Keep_Year_Only_Full_Access_When_ClearExisting_Is_False()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var hierarchy = await CreateAcademicHierarchyAsync();

            var result = await WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
            {
                UserIds = [user.Id],
                AcademicYearId = hierarchy.AcademicYearId,
                Scopes = [new BulkScopeAssignmentScopeDto()]
            }));

            result.AssignmentsCreated.ShouldBe(1);
            result.TotalCombinations.ShouldBe(1);

            var assignment = (await GetUserAssignmentsAsync(user.Id)).Single();
            assignment.AcademicYearId.ShouldBe(hierarchy.AcademicYearId);
            assignment.EducationBoardId.ShouldBeNull();
            assignment.EducationMediumId.ShouldBeNull();
            assignment.EducationMediumStageId.ShouldBeNull();
            assignment.StandardId.ShouldBeNull();
            assignment.DivisionId.ShouldBeNull();
            assignment.GrantsAllBoardsInAcademicYear().ShouldBeTrue();
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Reject_ClearExisting_With_Scope_Ids()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var hierarchy = await CreateAcademicHierarchyAsync();
            await BulkAssignBoardAsync(user.Id, hierarchy.AcademicYearId, hierarchy.BoardId);

            var exception = await Should.ThrowAsync<AbpValidationException>(() =>
                WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
                {
                    UserIds = [user.Id],
                    AcademicYearId = hierarchy.AcademicYearId,
                    Scopes =
                    [
                        new BulkScopeAssignmentScopeDto
                        {
                            EducationBoardId = hierarchy.BoardId,
                            EducationMediumId = hierarchy.MediumId
                        }
                    ],
                    ClearExisting = true
                })));

            exception.ValidationErrors.ShouldContain(x =>
                x.MemberNames.Contains(nameof(BulkScopeAssignmentDto.ClearExisting)));
            await AssertSingleTargetYearBoardAssignmentAsync(
                user.Id,
                hierarchy.AcademicYearId,
                hierarchy.BoardId);
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Reject_Null_Scopes_When_Not_ClearExisting_Without_Deleting_Target_Year()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var hierarchy = await CreateAcademicHierarchyAsync();
            await BulkAssignBoardAsync(user.Id, hierarchy.AcademicYearId, hierarchy.BoardId);

            var exception = await Should.ThrowAsync<Exception>(() =>
                WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
                {
                    UserIds = [user.Id],
                    AcademicYearId = hierarchy.AcademicYearId,
                    Scopes = null!,
                    ClearExisting = false
                })));

            (exception is AbpValidationException || exception is BusinessException).ShouldBeTrue(exception.ToString());
            await AssertSingleTargetYearBoardAssignmentAsync(
                user.Id,
                hierarchy.AcademicYearId,
                hierarchy.BoardId);
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Reject_Empty_Scopes_When_Not_ClearExisting_Without_Deleting_Target_Year()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var hierarchy = await CreateAcademicHierarchyAsync();
            await BulkAssignBoardAsync(user.Id, hierarchy.AcademicYearId, hierarchy.BoardId);

            var exception = await Should.ThrowAsync<Exception>(() =>
                WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
                {
                    UserIds = [user.Id],
                    AcademicYearId = hierarchy.AcademicYearId,
                    Scopes = [],
                    ClearExisting = false
                })));

            (exception is AbpValidationException || exception is BusinessException).ShouldBeTrue(exception.ToString());
            await AssertSingleTargetYearBoardAssignmentAsync(
                user.Id,
                hierarchy.AcademicYearId,
                hierarchy.BoardId);
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Reject_Null_Scope_Row_Without_Deleting_Target_Year()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var hierarchy = await CreateAcademicHierarchyAsync();
            await BulkAssignBoardAsync(user.Id, hierarchy.AcademicYearId, hierarchy.BoardId);

            var exception = await Should.ThrowAsync<Exception>(() =>
                WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
                {
                    UserIds = [user.Id],
                    AcademicYearId = hierarchy.AcademicYearId,
                    Scopes = [null!],
                    ClearExisting = false
                })));

            (exception is AbpValidationException || exception is BusinessException).ShouldBeTrue(exception.ToString());
            await AssertSingleTargetYearBoardAssignmentAsync(
                user.Id,
                hierarchy.AcademicYearId,
                hierarchy.BoardId);
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Reject_Empty_Guid_Scope_Row_Without_Deleting_Target_Year()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var hierarchy = await CreateAcademicHierarchyAsync();
            await BulkAssignBoardAsync(user.Id, hierarchy.AcademicYearId, hierarchy.BoardId);

            var exception = await Should.ThrowAsync<Exception>(() =>
                WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
                {
                    UserIds = [user.Id],
                    AcademicYearId = hierarchy.AcademicYearId,
                    Scopes =
                    [
                        new BulkScopeAssignmentScopeDto
                        {
                            EducationBoardId = Guid.Empty
                        }
                    ],
                    ClearExisting = false
                })));

            (exception is AbpValidationException || exception is BusinessException).ShouldBeTrue(exception.ToString());
            await AssertSingleTargetYearBoardAssignmentAsync(
                user.Id,
                hierarchy.AcademicYearId,
                hierarchy.BoardId);
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Replace_Employee_Organization_Assignments_Only_For_Target_Academic_Year()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var employee = await CreateEmployeeForUserAsync(user.Id);
            var yearA = await CreateAcademicHierarchyAsync();
            var yearAReplacementStageId = await CreateStageAsync(yearA.MediumId);
            var yearB = await CreateAcademicHierarchyAsync();

            await BulkAssignStageAsync(user.Id, yearA.AcademicYearId, yearA.StageId);
            await BulkAssignStageAsync(user.Id, yearB.AcademicYearId, yearB.StageId);
            await BulkAssignStageAsync(user.Id, yearA.AcademicYearId, yearAReplacementStageId);

            var employeeAssignments = await GetEmployeeOrganizationAssignmentsAsync(employee.Id);
            employeeAssignments.ShouldContain(x => x.EducationMediumStageId == yearAReplacementStageId);
            employeeAssignments.ShouldContain(x => x.EducationMediumStageId == yearB.StageId);
            employeeAssignments.ShouldNotContain(x => x.EducationMediumStageId == yearA.StageId);
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Not_Delete_Organization_Assignments_For_Other_Employees_With_Same_User()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();
            var otherEmployeeUser = await CreateUserAsync();
            var otherEmployee = await CreateEmployeeForUserAsync(otherEmployeeUser.Id, EmployeeStatusType.Left);
            await AssignEmployeeUserIdAsync(otherEmployee.Id, user.Id);
            var selectedEmployee = await CreateEmployeeForUserAsync(user.Id);
            var hierarchy = await CreateAcademicHierarchyAsync();
            var otherStageId = await CreateStageAsync(hierarchy.MediumId);

            await WithUnitOfWorkAsync(() =>
                _employeeOrganizationAssignmentRepository.InsertAsync(
                    new EmployeeOrganizationAssignment(
                        Guid.NewGuid(),
                        TestTenantId,
                        otherEmployee.Id,
                        otherStageId),
                    autoSave: true));

            await BulkAssignStageAsync(user.Id, hierarchy.AcademicYearId, hierarchy.StageId);

            var selectedAssignments = await GetEmployeeOrganizationAssignmentsAsync(selectedEmployee.Id);
            selectedAssignments.Select(x => x.EducationMediumStageId).ShouldBe([hierarchy.StageId]);

            var otherAssignments = await GetEmployeeOrganizationAssignmentsAsync(otherEmployee.Id);
            otherAssignments.Select(x => x.EducationMediumStageId).ShouldBe([otherStageId]);
        }
    }

    [Fact]
    public async Task BulkAssignAsync_Should_Reject_Missing_Academic_Year()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();

            var exception = await Should.ThrowAsync<AbpValidationException>(() =>
                WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
                {
                    UserIds = [user.Id],
                    Scopes =
                    [
                        new BulkScopeAssignmentScopeDto
                        {
                            EducationBoardId = Guid.NewGuid()
                        }
                    ]
                })));

            exception.ValidationErrors.ShouldContain(x =>
                x.MemberNames.Contains(nameof(BulkScopeAssignmentDto.AcademicYearId)));
        }
    }

    [Fact]
    public async Task GetUsersByDivisionAsync_Should_Not_Broaden_Year_Wildcard_To_Other_Years()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var yearAUser = await CreateUserAsync();
            var yearBUser = await CreateUserAsync();
            var yearA = await CreateAcademicHierarchyAsync();
            var yearB = await CreateAcademicHierarchyAsync();

            await BulkAssignAcademicYearAsync(yearAUser.Id, yearA.AcademicYearId);
            await BulkAssignAcademicYearAsync(yearBUser.Id, yearB.AcademicYearId);

            var userIds = await WithUnitOfWorkAsync(() =>
                _userScopeAppService.GetUsersByDivisionAsync(yearA.DivisionId));

            userIds.ShouldContain(yearAUser.Id);
            userIds.ShouldNotContain(yearBUser.Id);
        }
    }

    [Fact]
    public async Task GetUsersByStandardAsync_Should_Not_Broaden_Year_Wildcard_To_Other_Years()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var yearAUser = await CreateUserAsync();
            var yearBUser = await CreateUserAsync();
            var yearA = await CreateAcademicHierarchyAsync();
            var yearB = await CreateAcademicHierarchyAsync();

            await BulkAssignAcademicYearAsync(yearAUser.Id, yearA.AcademicYearId);
            await BulkAssignAcademicYearAsync(yearBUser.Id, yearB.AcademicYearId);

            var userIds = await WithUnitOfWorkAsync(() =>
                _userScopeAppService.GetUsersByStandardAsync(yearA.StandardId));

            userIds.ShouldContain(yearAUser.Id);
            userIds.ShouldNotContain(yearBUser.Id);
        }
    }

    [Fact]
    public void BulkScopeAssignmentDto_Should_Require_Academic_Year()
    {
        var input = new BulkScopeAssignmentDto
        {
            UserIds = [Guid.NewGuid()],
            Scopes =
            [
                new BulkScopeAssignmentScopeDto
                {
                    EducationBoardId = Guid.NewGuid()
                }
            ]
        };
        var validationResults = new List<ValidationResult>();

        Validator.TryValidateObject(
            input,
            new ValidationContext(input),
            validationResults,
            validateAllProperties: true);

        validationResults.ShouldContain(x =>
            x.MemberNames.Contains(nameof(BulkScopeAssignmentDto.AcademicYearId)));
    }

    [Fact]
    public void BulkScopeAssignmentDto_Should_Require_Scope_Row_When_Not_ClearExisting()
    {
        var input = new BulkScopeAssignmentDto
        {
            UserIds = [Guid.NewGuid()],
            AcademicYearId = Guid.NewGuid(),
            ClearExisting = false,
            Scopes = []
        };
        var validationResults = new List<ValidationResult>();

        Validator.TryValidateObject(
            input,
            new ValidationContext(input),
            validationResults,
            validateAllProperties: true);

        validationResults.ShouldContain(x =>
            x.MemberNames.Contains(nameof(BulkScopeAssignmentDto.Scopes)));
    }

    [Fact]
    public void BulkScopeAssignmentDto_Should_Require_Parent_Ids_For_Child_Rows()
    {
        var input = new BulkScopeAssignmentDto
        {
            UserIds = [Guid.NewGuid()],
            AcademicYearId = Guid.NewGuid(),
            Scopes =
            [
                new BulkScopeAssignmentScopeDto
                {
                    EducationMediumStageId = Guid.NewGuid()
                }
            ]
        };
        var validationResults = new List<ValidationResult>();

        Validator.TryValidateObject(
            input,
            new ValidationContext(input),
            validationResults,
            validateAllProperties: true);

        validationResults.ShouldContain(x =>
            x.MemberNames.Contains(nameof(BulkScopeAssignmentDto.Scopes)));
    }

    private async Task<IdentityUser> CreateUserAsync()
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var id = Guid.NewGuid();
            var user = new IdentityUser(id, $"bulk-scope-{id:N}", $"bulk-scope-{id:N}@test.local", TestTenantId);
            var result = await _userManager.CreateAsync(user, "Test@1234");
            result.Succeeded.ShouldBeTrue(string.Join(", ", result.Errors.Select(x => x.Description)));
            return user;
        });
    }

    private async Task<Guid> CreateRoleAsync()
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var role = new IdentityRole(Guid.NewGuid(), $"scope-employee-role-{suffix}", TestTenantId);
            var result = await _roleManager.CreateAsync(role);
            result.Succeeded.ShouldBeTrue(string.Join(", ", result.Errors.Select(x => x.Description)));
            return role.Id;
        });
    }

    private async Task<Employee> CreateEmployeeForUserAsync(
        Guid userId,
        EmployeeStatusType employeeStatusType = EmployeeStatusType.Active)
    {
        var roleId = await CreateRoleAsync();

        return await WithUnitOfWorkAsync(async () =>
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var employee = await _employeeManager.CreateEmployeeAsync(
                userId,
                roleId,
                $"Scope{suffix}",
                "Employee",
                null,
                $"SE{suffix[..4]}",
                employeeStatusType,
                DateOnly.FromDateTime(new DateTime(1992, 3, 10)),
                mobileNo: UniqueEmployeeMobileNo(),
                email: $"scope.employee.{suffix}@test.local",
                panNo: UniqueEmployeePan(),
                adharNo: UniqueEmployeeAdhar(),
                dateOfJoining: DateOnly.FromDateTime(new DateTime(2026, 4, 1)),
                employeeGroup: EmployeeGroupType.Teaching);

            return await _employeeRepository.InsertAsync(employee, autoSave: true);
        });
    }

    private async Task AssignEmployeeUserIdAsync(Guid employeeId, Guid userId)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var employee = await _employeeRepository.GetAsync(employeeId);
            employee.UpdateUserId(userId);
            await _employeeRepository.UpdateAsync(employee, autoSave: true);
        });
    }

    private async Task<AcademicYearBoards> CreateAcademicYearWithBoardsAsync()
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var academicYear = await _academicYearRepository.InsertAsync(
                await _academicYearManager.CreateAsync(
                    $"Bulk Scope {suffix}",
                    new DateTime(2040, 6, 1),
                    new DateTime(2041, 5, 31)),
                autoSave: true);

            var cbseBoard = await _boardRepository.InsertAsync(
                await _boardManager.CreateAsync(academicYear.Id, EducationBoardType.CBSE),
                autoSave: true);
            var gsebBoard = await _boardRepository.InsertAsync(
                await _boardManager.CreateAsync(academicYear.Id, EducationBoardType.GSEB),
                autoSave: true);

            return new AcademicYearBoards(academicYear.Id, cbseBoard.Id, gsebBoard.Id);
        });
    }

    private async Task<AcademicHierarchy> CreateAcademicHierarchyAsync()
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var academicYear = await _academicYearRepository.InsertAsync(
                await _academicYearManager.CreateAsync(
                    $"Bulk Scope Hierarchy {suffix}",
                    new DateTime(2050, 6, 1),
                    new DateTime(2051, 5, 31)),
                autoSave: true);

            var board = await _boardRepository.InsertAsync(
                await _boardManager.CreateAsync(academicYear.Id, EducationBoardType.CBSE),
                autoSave: true);
            var medium = await _mediumRepository.InsertAsync(
                await _mediumManager.CreateAsync(board.Id, $"English {suffix}"),
                autoSave: true);
            var stage = await _stageRepository.InsertAsync(
                await _stageManager.CreateAsync(medium.Id, $"Secondary {suffix}"),
                autoSave: true);
            var standard = await _standardRepository.InsertAsync(
                await _standardManager.CreateAsync(stage.Id, $"Std {suffix}", 10, null),
                autoSave: true);
            var division = await _divisionRepository.InsertAsync(
                await _divisionManager.CreateAsync(standard.Id, $"A {suffix}", 40),
                autoSave: true);

            return new AcademicHierarchy(
                academicYear.Id,
                board.Id,
                medium.Id,
                stage.Id,
                standard.Id,
                division.Id);
        });
    }

    private async Task<AcademicHierarchy> CreateGsebHierarchyBranchAsync(Guid academicYearId)
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var board = await _boardRepository.InsertAsync(
                await _boardManager.CreateAsync(academicYearId, EducationBoardType.GSEB),
                autoSave: true);
            var medium = await _mediumRepository.InsertAsync(
                await _mediumManager.CreateAsync(board.Id, $"Gujarati {suffix}"),
                autoSave: true);
            var stage = await _stageRepository.InsertAsync(
                await _stageManager.CreateAsync(medium.Id, $"Higher Secondary {suffix}"),
                autoSave: true);
            var standard = await _standardRepository.InsertAsync(
                await _standardManager.CreateAsync(stage.Id, $"Std {suffix}", 12, null),
                autoSave: true);
            var division = await _divisionRepository.InsertAsync(
                await _divisionManager.CreateAsync(standard.Id, $"A {suffix}", 40),
                autoSave: true);

            return new AcademicHierarchy(
                academicYearId,
                board.Id,
                medium.Id,
                stage.Id,
                standard.Id,
                division.Id);
        });
    }

    private async Task<Guid> CreateStageAsync(Guid mediumId)
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var stage = await _stageRepository.InsertAsync(
                await _stageManager.CreateAsync(mediumId, $"Replacement {suffix}"),
                autoSave: true);

            return stage.Id;
        });
    }

    private async Task BulkAssignAcademicYearAsync(Guid userId, Guid academicYearId)
    {
        await WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
        {
            UserIds = [userId],
            AcademicYearId = academicYearId,
            Scopes = [new BulkScopeAssignmentScopeDto()]
        }));
    }

    private async Task BulkAssignBoardAsync(Guid userId, Guid academicYearId, Guid boardId)
    {
        await WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
        {
            UserIds = [userId],
            AcademicYearId = academicYearId,
            Scopes =
            [
                new BulkScopeAssignmentScopeDto
                {
                    EducationBoardId = boardId
                }
            ]
        }));
    }

    private async Task BulkAssignStageAsync(Guid userId, Guid academicYearId, Guid stageId)
    {
        var scope = await BuildStageScopeRowAsync(stageId);

        await WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
        {
            UserIds = [userId],
            AcademicYearId = academicYearId,
            Scopes = [scope]
        }));
    }

    private async Task<BulkScopeAssignmentScopeDto> BuildStageScopeRowAsync(Guid stageId)
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var stage = await _stageRepository.GetAsync(stageId);
            var medium = await _mediumRepository.GetAsync(stage.EducationMediumId);
            var board = await _boardRepository.GetAsync(medium.EducationBoardId);

            return new BulkScopeAssignmentScopeDto
            {
                EducationBoardId = board.Id,
                EducationMediumId = medium.Id,
                EducationMediumStageId = stage.Id
            };
        });
    }

    private async Task<List<UserScopeAssignment>> GetUserAssignmentsAsync(Guid userId)
    {
        return await WithUnitOfWorkAsync(() =>
            _scopeRepository.GetListAsync(x => x.UserId == userId && x.IsActive && !x.IsDeleted));
    }

    private async Task AssertSingleTargetYearBoardAssignmentAsync(
        Guid userId,
        Guid academicYearId,
        Guid boardId)
    {
        var assignments = await GetUserAssignmentsAsync(userId);
        var targetYearAssignments = assignments.Where(x => x.AcademicYearId == academicYearId).ToList();
        targetYearAssignments.Count.ShouldBe(1);
        targetYearAssignments.Single().EducationBoardId.ShouldBe(boardId);
    }

    private async Task<List<EmployeeOrganizationAssignment>> GetEmployeeOrganizationAssignmentsAsync(Guid employeeId)
    {
        return await WithUnitOfWorkAsync(() =>
            _employeeOrganizationAssignmentRepository.GetListAsync(x => x.EmployeeId == employeeId && !x.IsDeleted));
    }

    private static string UniqueEmployeeMobileNo()
    {
        var number = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0) % 1_000_000_000;
        return $"9{number:D9}";
    }

    private static string UniqueEmployeePan()
    {
        var number = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0) % 10_000;
        return $"ABCDE{number:D4}F";
    }

    private static string UniqueEmployeeAdhar()
    {
        var number = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0) % 100_000_000_000;
        return $"8{number:D11}";
    }

    private sealed record AcademicYearBoards(Guid AcademicYearId, Guid CbseBoardId, Guid GsebBoardId);

    private sealed record AcademicHierarchy(
        Guid AcademicYearId,
        Guid BoardId,
        Guid MediumId,
        Guid StageId,
        Guid StandardId,
        Guid DivisionId);
}
