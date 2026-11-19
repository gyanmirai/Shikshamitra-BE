using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Onboarding_offering_choices_batch_cycles_and_keep_all_canonical_fields_and_order()
		=> RunCertificationScenarioAsync(async () =>
		{
			var workflow = await CreateAndPublishCurrentWorkflowAsync();
			var first = await CreateAvailableOfferingAsync(workflow.Id, registrationFormId:
				AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson));
			await WithUnitOfWorkAsync(async () =>
			{
				var form = await _formRepository.GetAsync(first.ApplicationFormDefinitionId!.Value);
				var metadata = JsonNode.Parse(form.GetProperty<string>("AdmissionFormExperience") ?? "{}")!.AsObject();
				metadata["coreFieldPolicyVersion"] = 1;
				form.SetProperty("AdmissionFormExperience", metadata.ToJsonString());
				await _formRepository.UpdateAsync(form, true);
			});
			var yearId = await WithUnitOfWorkAsync(async () => (await _cycleRepository.GetAsync(first.AdmissionCycleId)).AcademicYearId);
			var active = await MeasureOnboardingOfferingChoicesAsync(yearId, 1);
			active.FormSectionReads.ShouldBe(0);
			active.FormFieldReads.ShouldBe(0);
			await WithUnitOfWorkAsync(async () =>
			{
				var offering = await _offeringRepository.GetAsync(first.Id);
				offering.Deactivate();
				await _offeringRepository.UpdateAsync(offering, true);
			});
			var singleInactive = await MeasureOnboardingOfferingChoicesAsync(yearId, 1);
			singleInactive.FormSectionReads.ShouldBe(1);
			singleInactive.FormFieldReads.ShouldBe(1);
			// Activation readiness needs both section/step rules and question definitions.
			// These two batched reads are skipped for active legacy routes; their cost is fixed.
			singleInactive.ReadCount.ShouldBeLessThanOrEqualTo(active.ReadCount + 2,
				"Inactive readiness adds only one section batch and one field batch to the active catalogue.");
			await WithUnitOfWorkAsync(async () =>
			{
				for (var index = 0; index < 5; index++)
				{
					var cycle = new AdmissionCycle(Guid.NewGuid(), TestTenantId, $"Onboarding catalogue {index}", $"OB_CAT_{index}",
						yearId, new DateTime(2026, 1, 1), new DateTime(2027, 12, 31));
					if (index != 4) cycle.Publish(TestUserId, DateTime.UtcNow);
					await _cycleRepository.InsertAsync(cycle, true);
					var offering = new AdmissionOffering(Guid.NewGuid(), TestTenantId, cycle.Id, null, first.BoardId,
						first.MediumId, first.StageId, first.StandardId, $"OB_OFFERING_{index}", true, null, null,
						name: index < 2 ? "Shared choice name" : $"Choice {index}",
						entryMode: index == 2 ? AdmissionEntryMode.DirectRegistration : AdmissionEntryMode.Inquiry);
					offering.ConfigureReferences(workflow.Id, first.InquiryFormDefinitionId, first.ApplicationFormDefinitionId);
					if (index == 3) offering.Deactivate();
					await _offeringRepository.InsertAsync(offering, true);
				}
			});
			var many = await MeasureOnboardingOfferingChoicesAsync(yearId, 6);
			many.FormSectionReads.ShouldBe(1);
			many.FormFieldReads.ShouldBe(1);
			Console.WriteLine($"SQLite onboarding offering choices: active 1 cycle={active.ReadCount} reads; inactive 1 cycle={singleInactive.ReadCount} reads; mixed 6 cycles={many.ReadCount} reads. " +
				$"Section/field batches: active={active.FormSectionReads}/{active.FormFieldReads}, inactive={singleInactive.FormSectionReads}/{singleInactive.FormFieldReads}, mixed={many.FormSectionReads}/{many.FormFieldReads}.");
			many.ReadCount.ShouldBeLessThanOrEqualTo(singleInactive.ReadCount,
				"Onboarding offering choices must batch the same scoped reader across cycles, including draft, inactive and direct-entry choices.");
		});

	private sealed record OnboardingOfferingReadMeasurement(int ReadCount, int FormSectionReads, int FormFieldReads);

	private async Task<OnboardingOfferingReadMeasurement> MeasureOnboardingOfferingChoicesAsync(Guid yearId, int expectedCount)
	{
		var expected = await WithUnitOfWorkAsync(async () =>
		{
			var result = new List<(AdmissionOfferingListDto Row, Guid? WorkflowId)>();
			foreach (var cycle in await _cycleRepository.GetListAsync(item => item.AcademicYearId == yearId))
			{
				var rows = await GetRequiredService<AdmissionOfferingReadService>().GetListAsync(new() { AdmissionCycleId = cycle.Id, MaxResultCount = 1000 });
				foreach (var row in rows.Items)
					result.Add((row, (await _offeringRepository.GetAsync(row.Id)).WorkflowDefinitionId));
			}
			return result.OrderBy(item => item.Row.Name).ToList();
		});
		return await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			db.ChangeTracker.Clear();
			using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
			var result = await GetRequiredService<IStudentOnboardingAppService>().GetAvailableOfferingsAsync(yearId);
			result.Count.ShouldBe(expectedCount);
			result.Select(item => item.Id).ShouldBe(expected.Select(item => item.Row.Id));
			for (var index = 0; index < expected.Count; index++)
			{
				foreach (var property in typeof(AdmissionOfferingListDto).GetProperties())
					property.GetValue(result[index]).ShouldBe(property.GetValue(expected[index].Row),
						$"The canonical offering {property.Name} field must survive the Onboarding projection.");
				result[index].WorkflowDefinitionId.ShouldBe(expected[index].WorkflowId);
				result[index].AcademicYearId.ShouldBe(yearId);
			}
			return new OnboardingOfferingReadMeasurement(capture.ReadCount,
				capture.ReadCommands.Count(command => command.Contains("FROM \"AppAdmissionFormSections\"", StringComparison.Ordinal)),
				capture.ReadCommands.Count(command => command.Contains("FROM \"AppAdmissionFormFields\"", StringComparison.Ordinal)));
		});
	}
}
