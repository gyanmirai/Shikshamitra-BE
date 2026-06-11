using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
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
    public async Task BulkAssignAsync_Should_Reject_Missing_Academic_Year()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var user = await CreateUserAsync();

            var exception = await Should.ThrowAsync<AbpValidationException>(() =>
                WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
                {
                    UserIds = [user.Id],
                    EducationBoardIds = [Guid.NewGuid()]
                })));

            exception.ValidationErrors.ShouldContain(x =>
                x.MemberNames.Contains(nameof(BulkScopeAssignmentDto.AcademicYearIds)));
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
            EducationBoardIds = [Guid.NewGuid()]
        };
        var validationResults = new List<ValidationResult>();

        Validator.TryValidateObject(
            input,
            new ValidationContext(input),
            validationResults,
            validateAllProperties: true);

        validationResults.ShouldContain(x =>
            x.MemberNames.Contains(nameof(BulkScopeAssignmentDto.AcademicYearIds)));
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

    private async Task BulkAssignAcademicYearAsync(Guid userId, Guid academicYearId)
    {
        await WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
        {
            UserIds = [userId],
            AcademicYearIds = [academicYearId]
        }));
    }

    private async Task BulkAssignBoardAsync(Guid userId, Guid academicYearId, Guid boardId)
    {
        await WithUnitOfWorkAsync(() => _userScopeAppService.BulkAssignAsync(new BulkScopeAssignmentDto
        {
            UserIds = [userId],
            AcademicYearIds = [academicYearId],
            EducationBoardIds = [boardId]
        }));
    }

    private async Task<List<UserScopeAssignment>> GetUserAssignmentsAsync(Guid userId)
    {
        return await WithUnitOfWorkAsync(() =>
            _scopeRepository.GetListAsync(x => x.UserId == userId && x.IsActive && !x.IsDeleted));
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
