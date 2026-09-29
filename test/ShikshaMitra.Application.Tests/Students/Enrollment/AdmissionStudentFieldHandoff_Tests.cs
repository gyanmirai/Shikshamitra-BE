using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Additional_student_field_saves_and_clears_through_application_form()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
				userId: TestUserId, description: "Additional student field form test"), autoSave: true);
			var paletteFields = await _formAppService.GetSystemFieldsAsync();
			paletteFields.Count.ShouldBe(100);
			paletteFields.ShouldContain(field => field.Key == "Medical.BloodGroup" &&
				field.FieldType == AdmissionFormFieldType.Select);
			paletteFields.ShouldAllBe(field => !field.Key.StartsWith("FinancialAssistance.",
				StringComparison.OrdinalIgnoreCase));
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var section = (await _formSectionRepository.GetListAsync(item =>
					item.FormDefinitionId == offering.ApplicationFormDefinitionId)).Single();
				await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(
					Guid.NewGuid(), offering.ApplicationFormDefinitionId!.Value, section.Id,
					"BLOOD_GROUP", "Blood group", AdmissionFormFieldType.Select,
					AdmissionFormBindingType.System, "Medical.BloodGroup", false, false, 6,
					null, null, null, null, null,
					AdmissionAdditionalStudentFieldCatalog.Metadata["Medical.BloodGroup"].OptionsJson),
					autoSave: true);
			});
			var applicant = await CreateApplicantAsync("Asha");
			var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);

			await _applicationAppService.UpdateFormValuesAsync(application.Id, new()
			{
				ConcurrencyStamp = application.ConcurrencyStamp,
				Values = [new() { FieldCode = "BLOOD_GROUP", ValueJson = "\"O+\"" }]
			});
			(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(applicant.Id)))
				.ExtraProperties["AdmissionStudentField.Medical.BloodGroup"].ToString().ShouldBe("O+");

			application = await _applicationAppService.GetAsync(application.Id);
			await _applicationAppService.UpdateFormValuesAsync(application.Id, new()
			{
				ConcurrencyStamp = application.ConcurrencyStamp,
				Values = [new() { FieldCode = "BLOOD_GROUP", ValueJson = "\"\"" }]
			});
			(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(applicant.Id)))
				.ExtraProperties.ContainsKey("AdmissionStudentField.Medical.BloodGroup").ShouldBeFalse();
		}
	}

	[Fact]
	public Task Captured_student_fields_survive_finalization_and_student_reload() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("Asha");
			await WithUnitOfWorkAsync(async () =>
			{
				var applicant = await _applicantRepository.GetAsync(fixture.Application.ApplicantId);
				AdmissionFormBindingRegistry.ApplyValue("Medical.BloodGroup", "O+", applicant, null).ShouldBeTrue();
				AdmissionFormBindingRegistry.ApplyValue("PreviousSchool.Name", "Sample School", applicant, null).ShouldBeTrue();
				await _applicantRepository.UpdateAsync(applicant, true);
			});

			var result = await FinalizeAsync(fixture.Application, true, "student-fields-handoff");
			var student = await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(result.StudentId));
			student.ExtraProperties["AdmissionStudentField.Medical.BloodGroup"].ToString().ShouldBe("O+");
			student.ExtraProperties["AdmissionStudentField.PreviousSchool.Name"].ToString().ShouldBe("Sample School");
			var onboarding = await GetRequiredService<IStudentOnboardingAppService>().GetAsync(result.OnboardingCaseId);
			onboarding.CapturedStudentFields.ShouldContain(field =>
				field.Key == "Medical.BloodGroup" && field.Value == "O+");
			onboarding.CapturedStudentFields.ShouldContain(field =>
				field.Key == "PreviousSchool.Name" && field.Value == "Sample School");
		});

	[Fact]
	public Task Captured_student_fields_update_a_reused_student_without_changing_identity() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("Diya", linkExistingStudent: true);
			await WithUnitOfWorkAsync(async () =>
			{
				var applicant = await _applicantRepository.GetAsync(fixture.Application.ApplicantId);
				AdmissionFormBindingRegistry.ApplyValue("Medical.BloodGroup", "B+", applicant, null).ShouldBeTrue();
				await _applicantRepository.UpdateAsync(applicant, true);
			});

			var result = await FinalizeAsync(fixture.Application, false, "student-fields-existing-handoff");
			result.ReusedExistingStudent.ShouldBeTrue();
			result.StudentId.ShouldBe(fixture.ExistingStudentId!.Value);
			var student = await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(result.StudentId));
			student.ExtraProperties["AdmissionStudentField.Medical.BloodGroup"].ToString().ShouldBe("B+");
		});
}
