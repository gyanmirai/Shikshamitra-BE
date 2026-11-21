using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Inquiry_date_range_filters_the_same_local_day_before_count_paging_and_export() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateInquiryDateRangeFixtureAsync();
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			// 17 November in Asia/Kolkata is [16 Nov 18:30Z, 17 Nov 18:30Z).
			var input = new AdmissionInquiryListInput
			{
				AdmissionOfferingId = fixture.OfferingId,
				CreationTimeFrom = fixture.From,
				CreationTimeBefore = fixture.Before,
				Sorting = "creationTime asc",
				SkipCount = 1,
				MaxResultCount = 1
			};
			var page = await inquiries.GetListAsync(input);
			page.TotalCount.ShouldBe(2);
			page.Items.Single().InquiryNumber.ShouldBe("DATE-RANGE-2");
			var csv = Encoding.UTF8.GetString(await inquiries.ExportCsvAsync(input));
			csv.ShouldContain("DATE-RANGE-1");
			csv.ShouldContain("DATE-RANGE-2");
			csv.ShouldNotContain("DATE-RANGE-0");
			csv.ShouldNotContain("DATE-RANGE-3");
			csv.ShouldNotContain("DATE-RANGE-4");
		});

	[Fact]
	public Task Inquiry_date_range_supports_open_bounds_and_omitted_dates() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateInquiryDateRangeFixtureAsync();
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var fromOnly = await inquiries.GetListAsync(new()
			{
				AdmissionOfferingId = fixture.OfferingId,
				CreationTimeFrom = DateTime.SpecifyKind(fixture.From, DateTimeKind.Unspecified),
				Sorting = "creationTime asc"
			});
			fromOnly.TotalCount.ShouldBe(4);
			fromOnly.Items.Select(row => row.InquiryNumber).ShouldBe(new[] { "DATE-RANGE-1", "DATE-RANGE-2", "DATE-RANGE-3", "DATE-RANGE-4" });
			var beforeOnly = await inquiries.GetListAsync(new()
			{
				AdmissionOfferingId = fixture.OfferingId,
				CreationTimeBefore = fixture.Before,
				Sorting = "creationTime asc"
			});
			beforeOnly.TotalCount.ShouldBe(3);
			beforeOnly.Items.Select(row => row.InquiryNumber).ShouldBe(new[] { "DATE-RANGE-0", "DATE-RANGE-1", "DATE-RANGE-2" });
			(await inquiries.GetListAsync(new() { AdmissionOfferingId = fixture.OfferingId })).TotalCount.ShouldBe(5);
		});

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public Task Inquiry_date_range_rejects_empty_or_inverted_intervals_for_list_and_export(int daysAfterFrom) =>
		RunCertificationScenarioAsync(async () =>
		{
			var from = new DateTime(2026, 11, 16, 18, 30, 0, DateTimeKind.Utc);
			var input = new AdmissionInquiryListInput { CreationTimeFrom = from, CreationTimeBefore = from.AddDays(daysAfterFrom) };
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			(await Should.ThrowAsync<BusinessException>(() => inquiries.GetListAsync(input))).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
			(await Should.ThrowAsync<BusinessException>(() => inquiries.ExportCsvAsync(input))).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
		});

	private async Task<(Guid OfferingId, DateTime From, DateTime Before)> CreateInquiryDateRangeFixtureAsync()
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var from = new DateTime(2026, 11, 16, 18, 30, 0, DateTimeKind.Utc);
		var before = new DateTime(2026, 11, 17, 18, 30, 0, DateTimeKind.Utc);
		DateTime[] creationTimes = [from.AddSeconds(-1), from, before.AddSeconds(-1), before, before.AddSeconds(1)];
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			for (var index = 0; index < creationTimes.Length; index++)
			{
				var inquiry = new AdmissionInquiry(Guid.NewGuid(), TestTenantId, $"DATE-RANGE-{index}", offering.AdmissionCycleId,
					offering.Id, $"Date range applicant {index}", "9876543210", null, AdmissionInquirySource.WalkIn, null);
				await repository.InsertAsync(inquiry, true);
				db.Entry(inquiry).Property(row => row.CreationTime).CurrentValue = creationTimes[index];
			}
			await db.SaveChangesAsync();
		});
		return (offering.Id, from, before);
	}
}
