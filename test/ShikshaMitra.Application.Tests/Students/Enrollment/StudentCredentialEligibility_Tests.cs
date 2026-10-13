using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Credential_email_rechecks_real_enrollment_documents_overrides_and_school_timing(bool legacyNumericGrOnly) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync("completion");
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var item = fixture.Case;
			if (legacyNumericGrOnly)
				await WithUnitOfWorkAsync(async () =>
				{
					// Represent persisted numeric-only legacy storage, not an identifier-repair command.
					var student = await _studentRepository.GetRegistrationAggregateAsync(item.StudentId);
					typeof(Student).GetProperty(nameof(Student.GrNumber))!.SetValue(student, null);
					typeof(Student).GetProperty(nameof(Student.GrNo))!.SetValue(student, 724501);
					await _studentRepository.UpdateAsync(student!, true);
				});
			var workspace = await service.GetCredentialsAsync(item.Id);
			await service.SendCredentialsAsync(item.Id, new() { AccountType = "student", Relationship = "self",
				ExpectedEmail = fixture.StudentEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
			var resolver = GetRequiredService<IStudentCredentialEmailEligibilityResolver>();
			var waiting = await WithUnitOfWorkAsync(() => resolver.ResolveExplicitAsync(item.Id, "student"));
			waiting.EmailEnabled.ShouldBeTrue(); waiting.WaitingForCompletion.ShouldBeTrue(); waiting.IsReady.ShouldBeFalse();
			waiting.BlockedReason.ShouldNotBeNull().ShouldContain("full onboarding completion");
			var settings = (await service.GetSettingsAsync(item.AcademicYearId)).Single(value => value.AcademicYearId == item.AcademicYearId);
			var config = JsonNode.Parse(settings.ConfigurationJson)!.AsObject();
			config["readinessAccess"]!["credentialSendingTime"] = "activation";
			config["readinessAccess"]!["credentialProvisioningMode"] = "automatic";
			await service.SaveSettingsAsync(new() { AcademicYearId = item.AcademicYearId,
				ConfigurationJson = config.ToJsonString(), ConcurrencyStamp = settings.ConcurrencyStamp });
			// An enrolled case retains its reviewed timing instead of adopting later school defaults.
			(await WithUnitOfWorkAsync(() => resolver.ResolveExplicitAsync(item.Id, "student"))).WaitingForCompletion.ShouldBeTrue();
			var original = await WithUnitOfWorkAsync(async () =>
			{
				var student = await _studentRepository.GetRegistrationAggregateAsync(item.StudentId);
				return (Gr: student!.GrNumber, Numeric: student.GrNo, Roll: student.RollNo, Division: student.DivisionId);
			});
			await WithUnitOfWorkAsync(async () =>
			{
				var student = await _studentRepository.GetRegistrationAggregateAsync(item.StudentId);
				typeof(Student).GetProperty(nameof(Student.GrNumber))!.SetValue(student, null);
				typeof(Student).GetProperty(nameof(Student.GrNo))!.SetValue(student, null);
				await _studentRepository.UpdateAsync(student!, true);
			});
			(await WithUnitOfWorkAsync(() => resolver.ResolveExplicitAsync(item.Id, "student"))).BlockedReason.ShouldNotBeNull().ShouldContain("GR number");
			await WithUnitOfWorkAsync(async () =>
			{
				var student = await _studentRepository.GetRegistrationAggregateAsync(item.StudentId);
				typeof(Student).GetProperty(nameof(Student.GrNumber))!.SetValue(student, original.Gr);
				typeof(Student).GetProperty(nameof(Student.GrNo))!.SetValue(student, original.Numeric);
				typeof(Student).GetProperty(nameof(Student.RollNo))!.SetValue(student, original.Roll!.Value + 1);
				await _studentRepository.UpdateAsync(student!, true);
			});
			(await WithUnitOfWorkAsync(() => resolver.ResolveExplicitAsync(item.Id, "student"))).BlockedReason.ShouldNotBeNull().ShouldContain("roll number");
			var alternateDivision = await WithUnitOfWorkAsync(async () => await GetRequiredService<IRepository<Division, Guid>>()
				.InsertAsync(await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, "Credential mismatch", 40), true));
			await WithUnitOfWorkAsync(async () =>
			{
				var student = await _studentRepository.GetRegistrationAggregateAsync(item.StudentId);
				typeof(Student).GetProperty(nameof(Student.RollNo))!.SetValue(student, original.Roll);
				typeof(Student).GetProperty(nameof(Student.DivisionId))!.SetValue(student, alternateDivision.Id);
				await _studentRepository.UpdateAsync(student!, true);
			});
			(await WithUnitOfWorkAsync(() => resolver.ResolveExplicitAsync(item.Id, "student"))).BlockedReason.ShouldNotBeNull().ShouldContain("division");
			await WithUnitOfWorkAsync(async () =>
			{
				var student = await _studentRepository.GetRegistrationAggregateAsync(item.StudentId);
				typeof(Student).GetProperty(nameof(Student.DivisionId))!.SetValue(student, original.Division);
				await _studentRepository.UpdateAsync(student!, true);
				var enrollment = await _studentEnrollmentRepository.GetAsync(item.StudentEnrollmentId!.Value);
				typeof(StudentEnrollment).GetProperty(nameof(StudentEnrollment.IsActive))!.SetValue(enrollment, false);
				await _studentEnrollmentRepository.UpdateAsync(enrollment, true);
			});
			var inactive = await WithUnitOfWorkAsync(() => resolver.ResolveExplicitAsync(item.Id, "student"));
			inactive.WaitingForCompletion.ShouldBeFalse(); inactive.BlockedReason.ShouldNotBeNull().ShouldContain("enrollment");
			await WithUnitOfWorkAsync(async () =>
			{
				var enrollment = await _studentEnrollmentRepository.GetAsync(item.StudentEnrollmentId!.Value);
				typeof(StudentEnrollment).GetProperty(nameof(StudentEnrollment.IsActive))!.SetValue(enrollment, true);
				await _studentEnrollmentRepository.UpdateAsync(enrollment, true);
				var student = await _studentRepository.GetAsync(item.StudentId); student.ChangeStatus(StudentLifeCycleStatusEnum.Left);
				await _studentRepository.UpdateAsync(student, true);
			});
			var left = await WithUnitOfWorkAsync(() => resolver.ResolveExplicitAsync(item.Id, "student"));
			left.WaitingForCompletion.ShouldBeFalse(); left.BlockedReason.ShouldNotBeNull().ShouldContain("actively enrolled");
		});

	[Fact]
	public Task Credential_email_rechecks_real_Offering_document_evidence_and_rejects_onboarding_overrides() =>
		RunCertificationScenarioAsync(async () =>
		{
			var type = await CreateHandoffDocumentTypeAsync();
			var requirement = new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate", DocumentTypeId = type.Id };
			var fixture = await CreateOnboardingDocumentFixtureAsync(HandoffPolicy(requirement));
			var email = $"offering-document-owner-{Guid.NewGuid():N}@example.test";
			await WithUnitOfWorkAsync(async () =>
			{
				var applicant = await _applicantRepository.GetAsync(fixture.Application.ApplicantId);
				applicant.ExtraProperties["AdmissionStudentField.Applicant.Email"] = email; await _applicantRepository.UpdateAsync(applicant, true);
			});
			await AddHandoffEvidenceAsync(fixture, requirement);
			var application = await CompleteOnboardingDecisionAsync(fixture.Application.Id);
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			await ConfigureOfferingReadinessAsync(fixture.Offering, cycle.AcademicYearId, prepareAccess: true);
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var settings = (await service.GetSettingsAsync(cycle.AcademicYearId)).Single(value => value.AcademicYearId == cycle.AcademicYearId);
			var config = JsonNode.Parse(settings.ConfigurationJson)!.AsObject();
			config["readinessAccess"]!["sendStudentCredentialEmail"] = true;
			await service.SaveSettingsAsync(new() { AcademicYearId = cycle.AcademicYearId,
				ConfigurationJson = config.ToJsonString(), ConcurrencyStamp = settings.ConcurrencyStamp });
			var finalized = await FinalizeAsync(application, true, "credential-offering-documents");
			var item = await service.GetAsync(finalized.OnboardingCaseId);
			var structure = await CreateFeeSelectionReferenceAsync(fixture.Offering, cycle.AcademicYearId);
			await service.SaveFeeStructureAsync(item.Id, new() { StructureId = structure.Id, ConcurrencyStamp = item.ConcurrencyStamp });
			item = await service.GetAsync(item.Id);
			var division = await WithUnitOfWorkAsync(async () => await GetRequiredService<IRepository<Division, Guid>>()
				.InsertAsync(await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, "CredentialDoc", 40), true));
			item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
			item = await service.AllocateIdentifierAsync(item.Id, new() { NumberSeriesId = (await service.GetAvailableIdentifierSeriesAsync(item.Id)).Single().Id,
				RollNumber = 1, ConcurrencyStamp = item.ConcurrencyStamp });
			await WithUnitOfWorkAsync(async () =>
			{
				var roles = GetRequiredService<IdentityRoleManager>();
				if (!await roles.RoleExistsAsync("Student")) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "Student", TestTenantId))).Succeeded.ShouldBeTrue();
			});
			item = await service.ActivateAsync(item.Id, new() { IdempotencyKey = "credential-offering-activation", ConcurrencyStamp = item.ConcurrencyStamp });
			await service.SendCredentialsAsync(item.Id, new() { AccountType = "student", Relationship = "self", ExpectedEmail = email, ConcurrencyStamp = item.ConcurrencyStamp });
			var resolver = GetRequiredService<IStudentCredentialEmailEligibilityResolver>();
			(await WithUnitOfWorkAsync(() => resolver.ResolveExplicitAsync(item.Id, "student"))).IsReady.ShouldBeTrue();
			// A later imported/admin revision must invalidate the earlier acceptance for this real pinned requirement.
			await AddHandoffEvidenceAsync(fixture, requirement, version: 2, decision: "rejected");
			(await WithUnitOfWorkAsync(() => resolver.ResolveExplicitAsync(item.Id, "student"))).BlockedReason.ShouldNotBeNull().ShouldContain("Offering document");
			var documents = GetRequiredService<IStudentOnboardingDocumentAppService>(); var workspace = await documents.GetAsync(item.Id);
			await Should.ThrowAsync<BusinessException>(() => documents.OverrideAsync(item.Id, new() { DocumentTypeIds = [type.Id],
				Reason = "Staff cannot override a pinned Offering requirement", ConcurrencyStamp = workspace.ConcurrencyStamp }));
		});
}
