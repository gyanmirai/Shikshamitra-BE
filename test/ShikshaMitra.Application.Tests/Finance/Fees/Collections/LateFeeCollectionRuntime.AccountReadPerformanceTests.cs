using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
	[Fact]
	public async Task Account_policy_reads_reuse_loaded_accounts_and_each_distinct_pinned_version()
	{
		Guid year = default;
		DateTime asOf = default;
		var expected = new Dictionary<Guid, decimal>();
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var seeded = await SeedStudentWithOverdueDueFeeAsync(10, true);
				year = seeded.AcademicYearId;
				var student = await _studentRepo.GetAsync(seeded.StudentId);
				var assignment = (await _studentFeeStructureRepo.GetListAsync(x => x.StudentId == student.Id)).Single();
				var originalDue = await _dueFeeRepo.GetAsync(seeded.DueFeeId);
				var originalVersion = await _policyVersionRepo.GetAsync(assignment.CollectionPolicyVersionId!.Value);
				var snapshot = CollectionPolicySnapshot.Deserialize(originalVersion.SnapshotJson);
				snapshot.AmountPerDay = 40m;
				var secondVersionId = Guid.NewGuid();
				await _policyVersionRepo.InsertAsync(new FinancePolicyVersion(secondVersionId, TestTenantId,
					"collection", originalVersion.PolicyId, year, 99, "Other pinned rate", "Active", snapshot.Serialize(), "changed"), true);
				asOf = originalDue.DueDate.AddDays(10);
				var accounts = GetRequiredService<IRepository<FinanceFeeAccount, Guid>>();
				for (var index = 0; index < 10; index++)
				{
					var account = new FinanceFeeAccount(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), Guid.NewGuid(), year,
						student.AcademicBoardId!.Value, student.AcademicMediumId, student.AcademicMediumStageId,
						Guid.NewGuid(), $"Applicant {index}");
					var versionId = index < 5 ? originalVersion.Id : secondVersionId;
					account.CaptureAssignment(seeded.FeeStructureId, 1, JsonSerializer.Serialize(new
					{
						RegistrationJoiningDate = originalDue.DueDate.AddDays(-1),
						Policies = new[] { new { Family = "collection", PolicyId = originalVersion.PolicyId, VersionId = versionId } }
					}), $"read-fixture-{index}");
					await accounts.InsertAsync(account, true);
					var due = new DueFee(Guid.NewGuid(), TestTenantId, null, originalDue.FeeHeadId,
						originalDue.Month, originalDue.Year, false, 5000m, originalDue.DueDate, financeFeeAccountId: account.Id);
					await _dueFeeRepo.InsertAsync(due, true);
					expected.Add(due.Id, index < 5 ? 200m : 400m);
				}
			}
		});
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var ids = expected.Keys.ToArray();
				var dues = await _dueFeeRepo.GetListAsync(x => Enumerable.Contains(ids, x.Id));
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
				var balances = await GetRequiredService<LateFeeBalanceService>().GetBalancesAsync(dues, year, asOf);
				balances.Count.ShouldBe(10);
				foreach (var item in expected)
				{
					balances[item.Key].Total.ShouldBe(item.Value);
					balances[item.Key].PendingAmount.ShouldBe(item.Value);
					balances[item.Key].PaidAmount.ShouldBe(0m);
					balances[item.Key].WaivedAmount.ShouldBe(0m);
				}
				int CountReads(Type type) => reads.Commands.Count(sql => sql.Contains(
					$"\"{db.Model.FindEntityType(type)!.GetTableName()}\"", StringComparison.Ordinal));
				var accountReads = CountReads(typeof(FinanceFeeAccount));
				var versionReads = CountReads(typeof(FinancePolicyVersion));
				var observed = $"Observed account/version reads: {accountReads}/{versionReads}.";
				accountReads.ShouldBeLessThanOrEqualTo(1, "Use the already-loaded accounts. " + observed);
				versionReads.ShouldBeLessThanOrEqualTo(2, "Read each distinct pinned version only once. " + observed);
			}
		});
	}
	[Fact]
	public async Task Balance_read_context_refreshes_between_calls_and_keeps_unsaved_override_values()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var seeded = await SeedStudentWithOverdueDueFeeAsync(10, true);
				var due = await _dueFeeRepo.GetAsync(seeded.DueFeeId);
				var adjustment = new StudentFeePolicyOverride(Guid.NewGuid(), TestTenantId, seeded.StudentId, seeded.FeeStructureId);
				await GetRequiredService<IRepository<StudentFeePolicyOverride, Guid>>().InsertAsync(adjustment, true);
				var service = GetRequiredService<LateFeeBalanceService>();
				var asOf = due.DueDate.AddDays(10);
				(await service.GetBalancesAsync([due], seeded.AcademicYearId, asOf))[due.Id].Total.ShouldBe(200m);
				adjustment.Update(true, null, false, null);
				(await service.GetBalancesAsync([due], seeded.AcademicYearId, asOf))[due.Id].Total.ShouldBe(0m);
				adjustment.Update(false, 5, false, null);
				(await service.GetBalancesAsync([due], seeded.AcademicYearId, asOf))[due.Id].Total.ShouldBe(100m);
				due.GetAssessedLateFeeAmount().ShouldBe(0m);
			}
		});
	}
	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	public async Task Summary_preserves_persisted_account_links_and_distinguishes_one_from_multiple_accounts(int accountCount)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var seeded = await SeedStudentWithOverdueDueFeeAsync(10, true);
				var student = await _studentRepo.GetAsync(seeded.StudentId);
				var assignment = (await _studentFeeStructureRepo.GetListAsync(x => x.StudentId == student.Id)).Single();
				var originalDue = await _dueFeeRepo.GetAsync(seeded.DueFeeId);
				var accounts = GetRequiredService<IRepository<FinanceFeeAccount, Guid>>();
				var accountIds = new List<Guid>();
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				for (var index = 0; index < accountCount; index++)
				{
					var account = new FinanceFeeAccount(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), Guid.NewGuid(), seeded.AcademicYearId,
						student.AcademicBoardId!.Value, student.AcademicMediumId, student.AcademicMediumStageId,
						Guid.NewGuid(), $"Original applicant {index}");
					account.CaptureAssignment(seeded.FeeStructureId, 1, JsonSerializer.Serialize(new
					{
						RegistrationJoiningDate = originalDue.DueDate.AddDays(-1),
						Policies = new[] { new { Family = "collection", PolicyId = assignment.CollectionPolicyId!.Value,
							VersionId = assignment.CollectionPolicyVersionId!.Value } }
					}), $"linked-read-{index}");
					await accounts.InsertAsync(account, true);
					var due = new DueFee(Guid.NewGuid(), TestTenantId, null, originalDue.FeeHeadId,
						originalDue.Month, originalDue.Year, false, 5000m, originalDue.DueDate, financeFeeAccountId: account.Id);
					await _dueFeeRepo.InsertAsync(due, true);
					accountIds.Add(account.Id);
				}
				// All writes are flushed before linking; these links intentionally remain tracked only.
				foreach (var id in accountIds)
				{
					var account = await accounts.GetAsync(id);
					account.LinkStudent(student.Id, DateTimeOffset.UtcNow);
					(await db.Set<FinanceFeeAccount>().Where(a => a.Id == id).Select(a => a.StudentId).SingleAsync()).ShouldBeNull();
				}
				var input = new ShikshaMitra.Finance.Fees.Collections.Dtos.GetFeeCollectionSummaryInput
				{
					AcademicYearId = seeded.AcademicYearId, BoardId = student.AcademicBoardId!.Value,
					EducationMediumId = student.AcademicMediumId, MaxResultCount = 100
				};
				var summary = await _feeCollections.GetSummaryAsync(input);
				summary.TotalCount.ShouldBe(accountCount + 1);
				summary.Items.Sum(x => x.TotalFees).ShouldBe((accountCount + 1) * 5000d);
				summary.Items.Sum(x => x.PendingFees).ShouldBe((accountCount + 1) * 5000d);
				summary.Items.Sum(x => x.TotalCharges).ShouldBe((accountCount + 1) * 200d);
				summary.Items.Where(x => x.FinanceFeeAccountId.HasValue).ShouldAllBe(x => x.StudentId == null);
				await db.SaveChangesAsync();
				summary = await _feeCollections.GetSummaryAsync(input);
				summary.TotalCount.ShouldBe(accountCount == 1 ? 1 : 3);
				summary.Items.Sum(x => x.TotalFees).ShouldBe((accountCount + 1) * 5000d);
				summary.Items.Sum(x => x.PendingFees).ShouldBe((accountCount + 1) * 5000d);
				summary.Items.Sum(x => x.TotalCharges).ShouldBe((accountCount + 1) * 200d);
				summary.Items.Count(x => x.FinanceFeeAccountId.HasValue).ShouldBe(accountCount);
				if (accountCount == 1) summary.Items.Single().FinanceFeeAccountId.ShouldBe(accountIds.Single());
				else summary.Items.Count(x => !x.FinanceFeeAccountId.HasValue).ShouldBe(1);
				summary.Items.ShouldAllBe(x => x.StudentId == student.Id);
			}
		});
	}
}
