using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Setting.BorrowingRules;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryPolicyScopeSafety_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Class_Reader_Should_See_Applicable_Global_Stage_And_Own_Class_Policies_Before_Counting()
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var context = await SeedAsync(tenantId);
		using var reader = Principal(context.ReaderId, tenantId);

		var result = await GetRequiredService<ILibraryBorrowingPolicyAppService>().GetListAsync(context.YearId, pageSize: 100);

		result.TotalCount.ShouldBe(3);
		result.Items.Select(x => x.Id).ShouldContain(context.GlobalPolicy.Id);
		result.Items.Select(x => x.Id).ShouldContain(context.StagePolicy.Id);
		result.Items.Select(x => x.Id).ShouldContain(context.Class5Policy.Id);
		result.Items.Select(x => x.Id).ShouldNotContain(context.Class6Policy.Id);
	}

	[Fact]
	public async Task Class_Reader_Should_Not_Read_Other_Class_Policy_By_Id()
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var context = await SeedAsync(tenantId);
		using var reader = Principal(context.ReaderId, tenantId);

		var error = await Should.ThrowAsync<BusinessException>(() =>
			GetRequiredService<ILibraryBorrowingPolicyAppService>().GetAsync(context.Class6Policy.Id));

		error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Fact]
	public async Task Class_Manager_Should_Not_Take_Over_Other_Class_Draft_By_Changing_Its_Target()
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var context = await SeedAsync(tenantId);
		using (Principal(context.ReaderId, tenantId))
		{
			var replacement = Draft("Retargeted", context.StageId, context.Class5Id);
			replacement.ConcurrencyStamp = context.Class6Policy.ConcurrencyStamp;
			var error = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<ILibraryBorrowingPolicyAppService>().UpdateAsync(context.Class6Policy.Id, replacement));
			error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<LibraryBorrowingPolicyVersion, Guid>>().GetAsync(context.Class6Policy.Id)).StandardId.ShouldBe(context.Class6Id));
	}

	[Fact]
	public async Task Class_Manager_Should_Not_Take_Over_Other_Class_Policy_Family_By_Creating_A_Retargeted_Version()
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var context = await SeedAsync(tenantId);
		using (Principal(context.ReaderId, tenantId))
		{
			var replacement = Draft("Retargeted successor", context.StageId, context.Class5Id);
			replacement.PolicyId = context.Class6Policy.PolicyId;
			var error = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<ILibraryBorrowingPolicyAppService>().CreateAsync(context.YearId, replacement));
			error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<LibraryBorrowingPolicyVersion, Guid>>()
				.CountAsync(x => x.PolicyId == context.Class6Policy.PolicyId)).ShouldBe(1));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Class_Manager_Should_Not_Change_Legacy_Global_Quotas_Or_Opening_Hours(bool borrowingRules)
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var context = await SeedAsync(tenantId);
		using var reader = Principal(context.ReaderId, tenantId);

		var error = await Should.ThrowAsync<BusinessException>(async () =>
		{
			if (borrowingRules)
				await GetRequiredService<IBorrowingRuleAppService>().UpdateRulesAsync(context.YearId, new()
				{ Rules = [new() { Role = 0, MaxBooksAtTime = 99, ReservationLimit = 99 }] });
			else
				await GetRequiredService<ILibraryTimingAppService>().UpdateHoursAsync(context.YearId, new()
				{ Hours = [new() { Day = "Monday", StartTime = "01:00", EndTime = "23:00", Status = 0 }] });
		});

		error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Legacy_Settings_Readers_Should_Reject_Unknown_Academic_Context(bool borrowingRules)
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var context = await SeedAsync(tenantId);
		using var reader = Principal(context.ReaderId, tenantId);

		await Should.ThrowAsync<BusinessException>(async () =>
		{
			if (borrowingRules) await GetRequiredService<IBorrowingRuleAppService>().GetListAsync(Guid.NewGuid());
			else await GetRequiredService<ILibraryTimingAppService>().GetListAsync(Guid.NewGuid());
		});
	}

	[Fact]
	public async Task Scope_Options_And_Calendar_Capabilities_Should_Not_Offer_Global_Or_Other_Class_Changes()
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var context = await SeedAsync(tenantId);
		using var reader = Principal(context.ReaderId, tenantId);

		var options = await GetRequiredService<ILibraryBorrowingPolicyAppService>().GetOptionsAsync(context.YearId);
		var calendar = await GetRequiredService<ILibraryCalendarAppService>().GetSettingsAsync(context.YearId);

		options.CanManageGlobalPolicies.ShouldBeFalse();
		options.Standards.Select(x => x.Id).ShouldContain(context.Class5Id);
		options.Standards.Select(x => x.Id).ShouldNotContain(context.Class6Id);
		options.Stages.ShouldBeEmpty();
		calendar.CanManageCalendar.ShouldBeFalse();
		await Should.ThrowAsync<BusinessException>(() => GetRequiredService<ILibraryCalendarAppService>()
			.UpdateSettingsAsync(new() { TimeZoneId = "America/New_York" }));
	}

	private async Task<ScopedContext> SeedAsync(Guid tenantId)
	{
		using var admin = Principal(Guid.NewGuid(), tenantId);
		var initial = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var readerId = Guid.NewGuid();
		var placement = await WithUnitOfWorkAsync(async () =>
		{
			var board = new EducationBoard(Guid.NewGuid(), tenantId, EducationBoardType.CBSE, initial.YearId);
			await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
			var medium = new EducationMedium(Guid.NewGuid(), tenantId, "English", board.Id);
			await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
			var stage = new EducationMediumStage(Guid.NewGuid(), tenantId, "Middle school", null, medium.Id);
			await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
			var class5 = new Standard(Guid.NewGuid(), tenantId, "5", 10, stage.Id, null);
			var class6 = new Standard(Guid.NewGuid(), tenantId, "6", 11, stage.Id, null);
			await GetRequiredService<IRepository<Standard, Guid>>().InsertManyAsync([class5, class6], true);
			var login = $"policy-reader-{readerId:N}";
			await GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(readerId, login, login + "@example.test", tenantId), true);
			await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId,
				userId: readerId, academicYearId: initial.YearId, educationBoardId: board.Id, educationMediumId: medium.Id,
				educationMediumStageId: stage.Id, standardId: class5.Id), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(readerId);
			return (stage.Id, Class5Id: class5.Id, Class6Id: class6.Id);
		});
		var policies = GetRequiredService<ILibraryBorrowingPolicyAppService>();
		var global = await policies.CreateAsync(initial.YearId, Draft("Global"));
		var stagePolicy = await policies.CreateAsync(initial.YearId, Draft("Stage", placement.Id));
		var class5Policy = await policies.CreateAsync(initial.YearId, Draft("Class 5", placement.Id, placement.Class5Id));
		var class6Policy = await policies.CreateAsync(initial.YearId, Draft("Class 6", placement.Id, placement.Class6Id));
		return new(initial.YearId, readerId, placement.Id, placement.Class5Id, placement.Class6Id, global, stagePolicy, class5Policy, class6Policy);
	}

	private IDisposable Principal(Guid userId, Guid tenantId) => GetRequiredService<ICurrentPrincipalAccessor>().Change(
		new ClaimsPrincipal(new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, userId.ToString()),
			new Claim(AbpClaimTypes.TenantId, tenantId.ToString())], "LibraryPolicyTest")));

	private static LibraryBorrowingPolicyDraftRequest Draft(string name, Guid? stageId = null, Guid? standardId = null) => new()
	{
		Name = name, Role = 0, EffectiveFrom = new(2026, 11, 1), EducationMediumStageId = stageId, StandardId = standardId,
		Terms = new() { MaxBooksAtTime = 2, ReservationLimit = 1, LoanDays = 7, MaxRenewals = 0, RenewalDays = 0, PickupWindowDays = 2 }
	};

	private sealed record ScopedContext(Guid YearId, Guid ReaderId, Guid StageId, Guid Class5Id, Guid Class6Id,
		LibraryBorrowingPolicyVersionDto GlobalPolicy, LibraryBorrowingPolicyVersionDto StagePolicy,
		LibraryBorrowingPolicyVersionDto Class5Policy, LibraryBorrowingPolicyVersionDto Class6Policy);
}
