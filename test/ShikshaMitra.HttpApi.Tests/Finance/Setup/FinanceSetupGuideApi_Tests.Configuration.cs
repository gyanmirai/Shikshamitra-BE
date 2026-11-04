using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Setup;

public partial class FinanceSetupGuideApi_Tests
{
	[Fact]
	public async Task Class_only_scope_does_not_count_a_sibling_class_structure_on_the_same_board()
	{
		var school = await SeedAsync(); var classes = await AddClassesAsync(school);
		await AddPublishedStructureAsync(school, classes.Second);
		await InAsync(school.TenantId, async services => {
			var assignments = services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await assignments.DeleteAsync(x => x.UserId == school.ActorId, true);
			var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(school.MediumId);
			await assignments.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), school.TenantId, userId: school.ActorId,
				academicYearId: school.YearId, educationBoardId: medium.EducationBoardId, educationMediumId: school.MediumId,
				educationMediumStageId: classes.Stage, standardId: classes.First), true);
		});
		using var client = As(school, "Finance.Settings.FeeStructure.View");
		Setting(await ReadAsync(client, StateUrl(school)), "fee-structures").GetProperty("configurationState").GetString().ShouldBe("empty");
		await AddPublishedStructureAsync(school, classes.First);
		Setting(await ReadAsync(client, StateUrl(school)), "fee-structures").GetProperty("configurationState").GetString().ShouldBe("configured");
	}

	[Fact]
	public async Task Fee_category_presence_requires_an_active_head_belonging_to_that_active_category_and_year()
	{
		var school = await SeedAsync(); Guid categoryId = default; Guid incomeId = default;
		await InAsync(school.TenantId, async services => {
			var category = new FeeCategory(Guid.NewGuid(), school.TenantId, school.YearId, "Tuition", null);
			await services.GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(category, true); categoryId = category.Id;
			var income = new TransactionCategory(Guid.NewGuid(), school.TenantId, school.YearId, TransactionType.Income, "Tuition mapping", null);
			await services.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(income, true); incomeId = income.Id;
		});
		using var client = As(school, "Finance.Settings.FeeCategory.View");
		Setting(await ReadAsync(client, StateUrl(school)), "fee-categories").GetProperty("configurationState").GetString().ShouldBe("empty");
		Guid headId = default;
		await InAsync(school.TenantId, async services => {
			var head = new FeeHead(Guid.NewGuid(), school.TenantId, school.YearId, categoryId, "Tuition head", null, false, null, incomeId);
			await services.GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(head, true); headId = head.Id;
		});
		Setting(await ReadAsync(client, StateUrl(school)), "fee-categories").GetProperty("configurationState").GetString().ShouldBe("configured");
		await InAsync(school.TenantId, async services => {
			var heads = services.GetRequiredService<IRepository<FeeHead, Guid>>(); var head = await heads.GetAsync(headId);
			head.SetActiveStatus(false, "fixture inactive"); await heads.UpdateAsync(head, true);
		});
		Setting(await ReadAsync(client, StateUrl(school)), "fee-categories").GetProperty("configurationState").GetString().ShouldBe("empty");
	}

	private async Task<(Guid Stage, Guid First, Guid Second)> AddClassesAsync(School school)
	{
		(Guid, Guid, Guid) result = default;
		await InAsync(school.TenantId, async services => {
			var stage = await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(school.MediumId, "Primary");
			await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
			var manager = services.GetRequiredService<StandardManager>();
			var first = await manager.CreateAsync(stage.Id, "Class 1", 5, null);
			var second = await manager.CreateAsync(stage.Id, "Class 2", 6, null);
			await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(first, true);
			await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(second, true);
			result = (stage.Id, first.Id, second.Id);
		}); return result;
	}

	private async Task AddPublishedStructureAsync(School school, Guid classId)
	{
		await InAsync(school.TenantId, async services => {
			var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(school.MediumId);
			var suffix = Guid.NewGuid().ToString("N");
			var category = new FeeCategory(Guid.NewGuid(), school.TenantId, school.YearId, "Fees " + suffix, null);
			await services.GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(category, true);
			var income = new TransactionCategory(Guid.NewGuid(), school.TenantId, school.YearId, TransactionType.Income, "Income " + suffix, null);
			await services.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(income, true);
			var head = new FeeHead(Guid.NewGuid(), school.TenantId, school.YearId, category.Id, "Head " + suffix, null, false, null, income.Id);
			await services.GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(head, true);
			var container = new FeeStructureMetadata(Guid.NewGuid(), school.TenantId, school.YearId, medium.EducationBoardId, school.MediumId, null, "Container " + suffix);
			await services.GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(container, true);
			var structure = new FeeStructure(Guid.NewGuid(), school.TenantId, container.Id, classId, "Published " + suffix);
			structure.AddDetail(new FeeStructureDetail(Guid.NewGuid(), school.TenantId, structure.Id, head.Id, 100m, false, false, null, null));
			structure.Lock(); await services.GetRequiredService<IRepository<FeeStructure, Guid>>().InsertAsync(structure, true);
		});
	}
}
