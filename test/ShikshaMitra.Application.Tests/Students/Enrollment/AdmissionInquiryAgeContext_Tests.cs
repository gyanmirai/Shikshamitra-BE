using System;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(5)]
	[InlineData(0)]
	public Task Inquiry_age_context_should_use_existing_class_minimum_and_academic_year_start(int minimumAge) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var start = new DateTime(2028, 4, 1);
			await WithUnitOfWorkAsync(async () =>
			{
				var standard = await _standardRepository.GetAsync(offering.StandardId);
				standard.AgeLowerLimit = minimumAge;
				await _standardRepository.UpdateAsync(standard, true);
				var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
				var years = GetRequiredService<IRepository<AcademicYear, Guid>>();
				var year = await years.GetAsync(cycle.AcademicYearId);
				year.SetDates(start, start.AddYears(1).AddDays(-1));
				await years.UpdateAsync(year, true);
			});
			var context = await WithUnitOfWorkAsync(() =>
				GetRequiredService<IAdmissionInquiryCaptureAppService>().GetContextAsync(offering.Id));
			context.AcademicYearStartDate.ShouldBe(DateOnly.FromDateTime(start));
			context.MinimumAgeYears.ShouldBe(minimumAge > 0 ? minimumAge : null);
		});
}
