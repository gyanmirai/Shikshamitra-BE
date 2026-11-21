using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(1001, false)]
	[InlineData(1002, false)]
	[InlineData(1003, true)]
	public Task Gr_preview_uses_the_shared_saved_counter_and_reports_the_limit_without_allocating(long next, bool exhausted) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateAutomaticRegressionFixtureAsync("GrLivePreview");
			var year = (await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId))).AcademicYearId;
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var caseId = (await FinalizeAsync(fixture.Application, true, "gr-preview-case")).OnboardingCaseId;
			var initialSeries = (await service.GetAvailableIdentifierSeriesAsync(caseId)).Single();
			var settings = (await service.GetSettingsAsync(year)).Single(s => s.AcademicYearId == year);
			var config = JsonNode.Parse(settings.ConfigurationJson)!;
			var rules = config["grNumber"]!["configurations"]!.AsArray();
			var original = rules[0]!;
			var revision = original.DeepClone();
			revision["id"] = "gr-preview-v2";
			revision["previousVersionId"] = original["id"]!.GetValue<string>();
			revision["version"] = 2;
			revision["maximumNumber"] = 1002;
			revision["prefix"] = " GR-";
			revision["suffix"] = "AD ";
			revision["status"] = "draft";
			revision["isActive"] = false;
			rules.Add(revision);
			settings = await service.SaveSettingsAsync(new() { AcademicYearId = year, ConcurrencyStamp = settings.ConcurrencyStamp, ConfigurationJson = config.ToJsonString() });
			original["status"] = "superseded";
			original["isActive"] = false;
			revision["status"] = "published";
			revision["isActive"] = true;
			await service.SaveSettingsAsync(new() { AcademicYearId = year, ConcurrencyStamp = settings.ConcurrencyStamp, ConfigurationJson = config.ToJsonString() });
			await WithUnitOfWorkAsync(async () =>
			{
				var oldYear = await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(
					new AcademicYear(Guid.NewGuid(), TestTenantId, "Previous GR year", new DateTime(2020, 4, 1), new DateTime(2021, 3, 31)), true);
				await GetRequiredService<IRepository<StudentIdentifierSeries, Guid>>().InsertAsync(
					new StudentIdentifierSeries(initialSeries.Id, TestTenantId, oldYear.Id, null, StudentIdentifierType.GrNumber,
						initialSeries.Code, initialSeries.Name, initialSeries.Prefix, initialSeries.Suffix, initialSeries.Padding,
						next, null, DateTime.UnixEpoch, null), true);
			});
			var preview = (await service.GetGrNumberPreviewsAsync(year)).Single();
			preview.RuleId.ShouldBe("gr-preview-v2");
			preview.NextSequence.ShouldBe(next);
			preview.MaximumNumber.ShouldBe(1002);
			preview.LimitReached.ShouldBe(exhausted);
			preview.IsEffective.ShouldBeTrue();
			if (exhausted)
			{
				preview.NextPreview.ShouldBeNull();
				var error = await Should.ThrowAsync<BusinessException>(() => service.GetAvailableIdentifierSeriesAsync(caseId));
				error.Code.ShouldBe(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);
				error.Data["ReasonCode"].ShouldBe("SeriesLimitReached");
				error.Data["MaximumNumber"].ShouldBe(1002L);
				error.Data["NextSequence"].ShouldBe(next);
			}
			else
			{
				var available = (await service.GetAvailableIdentifierSeriesAsync(caseId)).Single();
				available.Id.ShouldBe(initialSeries.Id);
				preview.NextPreview.ShouldBe(available.NextPreview);
			}
			(await service.GetGrNumberPreviewsAsync(year)).Single().NextSequence.ShouldBe(next);
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentIdentifierSeries, Guid>>().GetAsync(initialSeries.Id)))
				.NextSequence.ShouldBe(next);
		});
}
