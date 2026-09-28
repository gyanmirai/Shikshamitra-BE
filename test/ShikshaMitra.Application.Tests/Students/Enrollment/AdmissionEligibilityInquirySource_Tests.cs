using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Eligibility_inquiry_answers_require_persisted_source_provenance_and_use_binding_keys()
		=> RunCertificationScenarioAsync(async () =>
		{
			var formId = await CreateEligibilityAnswerFormAsync(AdmissionFormFieldType.Number,
				AdmissionFormPurpose.Inquiry);
			var criterion = new { code = "INQUIRY", name = "Original inquiry answers", kind = "inquiry", formDefinitionId = formId,
				conditionJson = "{\"operator\":\"ALL\",\"conditions\":[{\"field\":\"inquiry.Applicant.FirstName\",\"operator\":\"Equals\",\"value\":\"Aanya\"},{\"field\":\"inquiry.ANSWER\",\"operator\":\"GreaterThanOrEqual\",\"value\":80}]}" };
			var fixture = await CreateEligibilityFixtureAsync(EligibilityPolicyJson([criterion]), new DateTime(2021, 4, 1), inquiryFormDefinitionId: formId);
			var inquiry = await GetRequiredService<IAdmissionInquiryAppService>().CreateAsync(new()
			{
				AdmissionCycleId = fixture.Offering.AdmissionCycleId, AdmissionOfferingId = fixture.Offering.Id,
				BoardId = fixture.Offering.BoardId, MediumId = fixture.Offering.MediumId,
				StandardId = fixture.Offering.StandardId,
				ApplicantName = "Fallback Patel", ApplicantFirstName = "Fallback", ApplicantLastName = "Patel",
				GuardianFirstName = "Meera", GuardianPhone = "9876512345", Phone = "9876512345", Source = AdmissionInquirySource.Website,
				FormValues =
				[
					new() { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"Aanya\"" },
					new() { FieldCode = "APPLICANT_DOB", ValueJson = "\"2021-04-01\"" },
					new() { FieldCode = "APPLICANT_GENDER", ValueJson = "\"Female\"" },
					new() { FieldCode = "ANSWER", ValueJson = "90" }
				]
			});
			var inquiries = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
			var snapshot = await WithUnitOfWorkAsync(async () =>
			{
				var persisted = await inquiries.GetAsync(inquiry.Id);
				persisted.FormResponseDefinitionId.ShouldBe(formId);
				using var json = JsonDocument.Parse(persisted.FormResponseSnapshotJson!);
				json.RootElement.TryGetProperty("Applicant.FirstName", out _).ShouldBeTrue();
				json.RootElement.TryGetProperty("APPLICANT_FIRST_NAME", out _).ShouldBeFalse();
				return persisted.FormResponseSnapshotJson!;
			});
			var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, fixture.Offering.Id, inquiry.Id);
			(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
			var workflow = await _applicationAppService.GetWorkflowAsync(application.Id);
			var stageId = workflow.Stages.Single(item => item.Code == "ELIGIBILITY").Id;
			var service = GetRequiredService<IAdmissionEligibilityAppService>();
			async Task<AdmissionEligibilityDto> CheckAsync() => await service.EvaluateAsync(application.Id, stageId,
				new() { ConcurrencyStamp = (await _applicationAppService.GetAsync(application.Id)).ConcurrencyStamp! });
			(await CheckAsync()).Status.ShouldBe("eligible");
			// Legacy snapshots do not inherit provenance from today's offering configuration.
			await WithUnitOfWorkAsync(async () =>
			{
				var legacy = await inquiries.GetAsync(inquiry.Id);
				legacy.ExtraProperties.Remove("AdmissionFormResponseDefinitionId");
				legacy.RecordFormResponseSnapshot(snapshot);
				await inquiries.UpdateAsync(legacy, autoSave: true);
			});
			(await service.GetAsync(application.Id, stageId)).IsStale.ShouldBeTrue();
			(await CheckAsync()).Status.ShouldBe("needsInformation");
			await WithUnitOfWorkAsync(async () =>
			{
				var mismatched = await inquiries.GetAsync(inquiry.Id);
				mismatched.RecordFormResponseSnapshot(snapshot, Guid.NewGuid());
				await inquiries.UpdateAsync(mismatched, autoSave: true);
			});
			var mismatch = await CheckAsync();
			mismatch.Status.ShouldBe("needsInformation");
			mismatch.CanComplete.ShouldBeFalse();
			(await service.GetHistoryAsync(application.Id, stageId)).Count.ShouldBe(3);
		});
}
