using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
	[Fact]
	public async Task Summary_default_page_only_reads_dues_for_visible_payers()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, originalStudentId, _, originalDueId, _) = await SeedStudentWithOverdueDueFeeAsync(10, true);
			var originalStudent = await _studentRepo.GetAsync(originalStudentId);
			var assignment = (await _studentFeeStructureRepo.GetListAsync(x => x.StudentId == originalStudentId)).Single();
			var due = await _dueFeeRepo.GetAsync(originalDueId);
			var stage = (await _stageRepo.GetListAsync(x => x.EducationMediumId == originalStudent.AcademicMediumId)).Single();
			for (var index = 1; index < 3; index++)
				await AddPolicyReadStudentAsync(originalStudent, assignment, due, stage.Id, index);
			var input = new GetFeeCollectionSummaryInput
			{
				AcademicYearId = year, BoardId = originalStudent.AcademicBoardId!.Value,
				EducationMediumId = originalStudent.AcademicMediumId, MaxResultCount = 1
			};
			var complete = await _feeCollections.GetSummaryAsync(new GetFeeCollectionSummaryInput
			{
				AcademicYearId = year, BoardId = originalStudent.AcademicBoardId.Value,
				EducationMediumId = originalStudent.AcademicMediumId, MaxResultCount = 100
			});
			complete.TotalCount.ShouldBe(3);
			var offPageStudentId = complete.Items[1].StudentId!.Value;
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
				var page = await _feeCollections.GetSummaryAsync(input);
				page.TotalCount.ShouldBe(complete.TotalCount);
				page.Items.Single().ShouldBeEquivalentTo(complete.Items[0]);
				var dueTable = db.Model.FindEntityType(typeof(DueFee))!.GetTableName()!;
				var dueReads = reads.Details.Where(read => read.Sql.Contains($"\"{dueTable}\"", StringComparison.Ordinal)).ToArray();
				dueReads.ShouldNotBeEmpty();
				dueReads.ShouldAllBe(read => !read.Parameters.Contains(offPageStudentId.ToString(), StringComparison.OrdinalIgnoreCase),
					"The first page must not hydrate dues belonging only to another page.");
			});
		}
	}
}
