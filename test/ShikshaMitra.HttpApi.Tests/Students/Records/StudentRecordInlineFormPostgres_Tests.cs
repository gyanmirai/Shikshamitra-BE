using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordsApplicationPostgres_Tests
{
	[StudentRecordsApplicationPostgresFact]
	public async Task Inline_form_postgres_http_creates_contact_only_mother_preserves_father_submission_and_audit()
	{
		Client.Timeout = TimeSpan.FromMinutes(3);
		Client.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var fixture = await SeedInlineFormPostgresAsync();
		var root = $"/api/students/records/{fixture.StudentId}/details";
		using var read = await Client.GetAsync(root + "/form-snapshot");
		read.StatusCode.ShouldBe(HttpStatusCode.OK, await read.Content.ReadAsStringAsync());
		var snapshot = (await read.Content.ReadFromJsonAsync<StudentRecordSectionSnapshotDto>(JsonOptions))!;
		var payload = JsonNode.Parse(snapshot.Data.GetRawText())!;
		var source = payload["sources"]!.AsArray().Single()!;
		source["submissionId"]!.GetValue<Guid>().ShouldBe(fixture.SubmissionId);
		source["values"]!["MOTHER_MOBILE"].ShouldBeNull();
		source["values"]!["MOTHER_MOBILE"] = "9000000001";
		var input = new SaveStudentRecordFormDto
		{
			ClientKey = "inline-form-pg-" + Guid.NewGuid().ToString("N"), Reason = "Correct synthetic mother phone",
			ExpectedSnapshot = snapshot.Snapshot, Payload = JsonSerializer.SerializeToElement(payload)
		};
		using var response = await Client.PutAsJsonAsync(root + "/form", input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var saved = (await response.Content.ReadFromJsonAsync<StudentRecordChangeDto>(JsonOptions))!;
		saved.Status.ShouldBe("Applied"); saved.Section.ShouldBe("registration-form");
		saved.RequiredApprovals.ShouldBe(0);
		using var replay = await Client.PutAsJsonAsync(root + "/form", input, JsonOptions);
		replay.StatusCode.ShouldBe(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
		(await replay.Content.ReadFromJsonAsync<StudentRecordChangeDto>(JsonOptions))!.Id.ShouldBe(saved.Id);
		await InTenantAsync(async services =>
		{
			var family = await services.GetRequiredService<IRepository<StudentFamilyDetail, Guid>>().GetAsync(fixture.StudentId);
			family.MotherDetails!.FullName.ShouldBe(string.Empty); family.MotherDetails.ContactNo.ShouldBe("9000000001");
			family.FatherDetails!.FullName.ShouldBe("Saved father"); family.FatherDetails.ContactNo.ShouldBe("9000000002");
			family.FatherDetails.Email.ShouldBeNull();
			family.GuardianDetails!.FullName.ShouldBe(string.Empty);
			family.GuardianDetails.ContactNo.ShouldBeNull();
			family.GuardianDetails.Email.ShouldBeNull();
			var student = await services.GetRequiredService<IStudentRepository>().GetAsync(fixture.StudentId);
			StudentFamilyContactSelection.Resolve(student, family).ShouldBe("father");
			(await services.GetRequiredService<IRepository<AdmissionFormSubmission, Guid>>().GetAsync(fixture.SubmissionId)).SnapshotJson.ShouldBe(fixture.OriginalJson);
			(await services.GetRequiredService<IRepository<StudentRecordChange, Guid>>().CountAsync(x => x.StudentId == fixture.StudentId)).ShouldBe(1);
			(await services.GetRequiredService<IRepository<StudentGuardianAccessReview, Guid>>().CountAsync(x => x.StudentId == fixture.StudentId && x.ChangeRequestId == saved.Id)).ShouldBe(1);
			return true;
		});
		using var after = await Client.GetAsync(root + "/form-snapshot");
		after.StatusCode.ShouldBe(HttpStatusCode.OK, await after.Content.ReadAsStringAsync());
		var current = (await after.Content.ReadFromJsonAsync<StudentRecordSectionSnapshotDto>(JsonOptions))!;
		current.Data.GetProperty("sources")[0].GetProperty("values").GetProperty("MOTHER_MOBILE").GetString().ShouldBe("9000000001");
		output.WriteLine("Real PostgreSQL HTTP snapshot -> single Mother.Mobile edit -> Applied/replay/readback; existing father preserved, immutable source unchanged, one Correction and guardian review persisted.");
	}

	private async Task<InlineFormPostgresFixture> SeedInlineFormPostgresAsync()
	{
		var offering = await SeedAdmissionReturnConcurrencyOfferingAsync(
			new Fixture(factory.SeededAcademicYearId, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, []), AdmissionEntryMode.DirectRegistration);
		return await InTenantAsync(async services =>
		{
			var student = new Student(Guid.NewGuid(), TenantId, "Inline", null, "Provider", factory.SeededAcademicYearId,
				factory.SeededBoardId, factory.SeededMediumId, factory.SeededMediumStageId, factory.SeededStandardId,
				factory.SeededDivisionId, status: StudentLifeCycleStatusEnum.Enrolled);
			await services.GetRequiredService<IStudentRepository>().InsertAsync(student, true);
			var family = new StudentFamilyDetail(student.Id, TenantId);
			family.UpdateFatherDetails(new ParentGuardianDetails("Saved father", null, null, null, "9000000002", null, null, null, null, null, null, null, null));
			family.UpdateGuardianDetails(new ParentGuardianDetails(null, null, null, null, null, null, null, null, null, null, null, null, null));
			StudentFamilyContactSelection.Select(student, "father", family);
			await services.GetRequiredService<IStudentRepository>().UpdateAsync(student, true);
			await services.GetRequiredService<IRepository<StudentFamilyDetail, Guid>>().InsertAsync(family, true);
			var admissionFamily = new AdmissionFamily(Guid.NewGuid(), TenantId, "INLINE-PG-FAMILY");
			await services.GetRequiredService<IRepository<AdmissionFamily, Guid>>().InsertAsync(admissionFamily, true);
			var applicant = new AdmissionApplicant(Guid.NewGuid(), TenantId, admissionFamily.Id, "Inline", null, "Provider", null, null, null, student.Id);
			await services.GetRequiredService<IRepository<AdmissionApplicant, Guid>>().InsertAsync(applicant, true);
			var formId = offering.ApplicationFormDefinitionId!.Value;
			var section = (await services.GetRequiredService<IRepository<AdmissionFormSectionDefinition, Guid>>().GetListAsync(x => x.FormDefinitionId == formId)).Single();
			await services.GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>().InsertAsync(new(Guid.NewGuid(), formId,
				section.Id, "MOTHER_MOBILE", "Mother mobile", AdmissionFormFieldType.Phone, AdmissionFormBindingType.System,
				"Mother.Mobile", false, false, 10, null, null, null, "{\"format\":\"phone\",\"phone\":true}", null, null), true);
			var application = new AdmissionApplication(Guid.NewGuid(), TenantId, "INLINE-PG-" + Guid.NewGuid().ToString("N"), applicant.Id, null,
				offering.AdmissionCycleId, offering.Id, offering.WorkflowDefinitionId!.Value, formId);
			application.Submit(); application.Approve(Guid.Parse(TestAuthHandler.TestUserId), null);
			await services.GetRequiredService<IRepository<AdmissionApplication, Guid>>().InsertAsync(application, true);
			var instance = new AdmissionFormInstance(Guid.NewGuid(), TenantId, application.Id, formId, 1);
			var effective = new AdmissionFormDto
			{
					Id = formId, Version = 1, Purpose = AdmissionFormPurpose.Registration,
					Sections = [new AdmissionFormSectionDto
					{
						Id = section.Id, FormDefinitionId = formId, Code = section.Code, Name = section.Name,
						Fields = [new AdmissionFormFieldDto { Id = Guid.NewGuid(), FormDefinitionId = formId, SectionId = section.Id,
							Code = "MOTHER_MOBILE", Label = "Mother mobile", FieldType = AdmissionFormFieldType.Phone,
							BindingType = AdmissionFormBindingType.System, BindingKey = "Mother.Mobile", ValidationJson = "{\"format\":\"phone\",\"phone\":true}" },
							new AdmissionFormFieldDto { Id = Guid.NewGuid(), FormDefinitionId = formId, SectionId = section.Id,
								Code = "NOTES", Label = "Notes", FieldType = AdmissionFormFieldType.Text, BindingType = AdmissionFormBindingType.Custom },
							new AdmissionFormFieldDto { Id = Guid.NewGuid(), FormDefinitionId = formId, SectionId = section.Id,
								Code = "IDENTITY_DOCUMENTNAME", Label = "Document name", FieldType = AdmissionFormFieldType.Text,
								BindingType = AdmissionFormBindingType.System, BindingKey = "Identity.DocumentName" },
							new AdmissionFormFieldDto { Id = Guid.NewGuid(), FormDefinitionId = formId, SectionId = section.Id,
								Code = "APPLICANT_PROFILEPHOTO", Label = "Profile photo", FieldType = AdmissionFormFieldType.File,
								BindingType = AdmissionFormBindingType.System, BindingKey = "Applicant.ProfilePhoto" }]
					}]
			};
			// Optional metadata-only diagnostic input; values always come from this synthetic fixture.
			var metadataPath = Environment.GetEnvironmentVariable("STUDENT_RECORDS_INLINE_FORM_DEFINITION");
			if (!string.IsNullOrWhiteSpace(metadataPath))
			{
				effective = JsonSerializer.Deserialize<AdmissionFormDto>(await File.ReadAllTextAsync(metadataPath), JsonOptions)!;
				effective.Id = formId; effective.Version = 1; effective.Purpose = AdmissionFormPurpose.Registration;
				foreach (var capturedSection in effective.Sections)
				{
					capturedSection.Id = Guid.NewGuid(); capturedSection.FormDefinitionId = formId;
					foreach (var capturedField in capturedSection.Fields)
					{
						capturedField.Id = Guid.NewGuid(); capturedField.FormDefinitionId = formId;
						capturedField.SectionId = capturedSection.Id; capturedField.DefaultValue = null;
					}
				}
				output.WriteLine("Using metadata-only frozen schema with {0} fields; all answers are synthetic.", effective.Sections.Sum(x => x.Fields.Count));
			}
			AdmissionEffectiveFormStore.Freeze(instance, new AdmissionFormInheritancePreviewDto { Definition = effective });
			instance.Submit(Guid.Parse(TestAuthHandler.TestUserId));
			await services.GetRequiredService<IRepository<AdmissionFormInstance, Guid>>().InsertAsync(instance, true);
			var originalJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["NOTES"] = "Original submitted note" });
			var submission = new AdmissionFormSubmission(Guid.NewGuid(), TenantId, instance.Id, application.Id, 1,
				Guid.Parse(TestAuthHandler.TestUserId), originalJson);
			await services.GetRequiredService<IRepository<AdmissionFormSubmission, Guid>>().InsertAsync(submission, true);
			var onboarding = new StudentOnboardingCase(Guid.NewGuid(), TenantId, application.Id, applicant.Id, student.Id, offering.Id,
				factory.SeededAcademicYearId, offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId,
				"inline-pg-" + Guid.NewGuid().ToString("N"), null, DateTime.UtcNow.AddDays(3));
			await services.GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().InsertAsync(onboarding, true);
			var enrollment = StudentEnrollment.Create(Guid.NewGuid(), TenantId, student.Id, factory.SeededAcademicYearId,
				student.StandardId, student.DivisionId, 7, onboarding.Id);
			await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(enrollment, true);
			onboarding.CompleteHistoricalImport(enrollment.Id, student.DivisionId, 7, DateTime.UtcNow);
			await services.GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().UpdateAsync(onboarding, true);
			application.FinalizeApprovedAdmission(student.Id, onboarding.Id, DateTime.UtcNow);
			await services.GetRequiredService<IRepository<AdmissionApplication, Guid>>().UpdateAsync(application, true);
			return new InlineFormPostgresFixture(student.Id, submission.Id, originalJson);
		});
	}
	private sealed record InlineFormPostgresFixture(Guid StudentId, Guid SubmissionId, string OriginalJson);
}
