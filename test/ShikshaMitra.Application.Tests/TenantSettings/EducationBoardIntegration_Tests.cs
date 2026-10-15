using System;
using System.Threading.Tasks;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class EducationBoardIntegration_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Supported_boards_persist_and_deduplicate_within_the_year_and_reject_unsupported_types()
	{
		var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var boards = GetRequiredService<IEducationBoardRepository>();
		var manager = GetRequiredService<EducationBoardManager>();
		Guid boardId = Guid.Empty;
		Guid yearId = Guid.Empty;
		await WithUnitOfWorkAsync(async () =>
		{
			var year = await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, "Board test year", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
			yearId = year.Id;
			var first = await boards.InsertAsync(await manager.CreateAsync(yearId, EducationBoardType.GSEB), true);
			boardId = first.Id;
			await boards.InsertAsync(await manager.CreateAsync(yearId, EducationBoardType.CBSE), true);
			await Should.ThrowAsync<AlreadyExistsException>(() => manager.CreateAsync(yearId, EducationBoardType.GSEB));
			await Should.ThrowAsync<AlreadyExistsException>(() => manager.CreateAsync(yearId, EducationBoardType.CBSE));
			for (var type = 2; type <= 6; type++)
			{
				(await Should.ThrowAsync<BusinessException>(() => manager.CreateAsync(yearId, (EducationBoardType)type))).Code.ShouldBe(EducationBoardNames.InvalidType);
				(await Should.ThrowAsync<BusinessException>(() => manager.UpdateAsync(first, (EducationBoardType)type))).Code.ShouldBe(EducationBoardNames.InvalidType);
			}
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var restored = await boards.GetAsync(boardId);
			restored.CustomName.ShouldBeNull();
			restored.Name.ShouldBe("GSEB");
			await manager.UpdateAsync(restored, EducationBoardType.GSEB, customName: "Regional Board");
			await boards.UpdateAsync(restored, true);
		});
		await WithUnitOfWorkAsync(async () => (await boards.GetAsync(boardId)).Name.ShouldBe("GSEB"));
	}
}
