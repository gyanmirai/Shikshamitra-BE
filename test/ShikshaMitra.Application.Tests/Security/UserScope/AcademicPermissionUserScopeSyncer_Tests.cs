using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Seeders;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Security.UserScope;

public class AcademicPermissionUserScopeSyncer_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

	private readonly AcademicPermissionUserScopeSyncer _syncer;
	private readonly AcademicSetupPermissionManager _permissionManager;
	private readonly AcademicYearManager _academicYearManager;
	private readonly EducationBoardManager _boardManager;
	private readonly EducationMediumManager _mediumManager;
	private readonly EducationMediumStageManager _stageManager;
	private readonly StandardManager _standardManager;
	private readonly DivisionManager _divisionManager;
	private readonly IdentityUserManager _userManager;
	private readonly IRepository<TenantSettings.AcademicYears.AcademicYear, Guid> _academicYearRepository;
	private readonly IRepository<EducationBoard, Guid> _boardRepository;
	private readonly IRepository<EducationMedium, Guid> _mediumRepository;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;
	private readonly IRepository<EducationMediumStagePermission, Guid> _stagePermissionRepository;
	private readonly IRepository<TenantSettings.AcademicSetups.Divisions.DivisionPermission, Guid> _divisionPermissionRepository;
	private readonly IRepository<UserScopeAssignment, Guid> _scopeRepository;
	private readonly ICurrentTenant _currentTenant;

	public AcademicPermissionUserScopeSyncer_Tests()
	{
		_syncer = GetRequiredService<AcademicPermissionUserScopeSyncer>();
		_permissionManager = GetRequiredService<AcademicSetupPermissionManager>();
		_academicYearManager = GetRequiredService<AcademicYearManager>();
		_boardManager = GetRequiredService<EducationBoardManager>();
		_mediumManager = GetRequiredService<EducationMediumManager>();
		_stageManager = GetRequiredService<EducationMediumStageManager>();
		_standardManager = GetRequiredService<StandardManager>();
		_divisionManager = GetRequiredService<DivisionManager>();
		_userManager = GetRequiredService<IdentityUserManager>();
		_academicYearRepository = GetRequiredService<IRepository<TenantSettings.AcademicYears.AcademicYear, Guid>>();
		_boardRepository = GetRequiredService<IRepository<EducationBoard, Guid>>();
		_mediumRepository = GetRequiredService<IRepository<EducationMedium, Guid>>();
		_stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
		_divisionRepository = GetRequiredService<IRepository<Division, Guid>>();
		_stagePermissionRepository = GetRequiredService<IRepository<EducationMediumStagePermission, Guid>>();
		_divisionPermissionRepository =
			GetRequiredService<IRepository<TenantSettings.AcademicSetups.Divisions.DivisionPermission, Guid>>();
		_scopeRepository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task SyncAsync_Should_Create_Stage_Scope_With_Resolved_Parent_Hierarchy()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var user = await CreateUserAsync();
			var hierarchy = await CreateAcademicHierarchyAsync();
			await InsertStagePermissionAsync(user.Id, hierarchy.StageId);

			var inserted = await WithUnitOfWorkAsync(() => _syncer.SyncAsync());

			inserted.ShouldBe(1);
			var scope = (await _scopeRepository.GetListAsync(x => x.UserId == user.Id)).Single();
			scope.AcademicYearId.ShouldBe(hierarchy.AcademicYearId);
			scope.EducationBoardId.ShouldBe(hierarchy.BoardId);
			scope.EducationMediumId.ShouldBe(hierarchy.MediumId);
			scope.EducationMediumStageId.ShouldBe(hierarchy.StageId);
			scope.StandardId.ShouldBeNull();
			scope.DivisionId.ShouldBeNull();
		}
	}

	[Fact]
	public async Task SyncAsync_Should_Create_Division_Scope_With_Resolved_Full_Hierarchy()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var user = await CreateUserAsync();
			var hierarchy = await CreateAcademicHierarchyAsync();
			await InsertDivisionPermissionAsync(user.Id, hierarchy.DivisionId);

			await WithUnitOfWorkAsync(() => _syncer.SyncAsync());

			var scope = (await _scopeRepository.GetListAsync(x => x.UserId == user.Id)).Single();
			scope.AcademicYearId.ShouldBe(hierarchy.AcademicYearId);
			scope.EducationBoardId.ShouldBe(hierarchy.BoardId);
			scope.EducationMediumId.ShouldBe(hierarchy.MediumId);
			scope.EducationMediumStageId.ShouldBe(hierarchy.StageId);
			scope.StandardId.ShouldBe(hierarchy.StandardId);
			scope.DivisionId.ShouldBe(hierarchy.DivisionId);
		}
	}

	[Fact]
	public async Task SyncAsync_Should_Be_Idempotent_And_Not_Delete_Manual_Scopes()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var user = await CreateUserAsync();
			var manualAcademicYearId = Guid.NewGuid();
			var hierarchy = await CreateAcademicHierarchyAsync();
			await InsertStagePermissionAsync(user.Id, hierarchy.StageId);
			await InsertManualScopeAsync(user.Id, manualAcademicYearId);

			var firstInserted = await WithUnitOfWorkAsync(() => _syncer.SyncAsync());
			var secondInserted = await WithUnitOfWorkAsync(() => _syncer.SyncAsync());

			firstInserted.ShouldBe(1);
			secondInserted.ShouldBe(0);
			var scopes = await _scopeRepository.GetListAsync(x => x.UserId == user.Id);
			scopes.Count(x => x.IsActive).ShouldBe(2);
			scopes.ShouldContain(x => x.AcademicYearId == manualAcademicYearId && x.Description == "Manual test scope");
			scopes.Count(x => x.EducationMediumStageId == hierarchy.StageId).ShouldBe(1);
		}
	}

	[Fact]
	public async Task SyncAsync_Should_Deactivate_Legacy_Demo_Scopes_Only()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var user = await CreateUserAsync();
			var manualAcademicYearId = Guid.NewGuid();
			await InsertLegacySeededScopeAsync(user.Id);
			await InsertManualScopeAsync(user.Id, manualAcademicYearId);

			await WithUnitOfWorkAsync(() => _syncer.SyncAsync());

			var scopes = await _scopeRepository.GetListAsync(x => x.UserId == user.Id);
			scopes.Single(x => x.Description!.StartsWith("Seeded full organization scope")).IsActive.ShouldBeFalse();
			scopes.Single(x => x.Description == "Manual test scope").IsActive.ShouldBeTrue();
		}
	}

	[Fact]
	public async Task SyncAsync_Should_Not_Create_Scope_For_User_Without_Academic_Permissions()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var user = await CreateUserAsync();

			var inserted = await WithUnitOfWorkAsync(() => _syncer.SyncAsync());

			inserted.ShouldBe(0);
			(await _scopeRepository.GetListAsync(x => x.UserId == user.Id)).ShouldBeEmpty();
		}
	}

	private async Task<IdentityUser> CreateUserAsync()
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var id = Guid.NewGuid();
			var user = new IdentityUser(id, $"scope-sync-{id:N}", $"scope-sync-{id:N}@test.local", TestTenantId);
			var result = await _userManager.CreateAsync(user, "Test@1234");
			result.Succeeded.ShouldBeTrue(string.Join(", ", result.Errors.Select(x => x.Description)));
			return user;
		});
	}

	private async Task<AcademicHierarchy> CreateAcademicHierarchyAsync()
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var suffix = Guid.NewGuid().ToString("N")[..8];
			var academicYear = await _academicYearRepository.InsertAsync(
				await _academicYearManager.CreateAsync(
					$"Scope Sync {suffix}",
					new DateTime(2030, 6, 1),
					new DateTime(2031, 5, 31)),
				autoSave: true);

			var board = await _boardRepository.InsertAsync(
				await _boardManager.CreateAsync(academicYear.Id, EducationBoardType.GSEB),
				autoSave: true);
			var medium = await _mediumRepository.InsertAsync(
				await _mediumManager.CreateAsync(board.Id, $"Gujarati {suffix}"),
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

	private async Task InsertStagePermissionAsync(Guid userId, Guid stageId)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			await _stagePermissionRepository.InsertManyAsync(
				_permissionManager.CreateEducationMediumStagePermissions(userId, [stageId]),
				autoSave: true);
		});
	}

	private async Task InsertDivisionPermissionAsync(Guid userId, Guid divisionId)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			await _divisionPermissionRepository.InsertManyAsync(
				_permissionManager.CreateDivisionPermissions(userId, [divisionId]),
				autoSave: true);
		});
	}

	private async Task InsertManualScopeAsync(Guid userId, Guid academicYearId)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					TestTenantId,
					userId: userId,
					academicYearId: academicYearId,
					description: "Manual test scope"),
				autoSave: true);
		});
	}

	private async Task InsertLegacySeededScopeAsync(Guid userId)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					TestTenantId,
					userId: userId,
					description: "Seeded full organization scope for Teacher."),
				autoSave: true);
		});
	}

	private sealed record AcademicHierarchy(
		Guid AcademicYearId,
		Guid BoardId,
		Guid MediumId,
		Guid StageId,
		Guid StandardId,
		Guid DivisionId);
}
