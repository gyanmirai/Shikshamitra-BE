using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentCatalogOrder_Tests : ShikshaMitraApplicationTestBase<AcademicChildOrderTestModule>
{
	private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	[Fact]
	public async Task Assessment_catalog_uses_saved_stage_and_class_order_for_preprimary_numbered_and_stream_classes()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var principal = SignIn();
		await WithUnitOfWorkAsync(async () =>
		{
			var medium = await SeedMediumAsync();
			var early = Stage(medium.Id, "Zulu Early Years", 0);
			var primary = Stage(medium.Id, "Middle School", 2);
			var senior = Stage(medium.Id, "Alpha Senior School", 10);
			foreach (var stage in new[] { senior, primary, early })
				await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
			var expected = new List<Standard>
			{
				Class(early.Id, "Nursery", 0), Class(early.Id, "LKG", 1), Class(early.Id, "UKG", 2)
			};
			expected.AddRange(Enumerable.Range(1, 10).Select(grade => Class(primary.Id, $"Std {grade}", grade)));
			expected.AddRange(new[]
			{
				Class(senior.Id, "Std 11 Arts", 0), Class(senior.Id, "Std 11 Com", 1),
				Class(senior.Id, "Std 12 Arts", 2), Class(senior.Id, "Std 12 Com", 3),
				Class(senior.Id, "Custom Prep", 4)
			});
			foreach (var standard in expected.AsEnumerable().Reverse())
				await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);

			var catalog = await GetRequiredService<AdmissionAssessmentCatalogService>().GetAsync();
			catalog.Classes.Where(item => item.MediumId == medium.Id).Select(item => item.Id)
				.ShouldBe(expected.Select(item => item.Id));
		});
	}

	[Fact]
	public async Task Assessment_catalog_uses_stage_name_and_id_then_class_name_and_id_when_saved_positions_tie()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var principal = SignIn();
		await WithUnitOfWorkAsync(async () =>
		{
			var medium = await SeedMediumAsync();
			var first = Stage(medium.Id, "Alpha", 0, Guid.Parse("00000000-0000-0000-0000-000000000001"));
			var second = Stage(medium.Id, "Alpha", 0, Guid.Parse("00000000-0000-0000-0000-000000000002"));
			var last = Stage(medium.Id, "Zulu", 0);
			foreach (var stage in new[] { last, second, first })
				await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
			var expected = new[]
			{
				Class(first.Id, "Std 8", 0, Guid.Parse("00000000-0000-0000-0000-000000000011")),
				Class(first.Id, "Std 8", 0, Guid.Parse("00000000-0000-0000-0000-000000000012")),
				Class(second.Id, "Std 1", 0), Class(last.Id, "Std 10", 0), Class(last.Id, "Std 2", 0)
			};
			foreach (var standard in expected.Reverse())
				await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);

			var catalog = await GetRequiredService<AdmissionAssessmentCatalogService>().GetAsync();
			catalog.Classes.Where(item => item.MediumId == medium.Id).Select(item => item.Id)
				.ShouldBe(expected.Select(item => item.Id));
		});
	}

	private IDisposable SignIn() => GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
		new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()), new Claim(AbpClaimTypes.TenantId, TenantId.ToString())], "Test")));

	private async Task<EducationMedium> SeedMediumAsync()
	{
		var year = await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(
			new(Guid.NewGuid(), TenantId, $"Assessment {Guid.NewGuid():N}", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
		var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(
			new(Guid.NewGuid(), TenantId, EducationBoardType.CBSE, year.Id), true);
		return await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(
			new(Guid.NewGuid(), TenantId, "English", board.Id), true);
	}

	private static EducationMediumStage Stage(Guid mediumId, string name, int order, Guid? id = null)
	{
		var stage = new EducationMediumStage(id ?? Guid.NewGuid(), TenantId, name, null, mediumId);
		stage.SetSortOrder(order);
		return stage;
	}

	private static Standard Class(Guid stageId, string name, int order, Guid? id = null)
	{
		var standard = new Standard(id ?? Guid.NewGuid(), TenantId, name, 6, stageId, null);
		standard.SetSortOrder(order);
		return standard;
	}
}
