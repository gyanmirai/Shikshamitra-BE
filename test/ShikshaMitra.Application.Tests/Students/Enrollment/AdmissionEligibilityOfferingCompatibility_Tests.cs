using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{

	private async Task<EligibilityFixture> CreateEligibilityOfferingFixtureAsync(string source)
	{
		var formId = await CreateEligibilityAnswerFormAsync(AdmissionFormFieldType.Number,
			source == "inquiry" ? AdmissionFormPurpose.Inquiry : AdmissionFormPurpose.Registration);
		return await CreateEligibilityFixtureAsync(EligibilityPolicyJson([EligibilityOfferingCriterion(source, formId)]),
			new DateTime(2021, 4, 1), applicationFormDefinitionId: source == "application" ? formId : null,
			inquiryFormDefinitionId: source == "inquiry" ? formId : null);
	}

	private static object EligibilityOfferingCriterion(string source, Guid formId) => new
	{
		code = "SOURCE_MARKS", name = "Previous school marks", kind = source, formDefinitionId = formId,
		conditionJson = "{\"field\":\"" + source + ".ANSWER\",\"operator\":\"GreaterThanOrEqual\",\"value\":50}"
	};

	private Task<Guid> CreateEligibilityOfferingCycleAsync(Guid sourceCycleId) => WithUnitOfWorkAsync(async () =>
	{
		var source = await _cycleRepository.GetAsync(sourceCycleId);
		var cycle = new AdmissionCycle(Guid.NewGuid(), TestTenantId, "Eligibility compatibility cycle",
			$"ELIG_{Guid.NewGuid():N}"[..24], source.AcademicYearId, source.StartDate, source.EndDate);
		cycle.Publish(TestUserId, DateTime.UtcNow);
		await _cycleRepository.InsertAsync(cycle, autoSave: true);
		return cycle.Id;
	});

	private Task SetStoredEligibilityOfferingFormAsync(Guid offeringId, string source, Guid formId) => WithUnitOfWorkAsync(async () =>
	{
		var offering = await _offeringRepository.GetAsync(offeringId);
		offering.ConfigureReferences(offering.WorkflowDefinitionId,
			source == "inquiry" ? formId : offering.InquiryFormDefinitionId,
			source == "application" ? formId : offering.ApplicationFormDefinitionId);
		await _offeringRepository.UpdateAsync(offering, autoSave: true);
	});

	private async Task<T> EligibilityOfferingInputAsync<T>(AdmissionOfferingDto source) where T : AdmissionOfferingWriteDto, new()
	{
		var inquiryProfileId = source.InquiryIntakeProfileId;
		var assignmentProfileId = source.CounsellorAssignmentProfileId;
		if (!inquiryProfileId.HasValue || !assignmentProfileId.HasValue)
		{
			var ids = await WithUnitOfWorkAsync(async () =>
			{
				var repository = GetRequiredService<IRepository<AdmissionIntakeProfile, Guid>>();
				var inquiry = new AdmissionIntakeProfile(Guid.NewGuid(), TestTenantId,
					AdmissionIntakeProfileKind.Inquiry, $"TEST_INQUIRY_{Guid.NewGuid():N}"[..30],
					"Inquiry compatibility", JsonSerializer.Serialize(new AdmissionInquiryIntakeRules()));
				var assignment = new AdmissionIntakeProfile(Guid.NewGuid(), TestTenantId,
					AdmissionIntakeProfileKind.CounsellorAssignment, $"TEST_ASSIGN_{Guid.NewGuid():N}"[..30],
					"Assignment compatibility", JsonSerializer.Serialize(new AdmissionCounsellorAssignmentRules
					{ CounsellorUserIds = [TestUserId] }));
				await repository.InsertAsync(inquiry, autoSave: false);
				await repository.InsertAsync(assignment, autoSave: true);
				return (inquiry.Id, assignment.Id);
			});
			inquiryProfileId = ids.Item1;
			assignmentProfileId = ids.Item2;
		}
		return new T
		{
		CampusId = source.CampusId, BoardId = source.BoardId, MediumId = source.MediumId,
		StageId = source.StageId, StandardId = source.StandardId, Code = source.Code,
		UseCycleDates = source.UseCycleDates, OpenDate = source.OpenDate, CloseDate = source.CloseDate,
		WorkflowDefinitionId = source.WorkflowDefinitionId, InquiryFormDefinitionId = source.InquiryFormDefinitionId,
		ApplicationFormDefinitionId = source.ApplicationFormDefinitionId, IsActive = source.IsActive,
		InquiryIntakeProfileId = inquiryProfileId,
		CounsellorAssignmentProfileId = assignmentProfileId
		};
	}

	private static Guid? SelectedEligibilityOfferingForm(AdmissionOfferingDto offering, string source) =>
		source == "application" ? offering.ApplicationFormDefinitionId : offering.InquiryFormDefinitionId;

	private static void SelectEligibilityOfferingForm(AdmissionOfferingWriteDto offering, string source, Guid? formId)
	{
		if (source == "application") offering.ApplicationFormDefinitionId = formId;
		else offering.InquiryFormDefinitionId = formId;
	}
}
