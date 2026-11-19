using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class DivisionOnboardingIntegration_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Creating_a_division_without_a_room_field_persists_an_unassigned_room()
	{
		var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()),
			new Claim(AbpClaimTypes.TenantId, tenantId.ToString()),
			new Claim(AbpClaimTypes.Role, "admin")], "Test")));
		Guid divisionId = Guid.Empty;
		await WithUnitOfWorkAsync(async () =>
		{
			var year = await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, "Division setup year", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, EducationBoardType.CBSE, year.Id), true);
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, "English", board.Id), true);
			var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, "Primary", null, medium.Id), true);
			var standard = await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, "Class 1", 6, stage.Id, null), true);
			var json = JsonSerializer.Serialize(new { name = "B", strength = 50, standardId = standard.Id });
			var input = JsonSerializer.Deserialize<CreateUpdateDivisionDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
			input.RoomNumber.ShouldBeNull();
			var created = await GetRequiredService<IDivisionAppService>().CreateAsync(input);
			divisionId = created.Id;
			created.Name.ShouldBe("B");
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var restored = await GetRequiredService<IRepository<Division, Guid>>().GetAsync(divisionId);
			restored.RoomNumber.ShouldBe(string.Empty);
			restored.Strength.ShouldBe(50);
		});
	}
}
