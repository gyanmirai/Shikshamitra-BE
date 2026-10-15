using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class SchoolIdentityIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Descendant_scope_can_read_inheritance_but_cannot_manage_parent_profiles(bool stageOnly) => RunAsync(async (board, medium) =>
	{
		var year = (await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(board)).AcademicYearId;
		var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(
			new(Guid.NewGuid(), TenantId, "Scope test stage", null, medium), true);
		var actorId = Guid.NewGuid();
		await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(
			new(actorId, $"scoped-{actorId:N}", $"{actorId:N}@example.test", TenantId), true);
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new(
			Guid.NewGuid(), TenantId, userId: actorId, academicYearId: year, educationBoardId: board,
			educationMediumId: medium, educationMediumStageId: stageOnly ? stage.Id : null), true);
		using var actor = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, actorId.ToString()), new Claim(AbpClaimTypes.UserName, $"scoped-{actorId:N}"),
			new Claim(AbpClaimTypes.TenantId, TenantId.ToString())], "Test")));
		ScopeLocalReuseInvalidator.ClearCurrentUnitOfWork(GetRequiredService<IUnitOfWorkManager>());
		var readable = await Service.GetAsync(new() { BoardId = board, MediumId = medium });
		readable.BoardId.ShouldBe(board);
		readable.CanManage.ShouldBe(!stageOnly);
		(await Service.GetAsync(new() { BoardId = board })).CanManage.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => Service.UpdateAsync(new() { BoardId = board })))
			.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		if (stageOnly)
			(await Should.ThrowAsync<BusinessException>(() => Service.UpdateAsync(new() { BoardId = board, MediumId = medium })))
				.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		else
			(await Service.UpdateAsync(new() { BoardId = board, MediumId = medium,
				Choices = new() { Banner = new() { Mode = SchoolIdentityAssetMode.Hidden } } }))
				.Choices.Banner.Mode.ShouldBe(SchoolIdentityAssetMode.Hidden);
	});
}
