using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ConcessionTypeEntity = ShikshaMitra.Finance.Fees.Settings.ConcessionTypes.ConcessionType;
using ConcessionRequestEntity = ShikshaMitra.Finance.Fees.ConcessionRequests.ConcessionRequest;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeSettingsReadPerformance_Tests(ITestOutputHelper output) : ShikshaMitraApplicationTestBase<FinanceSettingsReadTestModule>
{
	[Fact]
	public async Task Dependency_counts_preserve_selected_zero_heads_legacy_dues_and_corrupt_snapshot_fallback()
	{
		var year = Guid.NewGuid();
		var selected = Guid.NewGuid();
		var excluded = Guid.NewGuid();
		var structure = Guid.NewGuid();
		var otherStructure = Guid.NewGuid();
		var students = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
		await WithUnitOfWorkAsync(async () =>
		{
			var income = await FeeHeadAccountingTestData.CreateIncomeCategoryAsync(
				GetRequiredService<IRepository<AccountHead, Guid>>(),
				GetRequiredService<IRepository<TransactionCategory, Guid>>(), year, null, "Dependency");
			var category = new FeeCategory(Guid.NewGuid(), null, year, "Dependency category", null);
			await GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(category, true);
			await GetRequiredService<IRepository<FeeHead, Guid>>().InsertManyAsync([
				new FeeHead(selected, null, year, category.Id, "Selected zero head", null, false, null, income),
				new FeeHead(excluded, null, year, category.Id, "Optional head", null, false, null, income)
			], true);
			var detail = new FeeStructureDetail(Guid.NewGuid(), null, structure, selected, 0m, false, false, null, null);
			detail.AttachPaymentTerm(Guid.NewGuid(), Guid.NewGuid());
			await GetRequiredService<IRepository<FeeStructure, Guid>>().InsertManyAsync([
				new FeeStructure(structure, null, Guid.NewGuid(), Guid.NewGuid(), "Dependency structure"),
				new FeeStructure(otherStructure, null, Guid.NewGuid(), Guid.NewGuid(), "Other dependency structure")
			], true);
			await GetRequiredService<IRepository<FeeStructureDetail, Guid>>().InsertManyAsync([
				detail,
				new FeeStructureDetail(Guid.NewGuid(), null, structure, excluded, 0m, false, true, null, null),
				new FeeStructureDetail(Guid.NewGuid(), null, otherStructure, selected, 0m, false, false, null, null)
			], true);
			var explicitAssignment = new StudentFeeStructure(Guid.NewGuid(), null, students[0], structure, year);
			explicitAssignment.CaptureAssignment("selected", JsonSerializer.Serialize(new {
				Heads = new[] { new { FeeHeadId = selected, Included = true }, new { FeeHeadId = excluded, Included = false } }
			}));
			var corrupt = new StudentFeeStructure(Guid.NewGuid(), null, students[1], structure, year);
			corrupt.CaptureAssignment("corrupt", "{invalid");
			var empty = new StudentFeeStructure(Guid.NewGuid(), null, students[3], structure, year);
			empty.CaptureAssignment("empty", "{\"Heads\":[]}");
			await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().InsertManyAsync([
				explicitAssignment, corrupt, empty,
				new StudentFeeStructure(Guid.NewGuid(), null, students[2], structure, year),
				new StudentFeeStructure(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), structure, year)
			], true);
			DueFee Due(Guid student, MonthEnum month) => new(Guid.NewGuid(), null, student, selected, month,
				2026, false, 0m, new DateTime(2026, 4, 1));
			var bound = Due(students[0], MonthEnum.April);
			bound.BindAssignment(explicitAssignment.Id);
			await GetRequiredService<IRepository<DueFee, Guid>>().InsertManyAsync([
				bound, Due(students[0], MonthEnum.May), Due(students[4], MonthEnum.April), Due(students[4], MonthEnum.May)
			], true);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var result = await GetRequiredService<FeeHeadDependencyQuery>().GetAsync([selected, excluded, selected]);
			result.Count.ShouldBe(2);
			result[selected].StudentCount.ShouldBe(4);
			result[selected].AssignmentCount.ShouldBe(4);
			result[selected].FeeStructureCount.ShouldBe(2);
			result[selected].PaymentTermCount.ShouldBe(1);
			result[excluded].StudentCount.ShouldBe(2);
			result[excluded].AssignmentCount.ShouldBe(2);
			result[excluded].FeeStructureCount.ShouldBe(1);
			result[excluded].PaymentTermCount.ShouldBe(0);
		});
	}

	[Fact]
	public async Task Dependency_lookup_reads_reference_columns_without_loading_customized_schedules()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
			var result = await GetRequiredService<FeeHeadDependencyQuery>().GetAsync([Guid.NewGuid()]);
			var dependencies = result.Values.Single();
			dependencies.StudentCount.ShouldBe(0);
			dependencies.AssignmentCount.ShouldBe(0);
			dependencies.FeeStructureCount.ShouldBe(0);
			dependencies.PaymentTermCount.ShouldBe(0);
			reads.Commands.ShouldNotContain(sql => sql.Contains("CoverageStartDate", StringComparison.Ordinal),
				"dependency checks need reference IDs, not customized installment schedules");
		});
	}

	[Fact]
	public async Task Filtered_head_page_limits_the_database_read_before_loading_dependencies()
	{
		var yearId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var incomeId = await FeeHeadAccountingTestData.CreateIncomeCategoryAsync(
				GetRequiredService<IRepository<AccountHead, Guid>>(),
				GetRequiredService<IRepository<TransactionCategory, Guid>>(), yearId, null, "Filtered");
			var category = new FeeCategory(Guid.NewGuid(), null, yearId, "Filtered category", null);
			await GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(category, true);
			for (var index = 0; index < 12; index++)
				await GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(new FeeHead(
					Guid.NewGuid(), null, yearId, category.Id, $"Matching {index}", null, true, "Preserved", incomeId), true);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
			var result = await GetRequiredService<IFeeHeadAppService>().GetListAsync(yearId, page: 2, pageSize: 1,
				filters: """[{"column":"name","type":"contains","value":"MATCHING"}]""");
			output.WriteLine($"Filtered head page SELECT count: {reads.Count}");
			result.TotalCount.ShouldBe(12);
			result.Items.Single().FeeCategoryName.ShouldBe("Filtered category");
			result.Items.Single().IncomeAccountHeadName.ShouldBe("Filtered Revenue");
			reads.Commands.ShouldContain(sql => sql.Contains("FROM \"Finance_FeeHeads\"", StringComparison.Ordinal) &&
				sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase),
				"column-filtered lists must page in SQL rather than enrich every matching head");
		});
	}

	[Fact]
	public async Task Concession_type_list_checks_usage_without_loading_request_document_and_allocation_graphs()
	{
		var yearId = Guid.NewGuid();
		var usedId = Guid.NewGuid();
		var historicalId = Guid.NewGuid();
		var unusedId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<ConcessionTypeEntity, Guid>>().InsertManyAsync([
				new ConcessionTypeEntity(unusedId, null, yearId, "Performance type", "Preserved type"),
				new ConcessionTypeEntity(usedId, null, yearId, "Used type", null),
				new ConcessionTypeEntity(historicalId, null, yearId, "Historical type", null),
				new ConcessionTypeEntity(Guid.NewGuid(), null, Guid.NewGuid(), "Other year type", null)
			], true);
			var historical = Request(historicalId);
			historical.IsDeleted = true;
			await GetRequiredService<IRepository<ConcessionRequestEntity, Guid>>().InsertManyAsync(
				[Request(usedId), historical, Request(unusedId, Guid.NewGuid())], true);

			ConcessionRequestEntity Request(Guid typeId, Guid? tenantId = null) => new(Guid.NewGuid(), tenantId,
				Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), typeId, yearId,
				Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
			var result = await GetRequiredService<IConcessionTypeAppService>().GetListAsync(yearId);
			output.WriteLine($"Concession type page SELECT count: {reads.Count}");
			result.TotalCount.ShouldBe(3);
			result.Items.Single(x => x.Id == unusedId).Name.ShouldBe("Performance type");
			result.Items.Single(x => x.Id == unusedId).IsUsedInDownstream.ShouldBeFalse();
			result.Items.Single(x => x.Id == usedId).IsUsedInDownstream.ShouldBeTrue();
			result.Items.Single(x => x.Id == historicalId).IsUsedInDownstream.ShouldBeTrue();
			reads.Commands.ShouldNotContain(sql => sql.Contains("ConcessionRequestDocuments", StringComparison.Ordinal),
				"usage flags must not hydrate request attachments");
			reads.Commands.ShouldNotContain(sql => sql.Contains("ConcessionDueFeeAllocations", StringComparison.Ordinal),
				"usage flags must not hydrate allocation history");
		});
	}

	[Fact]
	public async Task Category_page_uses_bounded_reads_and_preserves_head_accounting_and_dependency_details()
	{
		var yearId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var incomeId = await FeeHeadAccountingTestData.CreateIncomeCategoryAsync(
				GetRequiredService<IRepository<AccountHead, Guid>>(),
				GetRequiredService<IRepository<TransactionCategory, Guid>>(), yearId, null, "Performance");
			for (var index = 0; index < 10; index++)
			{
				var category = new FeeCategory(Guid.NewGuid(), null, yearId, $"Category {index}", "Preserved description");
				await GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(category, true);
				await GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(new FeeHead(
					Guid.NewGuid(), null, yearId, category.Id, $"Head {index}", null, true, "Preserved head", incomeId), true);
			}
		});

		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
			var result = await GetRequiredService<IFeeCategoryAppService>().GetListAsync(yearId, pageSize: 10, sortBy: "name");
			output.WriteLine($"Category page SELECT count: {reads.Count}");
			result.TotalCount.ShouldBe(10);
			result.Items.Count.ShouldBe(10);
			foreach (var category in result.Items)
			{
				category.Description.ShouldBe("Preserved description");
				category.IsUsedInDownstream.ShouldBeFalse();
				var head = category.FeeHeads.Single();
				head.FeeCategoryName.ShouldBe(category.Name);
				head.IncomeCategoryName.ShouldStartWith("Performance Income");
				head.IncomeAccountHeadName.ShouldBe("Performance Revenue");
				head.Dependencies.StudentCount.ShouldBe(0);
				head.IsUsedInDownstream.ShouldBeFalse();
			}
			reads.Count.ShouldBeLessThanOrEqualTo(10, "a page must batch dependency and accounting reads, not repeat them per category");
		});
	}
}
