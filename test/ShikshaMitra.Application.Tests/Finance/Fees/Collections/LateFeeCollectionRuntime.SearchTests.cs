using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Students;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
	[Theory]
	[InlineData("10223", true)]
	[InlineData("223", true)]
	[InlineData("Trace 10223", true)]
	[InlineData("Trace", true)]
	[InlineData("87", true)]
	[InlineData("NoMatchingPayer", false)]
	public async Task Summary_Search_Matches_Name_Gr_And_Roll_Before_Paging(string search, bool matches)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (yearId, seededStudentId, _, _, _) = await SeedStudentWithOverdueDueFeeAsync(0, false);
			var seeded = await _studentRepo.GetAsync(seededStudentId);
			var target = new Student(Guid.NewGuid(), TestTenantId, "Trace", null, "Payer",
				yearId, seeded.AcademicBoardId!.Value, seeded.AcademicMediumId, seeded.AcademicMediumStageId!.Value,
				standardId: null, divisionId: null, grNo: 10223, rollNo: 87,
				status: StudentLifeCycleStatusEnum.Enrolled);
			await _studentRepo.InsertAsync(target, autoSave: true);

			var input = new GetFeeCollectionSummaryInput
			{
				AcademicYearId = yearId, BoardId = seeded.AcademicBoardId.Value,
				EducationMediumId = seeded.AcademicMediumId, Search = search,
				SkipCount = 0, MaxResultCount = 1,
			};
			var result = await _feeCollections.GetSummaryAsync(input);
			result.TotalCount.ShouldBe(matches ? 1 : 0);
			if (matches) result.Items.Single().StudentId.ShouldBe(target.Id);
			else result.Items.ShouldBeEmpty();

			input.SkipCount = 1;
			var next = await _feeCollections.GetSummaryAsync(input);
			next.TotalCount.ShouldBe(result.TotalCount);
			next.Items.ShouldBeEmpty();
		}
	}
}
