using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Settings.ChargeCategories;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
	[Fact]
	public async Task Optimized_reads_preserve_customized_schedule_publication_evidence_and_revision_pins()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (metadata, standard, candidate) = await ReadyCandidateAsync();
		var line = candidate.FeeHeads.Single();
		line.Amount = 1000m;
		var term = await GetRequiredService<IRepository<PaymentTerm, Guid>>().GetAsync(line.PaymentTermId!.Value);
		var installment = term.Versions.Single(x => x.Id == line.PaymentTermVersionId).Installments.OrderBy(x => x.Sequence).First();
		line.ScheduleMode = ScheduleMode.Customized;
		line.InstallmentOverrides = [new ScheduleOverrideDto {
			SourcePaymentTermInstallmentId = installment.Id, Sequence = installment.Sequence,
			Name = installment.Name, DueDate = installment.DueDate.AddDays(2)
		}];
		var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
		var preview = await _feeStructures.PreviewAsync(new() {
			FeeStructureMetadataId = metadata, StandardId = standard, StructureId = draft.Id, StandardFeeStructures = AsRequest(draft)
		});
		preview.Errors.ShouldBeEmpty();
		preview.Schedules.Single().Installments.Sum(x => x.CalculatedAmount).ShouldBe(1000m);
		preview.Schedules.Single().Installments.First().DueDate.ShouldBe(installment.DueDate.AddDays(2));
		var published = await _feeStructures.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp });
		published.PublicationReview.ShouldNotBeNull();
		var persisted = (await _feeStructures.GetByStandardAsync(standard)).StandardFeeStructures.Single(x => x.Id == published.Id);
		JsonSerializer.Serialize(persisted.PublicationReview).ShouldBe(JsonSerializer.Serialize(published.PublicationReview));
		await Should.ThrowAsync<BusinessException>(() => _feeStructures.UpdateAsync(published.Id, AsRequest(published)));
		var revision = await _feeStructures.CreateRevisionAsync(published.Id, new() { ConcurrencyStamp = published.ConcurrencyStamp });
		revision.FeeHeads.Single().Amount.ShouldBe(1000m);
		revision.FeeHeads.Single().PaymentTermVersionId.ShouldBe(line.PaymentTermVersionId);
		revision.FeeHeads.Single().ScheduleMode.ShouldBe(ScheduleMode.Customized);
		revision.FeeHeads.Single().InstallmentOverrides.First().DueDate.ShouldBe(installment.DueDate.AddDays(2));
		revision.CollectionPolicyVersionId.ShouldBe(published.CollectionPolicyVersionId);
		revision.PublicationReview.ShouldBeNull();
	}

	[Fact]
	public async Task Preview_reuses_shared_term_and_income_validation_without_changing_installments()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (metadata, standard, candidate) = await ReadyCandidateAsync();
		var first = candidate.FeeHeads.Single();
		first.Amount = 1000m;
		var head = await _feeHeadRepo.GetAsync(first.FeeHeadId);
		var extraHeads = Enumerable.Range(1, 9).Select(index => new FeeHead(Guid.NewGuid(), TestTenantId,
			head.AcademicYearId, head.FeeCategoryId, $"Shared validation head {index}", head.Module, true, null, head.IncomeCategoryId)).ToList();
		await _feeHeadRepo.InsertManyAsync(extraHeads, true);
		candidate.FeeHeads.AddRange(extraHeads.Select(extra => new FeeHeadAssignmentRequest {
			FeeHeadId = extra.Id, Amount = 1000m, PaymentTermId = first.PaymentTermId, PaymentTermVersionId = first.PaymentTermVersionId
		}));
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
			var result = await _feeStructures.PreviewAsync(new() {
				FeeStructureMetadataId = metadata, StandardId = standard, StandardFeeStructures = candidate
			});
			result.Errors.ShouldBeEmpty();
			result.Schedules.Count.ShouldBe(10);
			result.Schedules.ShouldAllBe(x => x.Installments.Count == 2 && x.Installments.Sum(i => i.CalculatedAmount) == 1000m);
			reads.Commands.Count(sql => sql.Contains("FROM \"Finance_PaymentTerms\"", StringComparison.Ordinal))
				.ShouldBeLessThanOrEqualTo(2, "shared term versions must not be loaded twice for every head");
			reads.Commands.Count(sql => sql.Contains("FROM \"Finance_TransactionCategories\"", StringComparison.Ordinal))
				.ShouldBeLessThanOrEqualTo(2, "heads sharing an Income Category must reuse its validation");
		});
	}

	[Fact]
	public async Task Metadata_pages_visible_containers_before_loading_class_configuration()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, standard, metadataId, _) = await SeedStandardAsync();
		var original = await _metadataRepo.GetAsync(metadataId);
		for (var index = 0; index < 10; index++)
			await _metadataRepo.InsertAsync(new FeeStructureMetadata(Guid.NewGuid(), TestTenantId, year,
				original.AcademicBoardId!.Value, original.AcademicMediumId!.Value, null, $"Performance metadata {index}"), true);
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
			var result = await GetRequiredService<IFeeStructureMetadataAppService>().GetListAsync(year, 2, 3, "Performance metadata");
			result.TotalCount.ShouldBe(10);
			result.Items.Count.ShouldBe(3);
			result.Items.ShouldAllBe(x => x.TotalClasses == 1 && x.ConfiguredClasses == 0 && x.Classes.Single().Id == standard);
			reads.Commands.ShouldContain(sql => sql.Contains("FROM \"Finance_FeeStructureMetadatas\"", StringComparison.Ordinal) && sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase),
				"only the requested visible metadata page should be materialized");
		});
	}

	[Fact]
	public async Task Preview_does_not_materialize_unrelated_policy_snapshots()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (metadata, standard, candidate) = await ReadyCandidateAsync();
		candidate.FeeHeads.Single().Amount = 1000m;
		var head = await _feeHeadRepo.GetAsync(candidate.FeeHeads.Single().FeeHeadId);
		var unrelated = await CreateCollectionPolicyAsync(head.AcademicYearId, head.Id, "Unrelated collection");
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			var result = await _feeStructures.PreviewAsync(new() {
				FeeStructureMetadataId = metadata, StandardId = standard, StandardFeeStructures = candidate
			});
			result.Errors.ShouldBeEmpty();
			result.Schedules.Single().AnnualAmount.ShouldBe(1000m);
			db.ChangeTracker.Entries<FinancePolicyVersion>().ShouldNotContain(x => x.Entity.PolicyId == unrelated,
				"an unrelated policy snapshot must not be materialized for this structure's validation");
		});
	}

	[Fact]
	public async Task Charge_category_page_shares_accounting_labels_instead_of_querying_each_row()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, _, headId) = await SeedStandardAsync();
		var head = await _feeHeadRepo.GetAsync(headId);
		var repository = GetRequiredService<IRepository<ChargeCategory, Guid>>();
		for (var index = 0; index < 10; index++)
		{
			var category = new ChargeCategory(Guid.NewGuid(), TestTenantId, year, $"Performance charge {index}", false, "Preserved charge");
			category.SetAccountingMapping(head.IncomeCategoryId);
			await repository.InsertAsync(category, true);
		}
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
			var result = await GetRequiredService<IChargeCategoryAppService>().GetListAsync(year, page: 2, pageSize: 3);
			result.TotalCount.ShouldBe(10);
			result.Items.Count.ShouldBe(3);
			result.Items.ShouldAllBe(x => x.IncomeCategoryId == head.IncomeCategoryId && x.IncomeCategoryName != null && x.IncomeCategoryName.StartsWith("Structure Tuition Income ", StringComparison.Ordinal) && x.Description == "Preserved charge");
			reads.Commands.Count(sql => sql.Contains("FROM \"Finance_TransactionCategories\"", StringComparison.Ordinal))
				.ShouldBeLessThanOrEqualTo(1, "a page should share its Income Category lookup");
		});
	}

	[Fact]
	public async Task Class_read_batches_shared_head_and_term_labels_without_changing_fee_configuration()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (metadata, standard, candidate) = await ReadyCandidateAsync();
		candidate.FeeHeads.Single().Amount = 1000m;
		for (var index = 0; index < 10; index++)
		{
			candidate.Name = $"Performance structure {index}";
			await _feeStructures.CreateAsync(metadata, new StandardFeeStructureSaveRequest
			{
				StandardId = standard, StandardFeeStructures = candidate
			});
		}
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
			var result = await _feeStructures.GetByStandardAsync(standard);
			result.StandardFeeStructures.Count.ShouldBe(10);
			foreach (var structure in result.StandardFeeStructures)
			{
				var head = structure.FeeHeads.Single();
				head.Amount.ShouldBe(1000m);
				head.FeeHeadName.ShouldBe("Tuition Fee");
				head.PaymentTermName.ShouldBe("Annual");
				head.PaymentTermVersionNumber.ShouldBe(1);
				structure.PolicyLabels["collection"].ShouldContain("v1");
			}
			var referenceReads = reads.Commands.Count(sql =>
				sql.Contains("FROM \"Finance_FeeHeads\"", StringComparison.Ordinal) ||
				sql.Contains("FROM \"Finance_PaymentTerms\"", StringComparison.Ordinal));
			referenceReads.ShouldBeLessThanOrEqualTo(4,
				"ten structures must share reference lookups instead of loading heads and term history per structure");
		});
	}
}
