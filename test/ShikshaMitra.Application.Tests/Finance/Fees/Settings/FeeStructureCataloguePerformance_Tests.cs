using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
 [Fact]
 public async Task Category_options_include_all_active_choices_and_the_saved_same_year_selection()
 {
  using var reviewer = Reviewer();
  using var tenant = _currentTenant.Change(TestTenantId);
  var (year, _, _, _) = await SeedStandardAsync();
  var repo = GetRequiredService<IRepository<FeeCategory, Guid>>();
  for (var index = 0; index < 25; index++) await repo.InsertAsync(new FeeCategory(Guid.NewGuid(), TestTenantId, year, $"Complete option {index}", null), true);
  await repo.InsertAsync(new FeeCategory(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), "Other year", null), true);
  var inactive = new FeeCategory(Guid.NewGuid(), TestTenantId, year, "Saved inactive", null);
  inactive.SetStatus(false, "Retired");
  await repo.InsertAsync(inactive, true);
  var service = GetRequiredService<IFeeCategoryAppService>();
  var options = await service.GetOptionsAsync(year);
  options.ShouldNotContain(x => x.Id == inactive.Id);
  (await service.GetOptionsAsync(year, inactive.Id)).ShouldContain(x => x.Id == inactive.Id && !x.IsActive);
  options.Count(x => x.Name.StartsWith("Complete option")).ShouldBe(25);
  options.ShouldNotContain(x => x.Name == "Other year");
 }

 [Fact]
 public async Task Navigator_summaries_omit_schedules_and_selected_detail_preserves_pins()
 {
  using var reviewer = Reviewer();
  using var tenant = _currentTenant.Change(TestTenantId);
  var (metadata, standard, candidate) = await ReadyCandidateAsync();
  candidate.FeeHeads.Single().Amount = 1000m;
  var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
  await WithUnitOfWorkAsync(async () => {
   var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
   using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
   var summaries = await _feeStructures.GetSummariesAsync(standard);
   summaries.Single().Id.ShouldBe(draft.Id);
   summaries.Single().ConcurrencyStamp.ShouldBe(draft.ConcurrencyStamp);
   var structureReads = reads.Commands.Where(sql => sql.Contains("FROM \"Finance_FeeStructures\"", StringComparison.Ordinal)).ToList();
   structureReads.ShouldNotBeEmpty("the navigator must read the scoped structure summaries");
   structureReads.ShouldNotContain(sql => sql.Contains("InstallmentOverrides") || sql.Contains("ExtraProperties"), "structure summary reads must not materialize aggregate schedules or publication evidence");
  });
  var detail = await _feeStructures.GetDetailAsync(draft.Id);
  detail.FeeHeads.Single().Amount.ShouldBe(1000m);
  detail.FeeHeads.Single().PaymentTermVersionId.ShouldBe(candidate.FeeHeads.Single().PaymentTermVersionId);
 }

 [Fact]
 public async Task Options_project_only_current_versions_without_usage_or_snapshot_graphs()
 {
  using var reviewer = Reviewer();
  using var tenant = _currentTenant.Change(TestTenantId);
  var (metadata, standard, candidate) = await ReadyCandidateAsync();
  // The candidate helper supplies the policy master ID; assignment validation
  // resolves its version later. Catalogue options must expose the current active
  // snapshot, not the candidate's deliberately unpinned nullable version field.
  var collection = await _collectionPolicies.GetAsync(candidate.CollectionPolicyId!.Value);
  collection.CurrentVersionId.ShouldNotBeNull();
  var expectedCollectionVersionId = collection.CurrentVersionId
   ?? throw new InvalidOperationException("The seeded active collection policy must have a current snapshot.");
  await WithUnitOfWorkAsync(async () => {
   var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
   using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
   var options = await _feeStructures.GetCatalogueAsync(metadata, standard);
   options.Terms.ShouldContain(x => x.VersionId == candidate.FeeHeads.Single().PaymentTermVersionId);
   var collectionOption = options.Policies.Single(x => x.Family == "collection" && x.Id == collection.Id);
   collectionOption.VersionId.ShouldBe(expectedCollectionVersionId);
   collectionOption.VersionNumber.ShouldBe(collection.Version);
   collectionOption.IsCurrent.ShouldBeTrue();
   reads.Commands.ShouldNotContain(sql => sql.Contains("SnapshotJson") || sql.Contains("Finance_FeeStructureDetails"), "options need neither immutable snapshot bodies nor structure usage");
  });
 }
}
