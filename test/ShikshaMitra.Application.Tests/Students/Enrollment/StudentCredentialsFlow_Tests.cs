using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("activation")]
	[InlineData("completion")]
	public Task One_click_credentials_prepares_both_saved_owners_and_persists_idempotent_delivery_intents(string timing) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync(timing);
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var beforeSettings = (await service.GetSettingsAsync(fixture.Case.AcademicYearId)).Single(value => value.AcademicYearId == fixture.Case.AcademicYearId).ConfigurationJson;
			var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
			workspace.Recipients.Select(owner => owner.Relationship).ShouldBe(["self", "father", "mother", "guardian"]);
			workspace.Recipients.ShouldAllBe(owner => owner.CanSend);
			var firstInput = new SendStudentCredentialsDto { AccountType = "student", Relationship = "self",
				ExpectedEmail = fixture.StudentEmail, ConcurrencyStamp = workspace.ConcurrencyStamp };
			workspace = await service.SendCredentialsAsync(workspace.CaseId, firstInput);
			workspace.Recipients.Single(owner => owner.AccountType == "student").Delivery!.Status.ShouldBe("Pending");
			var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
			var first = await WithUnitOfWorkAsync(() => requests.GetAsync(row => row.CaseId == workspace.CaseId && row.AccountType == "student"));
			first.RequiresSavedOwnerValidation().ShouldBeTrue();
			if (timing == "completion") first.Message.ShouldNotBeNull().ShouldContain("completion");
			var repeated = await service.SendCredentialsAsync(workspace.CaseId, firstInput);
			repeated.ConcurrencyStamp.ShouldBe(workspace.ConcurrencyStamp);
			(await WithUnitOfWorkAsync(() => requests.CountAsync(row => row.CaseId == workspace.CaseId))).ShouldBe(1);
			workspace = await service.SendCredentialsAsync(workspace.CaseId, new() { AccountType = "guardian", ConfirmRecipientAccess = true, Relationship = "father",
				ExpectedEmail = fixture.FatherEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
			var item = await service.GetAsync(workspace.CaseId);
			item.Status.ShouldBe(timing == "completion" ? StudentOnboardingStatus.Completed : StudentOnboardingStatus.NeedsAttention);
			item.StudentAccountStatus.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
			item.GuardianAccountStatus.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
			workspace.Recipients.Single(owner => owner.Relationship == "mother").CanSend.ShouldBeFalse();
			(await WithUnitOfWorkAsync(() => requests.CountAsync(row => row.CaseId == item.Id))).ShouldBe(2);
			var users = GetRequiredService<IdentityUserManager>();
			foreach (var link in await service.GetPortalAccountsAsync(item.Id))
			{
				var user = await WithUnitOfWorkAsync(() => users.GetByIdAsync(link.UserId));
				user.PasswordHash.ShouldBeNull(); user.EmailConfirmed.ShouldBeFalse();
				(await WithUnitOfWorkAsync(() => users.GetRolesAsync(user))).ShouldBe([link.AccountType == "student" ? "Student" : "Parent"]);
			}
			(await service.GetSettingsAsync(item.AcademicYearId)).Single(value => value.AcademicYearId == item.AcademicYearId).ConfigurationJson.ShouldBe(beforeSettings);
			(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(row => row.StudentId == item.StudentId))).ShouldBe(1);
		});

	[Fact]
	public Task One_click_credentials_preserves_unrequested_account_policies() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: true, accountsEnabled: false);
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var before = (await service.GetSettingsAsync(fixture.Case.AcademicYearId)).Single(value => value.AcademicYearId == fixture.Case.AcademicYearId).ConfigurationJson;
			var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
			workspace.Recipients.ShouldAllBe(owner => !owner.CanSend);
			(await Should.ThrowAsync<BusinessException>(() => service.SendCredentialsAsync(workspace.CaseId,
				new() { AccountType = "guardian", ConfirmRecipientAccess = true, Relationship = "father", ExpectedEmail = fixture.FatherEmail,
					ConcurrencyStamp = workspace.ConcurrencyStamp }))).Code.ShouldBe(StudentOnboardingErrorCodes.PortalAccountInvalid);
			(await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldBeEmpty();
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(row => row.CaseId == workspace.CaseId))).ShouldBe(0);
			(await service.GetSettingsAsync(fixture.Case.AcademicYearId)).Single(value => value.AcademicYearId == fixture.Case.AcademicYearId).ConfigurationJson.ShouldBe(before);
		});

	[Fact]
	public Task One_click_credentials_manual_send_leaves_automatic_flags_unchanged_and_retains_failed_worklist_until_sent() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false);
			var service = GetRequiredService<IStudentOnboardingAppService>();
			await LinkOneClickFixtureAccountsAsync(fixture);
			var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
			var before = (await service.GetSettingsAsync(fixture.Case.AcademicYearId)).Single(value => value.AcademicYearId == fixture.Case.AcademicYearId).ConfigurationJson;
			workspace.Recipients.Where(owner => owner.Relationship is "self" or "father").ShouldAllBe(owner => owner.CanSend);
			workspace.Recipients.Single(owner => owner.Relationship == "mother").CanSend.ShouldBeFalse();
			var legacy = GetRequiredService<IStudentCredentialEmailAppService>();
			(await Should.ThrowAsync<BusinessException>(() => legacy.RequestAsync(workspace.CaseId, new()
				{ AccountType = "student", ConcurrencyStamp = workspace.ConcurrencyStamp }))).Code.ShouldBe(StudentOnboardingErrorCodes.PortalAccountInvalid);
			workspace = await service.SendCredentialsAsync(workspace.CaseId, new() { AccountType = "student", Relationship = "self",
				ExpectedEmail = fixture.StudentEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
			workspace = await service.SendCredentialsAsync(workspace.CaseId, new() { AccountType = "guardian", ConfirmRecipientAccess = true, Relationship = "father",
				ExpectedEmail = fixture.FatherEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
			var completed = await service.GetAsync(workspace.CaseId);
			completed.Status.ShouldBe(StudentOnboardingStatus.Completed); completed.CredentialsPending.ShouldBeTrue();
			var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
			await WithUnitOfWorkAsync(async () =>
			{
				foreach (var row in await requests.GetListAsync(row => row.CaseId == workspace.CaseId))
				{
					row.Claim(DateTime.UtcNow);
					if (row.AccountType == "student") row.Failed(DateTime.UtcNow); else row.Sent(DateTime.UtcNow);
					await requests.UpdateAsync(row, true);
				}
			});
			(await service.GetAsync(workspace.CaseId)).CredentialsPending.ShouldBeTrue();
			var pending = await service.GetListAsync(new() { AcademicYearId = completed.AcademicYearId, PendingTask = "credentials" });
			pending.TotalCount.ShouldBe(1); pending.Items.Single().Id.ShouldBe(completed.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var row = await requests.GetAsync(row => row.CaseId == workspace.CaseId && row.AccountType == "student");
				row.Requeue(TestUserId, DateTime.UtcNow.AddMinutes(3)); row.RequireSavedOwnerValidation();
				row.Claim(DateTime.UtcNow.AddMinutes(3)); row.Sent(DateTime.UtcNow.AddMinutes(3)); await requests.UpdateAsync(row, true);
			});
			(await service.GetAsync(workspace.CaseId)).Status.ShouldBe(StudentOnboardingStatus.Completed);
			(await service.GetAsync(workspace.CaseId)).CredentialsPending.ShouldBeFalse();
			(await service.GetListAsync(new() { AcademicYearId = completed.AcademicYearId, PendingTask = "credentials" })).TotalCount.ShouldBe(0);
			(await service.GetSettingsAsync(fixture.Case.AcademicYearId)).Single(value => value.AcademicYearId == fixture.Case.AcademicYearId).ConfigurationJson.ShouldBe(before);
		});

	[Fact]
	public Task One_click_credentials_manual_new_accounts_leave_delivery_pending_until_sent_without_changing_automatic_flags() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false);
			var service = GetRequiredService<IStudentOnboardingAppService>(); var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
			workspace = await service.SendCredentialsAsync(workspace.CaseId, new() { AccountType = "student", Relationship = "self",
				ExpectedEmail = fixture.StudentEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
			workspace = await service.SendCredentialsAsync(workspace.CaseId, new() { AccountType = "guardian", ConfirmRecipientAccess = true, Relationship = "father",
				ExpectedEmail = fixture.FatherEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
			var item = await service.GetAsync(workspace.CaseId); item.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention); item.CredentialsPending.ShouldBeTrue();
			await WithUnitOfWorkAsync(async () =>
			{
				var repository = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
				foreach (var row in await repository.GetListAsync(row => row.CaseId == item.Id))
				{ row.Claim(DateTime.UtcNow); row.Sent(DateTime.UtcNow); await repository.UpdateAsync(row, true); }
			});
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item.Status.ShouldBe(StudentOnboardingStatus.Completed); item.CredentialsPending.ShouldBeFalse();
		});

	[Fact]
	public Task One_click_credentials_rejects_email_drift_stale_case_tenant_and_unassigned_staff_scope() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync("activation");
			var service = GetRequiredService<IStudentOnboardingAppService>(); var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
			var input = new SendStudentCredentialsDto { AccountType = "guardian", ConfirmRecipientAccess = true, Relationship = "father",
				ExpectedEmail = "different@example.test", ConcurrencyStamp = workspace.ConcurrencyStamp };
			(await Should.ThrowAsync<BusinessException>(() => service.SendCredentialsAsync(workspace.CaseId, input))).Code.ShouldBe(StudentOnboardingErrorCodes.PortalAccountInvalid);
			input.ExpectedEmail = fixture.FatherEmail; input.ConcurrencyStamp = "stale";
			(await Should.ThrowAsync<BusinessException>(() => service.SendCredentialsAsync(workspace.CaseId, input))).Code.ShouldBe(StudentOnboardingErrorCodes.ConcurrencyConflict);
			input.ConcurrencyStamp = workspace.ConcurrencyStamp;
			using (_currentTenant.Change(Guid.NewGuid()))
			{
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetCredentialsAsync(workspace.CaseId));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.SendCredentialsAsync(workspace.CaseId, input));
			}
			using (_principalAccessor.Change(EligibilityPrincipal(Guid.NewGuid(), "credentials-no-scope", "staff")))
			{
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetCredentialsAsync(workspace.CaseId));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.SendCredentialsAsync(workspace.CaseId, input));
			}
			(await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldBeEmpty();
		});

	[Fact]
	public Task One_click_credentials_does_not_expose_guardian_fallback_and_accepts_only_an_actual_applied_student_email_correction() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync("activation", captureStudentEmail: false);
			var service = GetRequiredService<IStudentOnboardingAppService>(); var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
			var missing = workspace.Recipients.Single(owner => owner.AccountType == "student");
			missing.Email.ShouldBeNull(); missing.CanSend.ShouldBeFalse();
			var eligibility = GetRequiredService<StudentOnboardingCredentialEligibilityResolver>();
			(await WithUnitOfWorkAsync(() => eligibility.ResolveAccountPreparationAsync(workspace.CaseId, "student"))).IsReady.ShouldBeFalse();
			(await WithUnitOfWorkAsync(() => eligibility.ResolveAsync(workspace.CaseId, "student"))).Email.ShouldBeEmpty();
			await WithUnitOfWorkAsync(async () =>
			{
				var student = await _studentRepository.GetRegistrationAggregateAsync(fixture.Case.StudentId); var previous = student!.ContactDetail!.Email;
				student.ApplyVerifiedRegistrationContact(student.ContactDetail.PrimaryContactNo, student.ContactDetail.SecondaryContactNo,
					fixture.StudentEmail, student.ContactDetail.Address1, student.ContactDetail.Address2, student.ContactDetail.Address3,
					student.ContactDetail.City, student.ContactDetail.State, student.ContactDetail.Country, student.ContactDetail.Pincode);
				await _studentRepository.UpdateAsync(student, true);
				var change = new StudentRecordChange(Guid.NewGuid(), TestTenantId, student.Id, fixture.Case.AcademicYearId, TestUserId,
					"general", "Correction", "credential-email-correction", "Student email corrected by approved Records workflow",
					JsonSerializer.Serialize(new { email = previous }), JsonSerializer.Serialize(new { email = fixture.StudentEmail }), "{}", null, "[]", 0, DateTimeOffset.UtcNow);
				change.Apply(DateTimeOffset.UtcNow); await GetRequiredService<IRepository<StudentRecordChange, Guid>>().InsertAsync(change, true);
			});
			workspace = await service.GetCredentialsAsync(workspace.CaseId);
			workspace.Recipients.Single(owner => owner.AccountType == "student").Email.ShouldBe(fixture.StudentEmail);
			workspace.Recipients.Single(owner => owner.AccountType == "student").CanSend.ShouldBeTrue();
			await service.SendCredentialsAsync(workspace.CaseId, new() { AccountType = "student", Relationship = "self",
				ExpectedEmail = fixture.StudentEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
		});

	[Theory]
	[InlineData("edit")]
	[InlineData("create")]
	[InlineData("update")]
	public Task One_click_credentials_direct_command_enforces_exact_action_and_identity_permissions(string action) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync("activation");
			var permission = action == "edit" ? ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit
				: action == "create" ? IdentityPermissions.Users.Create : IdentityPermissions.Users.Update;
			if (action == "update")
			{
				var sibling = await CreateExistingStudentAsync(fixture.Offering, "CredentialSibling", "QA");
				await WithUnitOfWorkAsync(async () =>
				{
					var manager = GetRequiredService<IdentityUserManager>();
					var parent = new IdentityUser(Guid.NewGuid(), fixture.FatherEmail, fixture.FatherEmail, TestTenantId);
					(await manager.CreateAsync(parent, "Existing-Parent-Password-29!")).Succeeded.ShouldBeTrue();
					(await manager.AddToRoleAsync(parent, "Parent")).Succeeded.ShouldBeTrue(); parent.SetEmailConfirmed(true);
					(await manager.UpdateAsync(parent)).Succeeded.ShouldBeTrue();
					await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId,
						sibling.Id, parent.Id, "guardian", "father", TestUserId), true);
				});
			}
			var service = CreateDeniedCredentialsService(permission);
			if (action != "edit")
			{
				var workspace = await WithUnitOfWorkAsync(() => service.GetCredentialsAsync(fixture.Case.Id));
				workspace.Recipients.Single(owner => owner.Relationship == "father").CanSend.ShouldBeFalse();
			}
			var error = await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => service.SendCredentialsAsync(fixture.Case.Id,
				new() { AccountType = "guardian", ConfirmRecipientAccess = true, Relationship = "father", ExpectedEmail = fixture.FatherEmail, ConcurrencyStamp = fixture.Case.ConcurrencyStamp })));
			error.Data["PolicyName"].ShouldBe(permission);
			(await GetRequiredService<IStudentOnboardingAppService>().GetPortalAccountsAsync(fixture.Case.Id)).ShouldBeEmpty();
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(row => row.CaseId == fixture.Case.Id))).ShouldBe(0);
		});

	[Fact]
	public Task One_click_credentials_reconciles_existing_account_handoffs_before_completion_time_delivery() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync("completion", emailEnabled: false);
			var links = GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>();
			await WithUnitOfWorkAsync(async () =>
			{
				var manager = GetRequiredService<IdentityUserManager>();
				foreach (var owner in new[] { (Type: "student", Relationship: "self", Email: fixture.StudentEmail),
					(Type: "guardian", Relationship: "father", Email: fixture.FatherEmail) })
				{
					var user = new IdentityUser(Guid.NewGuid(), owner.Email, owner.Email, TestTenantId);
					(await manager.CreateAsync(user)).Succeeded.ShouldBeTrue();
					(await manager.AddToRoleAsync(user, owner.Type == "student" ? "Student" : "Parent")).Succeeded.ShouldBeTrue();
					await links.InsertAsync(new(Guid.NewGuid(), TestTenantId, fixture.Case.StudentId, user.Id,
						owner.Type, owner.Relationship, TestUserId), true);
				}
			});
			var service = GetRequiredService<IStudentOnboardingAppService>(); var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
			workspace.Recipients.Single(owner => owner.AccountType == "student").CanSend.ShouldBeTrue();
			workspace = await service.SendCredentialsAsync(workspace.CaseId, new() { AccountType = "student", Relationship = "self",
				ExpectedEmail = fixture.StudentEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
			(await service.GetAsync(workspace.CaseId)).Status.ShouldBe(StudentOnboardingStatus.Completed);
			var request = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>()
				.GetAsync(row => row.CaseId == workspace.CaseId));
			request.Status.ShouldBe("Pending"); request.IsDue(DateTime.UtcNow).ShouldBeTrue();
		});

	[Fact]
	public Task One_click_credentials_upgrades_only_an_unclaimed_exact_automatic_intent_with_current_case_stamp() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false);
			await LinkOneClickFixtureAccountsAsync(fixture);
			var service = GetRequiredService<IStudentOnboardingAppService>(); var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
			var link = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>()
				.GetAsync(value => value.StudentId == fixture.Case.StudentId && value.AccountType == "guardian"));
			var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
			var row = new StudentCredentialEmailRequest(Guid.NewGuid(), TestTenantId, fixture.Case.Id, fixture.Case.StudentId,
				link.Id, link.UserId, "guardian", fixture.FatherEmail, Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-3));
			await WithUnitOfWorkAsync(() => requests.InsertAsync(row, true));
			var input = new SendStudentCredentialsDto { AccountType = "guardian", ConfirmRecipientAccess = true, Relationship = "father",
				ExpectedEmail = fixture.FatherEmail, ConcurrencyStamp = "stale" };
			(await Should.ThrowAsync<BusinessException>(() => service.SendCredentialsAsync(workspace.CaseId, input))).Code.ShouldBe(StudentOnboardingErrorCodes.ConcurrencyConflict);
			input.ConcurrencyStamp = workspace.ConcurrencyStamp;
			await service.SendCredentialsAsync(workspace.CaseId, input);
			var upgraded = await WithUnitOfWorkAsync(() => requests.GetAsync(row.Id));
			upgraded.RequiresSavedOwnerValidation().ShouldBeTrue(); upgraded.RequestedByUserId.ShouldBe(TestUserId);
			upgraded.Status.ShouldBe("Pending"); upgraded.AttemptCount.ShouldBe(0);
			(await WithUnitOfWorkAsync(() => requests.CountAsync(value => value.CaseId == fixture.Case.Id))).ShouldBe(1);
		});

	private async Task<OneClickCredentialsFixture> CreateOneClickCredentialsCaseAsync(string timing, bool captureStudentEmail = true,
		bool emailEnabled = true, bool accountsEnabled = true, string? studentAccess = null, string? guardianAccess = null,
		string provisioningMode = "manual", bool confirmFees = true)
	{
		var fixture = await CreateAutomaticRegressionFixtureAsync("OneClickCredentials");
		var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
		var service = GetRequiredService<IStudentOnboardingAppService>(); var settings = (await service.GetSettingsAsync(cycle.AcademicYearId)).Single(value => value.AcademicYearId == cycle.AcademicYearId);
		var config = JsonNode.Parse(settings.ConfigurationJson)!.AsObject(); var access = config["readinessAccess"]!.AsObject();
		access["studentAccount"] = studentAccess == null ? accountsEnabled : studentAccess == "required";
		access["guardianAccount"] = guardianAccess == null ? accountsEnabled : guardianAccess == "required";
		if (studentAccess != null) access["studentAccountAccess"] = studentAccess;
		if (guardianAccess != null) access["guardianAccountAccess"] = guardianAccess;
		access["sendStudentCredentialEmail"] = emailEnabled; access["sendGuardianCredentialEmail"] = emailEnabled;
		access["credentialSendingTime"] = timing; access["credentialProvisioningMode"] = provisioningMode;
		await service.SaveSettingsAsync(new() { AcademicYearId = cycle.AcademicYearId, ConfigurationJson = config.ToJsonString(), ConcurrencyStamp = settings.ConcurrencyStamp });
		var studentEmail = $"student-{Guid.NewGuid():N}@example.test"; var fatherEmail = $"father-{Guid.NewGuid():N}@example.test"; var motherEmail = $"mother-{Guid.NewGuid():N}@example.test";
		if (captureStudentEmail) await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(fixture.Applicant.Id);
			applicant.ExtraProperties["AdmissionStudentField.Applicant.Email"] = studentEmail; await _applicantRepository.UpdateAsync(applicant, true);
		});
		var item = await PrepareAutomaticRegressionCaseAsync(fixture, true, "one-click-credentials", confirmFeesBeforePlacement: true, confirmFees: confirmFees);
		await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>(); var family = await families.GetAsync(item.StudentId);
			family.UpdateFatherDetails(new ParentGuardianDetails("Saved Father", null, null, fatherEmail, "9000000002", null, null, null, null, null, null, null, null));
			family.UpdateMotherDetails(new ParentGuardianDetails("Saved Mother", null, null, motherEmail, "9000000003", null, null, null, null, null, null, null, null));
			await families.UpdateAsync(family, true);
			var roles = GetRequiredService<IdentityRoleManager>();
			foreach (var name in new[] { "Student", "Parent" })
				if (!await roles.RoleExistsAsync(name)) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), name, TestTenantId))).Succeeded.ShouldBeTrue();
		});
		item = await service.ActivateAsync(item.Id, new() { IdempotencyKey = "one-click-activation", ConcurrencyStamp = item.ConcurrencyStamp });
		return new(item, fixture.Offering, studentEmail, fatherEmail);
	}

	private StudentOnboardingAppService CreateDeniedCredentialsService(string permission)
	{
		var provider = GetRequiredService<IServiceProvider>();
		var authorization = Substitute.For<IAbpAuthorizationService>(); authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>()).Returns(call =>
			Task.FromResult(call.ArgAt<string>(2) == permission ? AuthorizationResult.Failed() : AuthorizationResult.Success()));
		var service = ActivatorUtilities.CreateInstance<StudentOnboardingAppService>(provider);
		service.LazyServiceProvider = new AbpLazyServiceProvider(new CredentialsAuthorizationProvider(provider, authorization));
		return service;
	}
	private async Task LinkOneClickFixtureAccountsAsync(OneClickCredentialsFixture fixture)
	{
		var service = GetRequiredService<IStudentOnboardingAppService>(); var item = await service.GetAsync(fixture.Case.Id);
		await service.ProvisionPortalAccountAsync(item.Id, new() { AccountType = "student", Relationship = "self", Email = fixture.StudentEmail,
			RelationshipVerified = true, ConcurrencyStamp = item.ConcurrencyStamp });
		item = await service.GetAsync(item.Id);
		await service.ProvisionPortalAccountAsync(item.Id, new() { AccountType = "guardian", Relationship = "father", Email = fixture.FatherEmail,
			RelationshipVerified = true, ConcurrencyStamp = item.ConcurrencyStamp });
	}
	private sealed class CredentialsAuthorizationProvider(IServiceProvider fallback, IAuthorizationService authorization) : IServiceProvider
	{
		public object? GetService(Type type) => type == typeof(IAuthorizationService) ? authorization : fallback.GetService(type);
	}
	private record OneClickCredentialsFixture(StudentOnboardingCaseDto Case, AdmissionOffering Offering, string StudentEmail, string FatherEmail);
}
