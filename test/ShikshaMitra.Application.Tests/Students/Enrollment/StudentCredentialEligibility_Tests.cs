using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups;
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
			var fixture = await CreateCertificationFixtureAsync("CredentialGates");
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var service = GetRequiredService<IStudentOnboardingAppService>();
			await service.CreatePolicyAsync(new() { AcademicYearId = cycle.AcademicYearId, Name = "Credential gates",
				RequireDocuments = false, RequireFamilyDetails = false, RequireCommunicationDetails = false,
				RequireStudentAccount = true, RequireGuardianAccount = false });
			var finalized = await FinalizeAsync(fixture.Application, true, "credential-gates");
			var email = $"credential-{Guid.NewGuid():N}@example.test";
			await WithUnitOfWorkAsync(async () =>
			{
				var roles = GetRequiredService<IdentityRoleManager>();
				if (!await roles.RoleExistsAsync("Student")) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "Student", TestTenantId))).Succeeded.ShouldBeTrue();
				var student = await _studentRepository.GetRegistrationAggregateAsync(finalized.StudentId);
				student!.ApplyVerifiedRegistrationContact("9000000001", null, email, "Test address", null, null, null, null, null, null);
				if (legacyNumericGrOnly) student.AssignGrNo(724501);
				await _studentRepository.UpdateAsync(student, true);
			});
			var division = await WithUnitOfWorkAsync(async () =>
			{
				var value = await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, "CredA", 40);
				return await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(value, true);
			});
			var item = await service.GetAsync(finalized.OnboardingCaseId);
			item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
			var series = await service.CreateIdentifierSeriesAsync(new() { AcademicYearId = cycle.AcademicYearId,
				Code = "CRED", Name = "Credential test series", EffectiveFrom = DateTime.UtcNow.AddDays(-1) });
			item = await service.AllocateIdentifierAsync(item.Id, new() { NumberSeriesId = series.Id, RollNumber = 1, ConcurrencyStamp = item.ConcurrencyStamp });
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item = await service.ActivateAsync(item.Id, new() { IdempotencyKey = "credential-gates-activate", ConcurrencyStamp = item.ConcurrencyStamp });
			if (legacyNumericGrOnly)
				await WithUnitOfWorkAsync(async () =>
				{
					var student = await _studentRepository.GetRegistrationAggregateAsync(finalized.StudentId);
					student!.GrNumber.ShouldBeNullOrEmpty(); student.GrNo.ShouldBe(724501);
				});
			var resolver = GetRequiredService<IStudentCredentialEmailEligibilityResolver>();
			(await WithUnitOfWorkAsync(() => resolver.ResolveAsync(item.Id, "student"))).EmailEnabled.ShouldBeFalse();
			var saved = await service.SaveSettingsAsync(new() { AcademicYearId = cycle.AcademicYearId,
				ConfigurationJson = CredentialGateConfiguration("activation", "automatic") });
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			var portal = (await service.GetPortalAccountsAsync(item.Id)).Single();
			(await WithUnitOfWorkAsync(async () => (await GetRequiredService<IdentityUserManager>().FindByIdAsync(portal.UserId.ToString()))!.PhoneNumber)).ShouldBe("9000000001");
			var queued = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
			(await WithUnitOfWorkAsync(() => queued.CountAsync(row => row.CaseId == item.Id))).ShouldBe(1);
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			(await WithUnitOfWorkAsync(() => queued.CountAsync(row => row.CaseId == item.Id))).ShouldBe(1);
			(await WithUnitOfWorkAsync(() => resolver.ResolveAsync(item.Id, "student"))).IsReady.ShouldBeTrue();
			if (legacyNumericGrOnly)
			{
				await WithUnitOfWorkAsync(async () =>
				{
					var student = await _studentRepository.GetRegistrationAggregateAsync(finalized.StudentId);
					student!.AssignGrNo(null); await _studentRepository.UpdateAsync(student, true);
				});
				(await WithUnitOfWorkAsync(() => resolver.ResolveAsync(item.Id, "student"))).BlockedReason.ShouldNotBeNull().ShouldContain("GR number");
				await WithUnitOfWorkAsync(async () =>
				{
					var student = await _studentRepository.GetRegistrationAggregateAsync(finalized.StudentId);
					student!.AssignGrNo(724501); await _studentRepository.UpdateAsync(student, true);
				});
				(await WithUnitOfWorkAsync(() => resolver.ResolveAsync(item.Id, "student"))).IsReady.ShouldBeTrue();
			}
			var type = await CreateHandoffDocumentTypeAsync();
			(await WithUnitOfWorkAsync(() => resolver.ResolveAsync(item.Id, "student"))).BlockedReason.ShouldNotBeNull().ShouldContain("every required document");
			var documents = GetRequiredService<IStudentOnboardingDocumentAppService>();
			var before = await documents.GetAsync(item.Id);
			await Should.ThrowAsync<BusinessException>(() => documents.OverrideAsync(item.Id, new() { OverrideAll = true,
				Reason = "Reviewed", ConcurrencyStamp = "stale" }));
			var after = await documents.OverrideAsync(item.Id, new() { DocumentTypeIds = [type.Id], Reason = "Verified original by authorized staff",
				ConcurrencyStamp = before.ConcurrencyStamp });
			after.Documents.Single(value => value.DocumentTypeId == type.Id).IsOverridden.ShouldBeTrue();
			(await WithUnitOfWorkAsync(() => resolver.ResolveAsync(item.Id, "student"))).IsReady.ShouldBeTrue();
			await service.SaveSettingsAsync(new() { AcademicYearId = cycle.AcademicYearId, ConfigurationJson = CredentialGateConfiguration("completion"),
				ConcurrencyStamp = saved.ConcurrencyStamp });
			(await WithUnitOfWorkAsync(() => resolver.ResolveAsync(item.Id, "student"))).BlockedReason.ShouldNotBeNull().ShouldContain("full onboarding completion");
			await WithUnitOfWorkAsync(async () =>
			{
				var student = await _studentRepository.GetAsync(finalized.StudentId); student.ChangeStatus(StudentLifeCycleStatusEnum.Left);
				await _studentRepository.UpdateAsync(student, true);
			});
			(await WithUnitOfWorkAsync(() => resolver.ResolveAsync(item.Id, "student"))).IsReady.ShouldBeFalse();
		});

	private static string CredentialGateConfiguration(string time, string mode = "manual") => JsonSerializer.Serialize(new
	{
		readinessAccess = new { requireDocuments = false, studentAccount = true, guardianAccount = false,
			sendStudentCredentialEmail = true, sendGuardianCredentialEmail = false, credentialSendingTime = time, credentialProvisioningMode = mode },
		onboardingProfiles = Array.Empty<object>(), grNumber = new { configurations = Array.Empty<object>() },
		rollNumber = new { configurations = Array.Empty<object>() }, divisionChange = new { configurations = Array.Empty<object>() }
	});
}
