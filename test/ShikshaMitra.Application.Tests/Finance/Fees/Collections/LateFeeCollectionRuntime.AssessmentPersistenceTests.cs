using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
	[Fact]
	public async Task Summary_Reads_Persisted_Late_Fee_Assessment_Without_Changing_Balances()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using var tenant = _currentTenant.Change(TestTenantId);
			var seeded = await SeedStudentWithOverdueDueFeeAsync(10, true);
			var due = await _dueFeeRepo.GetAsync(seeded.DueFeeId);
			var assignment = (await _studentFeeStructureRepo.GetListAsync(x => x.StudentId == seeded.StudentId)).Single();
			var versionId = assignment.CollectionPolicyVersionId!.Value;
			var assessedAt = new DateTimeOffset(DateTime.UtcNow.Date);
			due.PreserveLateFeeAssessment(200m, assessedAt, versionId, "Persisted assessment");
			await _dueFeeRepo.UpdateAsync(due, autoSave: true);
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			db.Entry(due).State = EntityState.Detached;
			var reloaded = await _dueFeeRepo.GetAsync(seeded.DueFeeId);
			reloaded.GetLateFeeAssessedAt().ShouldBe(assessedAt);
			reloaded.GetLateFeeAssessmentPolicyVersionId().ShouldBe(versionId);

			var row = await GetSummaryRowAsync(seeded.AcademicYearId, seeded.StudentId);
			row.TotalFees.ShouldBe(5000d);
			row.PaidFees.ShouldBe(0d);
			row.PendingFees.ShouldBe(5000d);
			row.TotalCharges.ShouldBe(200d);
			row.PendingCharges.ShouldBe(200d);
		});
	}
}
