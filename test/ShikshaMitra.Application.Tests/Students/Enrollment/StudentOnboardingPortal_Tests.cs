using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Onboarding_portal_password_setup_does_not_confirm_an_unlinked_identity() =>
		RunCertificationScenarioAsync(async () =>
		{
			var users = GetRequiredService<IdentityUserManager>();
			var user = new IdentityUser(Guid.NewGuid(), $"unlinked-{Guid.NewGuid():N}", "unlinked@example.test", TestTenantId);
			await WithUnitOfWorkAsync(async () => (await users.CreateAsync(user)).Succeeded.ShouldBeTrue());
			var token = await WithUnitOfWorkAsync(() => users.GeneratePasswordResetTokenAsync(user));
			await GetRequiredService<IAccountAppService>().ResetPasswordAsync(new ResetPasswordDto
			{
				UserId = user.Id, ResetToken = token, Password = "Unlinked-Test-Only-29!"
			});
			(await WithUnitOfWorkAsync(() => users.GetByIdAsync(user.Id))).EmailConfirmed.ShouldBeFalse();
		});

	[Theory]
	[InlineData("student", "self", "Student")]
	[InlineData("guardian", "father", "Parent")]
	public Task Onboarding_portal_accounts_are_persisted_idempotent_scoped_and_passwordless(string type, string relationship, string role) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("JayPortal");
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var service = GetRequiredService<IStudentOnboardingAppService>();
			await service.CreatePolicyAsync(new() { AcademicYearId = cycle.AcademicYearId, Name = "Portal test policy",
				RequireDocuments = false, RequireFamilyDetails = false, RequireCommunicationDetails = false,
				RequireStudentAccount = type == "student", RequireGuardianAccount = type == "guardian" });
			var finalized = await FinalizeAsync(fixture.Application, true, "portal-test");
			var email = $"portal-{Guid.NewGuid():N}@example.test";
			var users = GetRequiredService<IdentityUserManager>();
			await WithUnitOfWorkAsync(async () =>
			{
				var roles = GetRequiredService<IdentityRoleManager>();
				if (!await roles.RoleExistsAsync(role))
					(await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), role, TestTenantId))).Succeeded.ShouldBeTrue();
				var student = await _studentRepository.GetRegistrationAggregateAsync(finalized.StudentId);
				student!.ApplyVerifiedRegistrationContact("9000000001", null, email, "Test address", null, null, null, null, null, null);
				await _studentRepository.UpdateAsync(student, true);
				var familyRepo = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
				var family = await familyRepo.FindAsync(student.Id);
				var newFamily = family == null;
				family ??= new StudentFamilyDetail(student.Id, TestTenantId);
				family.UpdateFatherDetails(new ParentGuardianDetails("Test Father", null, null, email, "9000000002", null, null, null, null, null, null, null, null));
				if (newFamily) await familyRepo.InsertAsync(family, true); else await familyRepo.UpdateAsync(family, true);
			});
			var division = await WithUnitOfWorkAsync(async () =>
			{
				var value = await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, "PortalA", 40);
				return await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(value, true);
			});
			var item = await service.GetAsync(finalized.OnboardingCaseId);
			var input = new ProvisionStudentPortalAccountDto { AccountType = type, Relationship = relationship, Email = email,
				RelationshipVerified = true, ConcurrencyStamp = item.ConcurrencyStamp };
			(await Should.ThrowAsync<BusinessException>(() => service.ProvisionPortalAccountAsync(item.Id, input)))
				.Code.ShouldBe(StudentOnboardingErrorCodes.CaseNotEditable);
			item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
			var series = await service.CreateIdentifierSeriesAsync(new() { AcademicYearId = cycle.AcademicYearId,
				Code = "PORTAL", Name = "Portal test series", EffectiveFrom = DateTime.UtcNow.AddDays(-1) });
			item = await service.AllocateIdentifierAsync(item.Id, new() { NumberSeriesId = series.Id, RollNumber = 1, ConcurrencyStamp = item.ConcurrencyStamp });
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item = await service.ActivateAsync(item.Id, new() { IdempotencyKey = "portal-activation", ConcurrencyStamp = item.ConcurrencyStamp });
			item.Status.ShouldBe(StudentOnboardingStatus.Provisioning);
			item.StudentAccountRequired.ShouldBe(type == "student");
			item.GuardianAccountRequired.ShouldBe(type == "guardian");
			input.ConcurrencyStamp = item.ConcurrencyStamp;
			input.Email = "not-the-saved-owner@example.test";
			(await Should.ThrowAsync<BusinessException>(() => service.ProvisionPortalAccountAsync(item.Id, input)))
				.Code.ShouldBe(StudentOnboardingErrorCodes.PortalAccountInvalid);
			input.Email = email;
			input.RelationshipVerified = false;
			var validation = await Should.ThrowAsync<AbpValidationException>(() => service.ProvisionPortalAccountAsync(item.Id, input));
			validation.ValidationErrors.ShouldContain(error => error.MemberNames.Contains(nameof(input.RelationshipVerified)));
			input.RelationshipVerified = true;
			var result = await service.ProvisionPortalAccountAsync(item.Id, input);
			(await service.ProvisionPortalAccountAsync(item.Id, input)).UserId.ShouldBe(result.UserId);
			var user = await WithUnitOfWorkAsync(() => users.GetByIdAsync(result.UserId));
			user.PasswordHash.ShouldBeNull();
			user.EmailConfirmed.ShouldBeFalse();
			(await WithUnitOfWorkAsync(() => users.GetRolesAsync(user))).ShouldBe([role]);
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>()
				.CountAsync(x => x.StudentId == finalized.StudentId))).ShouldBe(1);
			using (_principalAccessor.Change(EligibilityPrincipal(user.Id, email, role)))
			{
				await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetMyPortalStudentsAsync());
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetPortalAccountsAsync(item.Id));
			}
			// Exercise the existing owner password-setup service, not a staff email-confirmation bypass.
			var accountService = GetRequiredService<IAccountAppService>();
			await Should.ThrowAsync<Exception>(() => accountService.ResetPasswordAsync(new ResetPasswordDto
			{
				UserId = user.Id, ResetToken = "invalid-owner-token", Password = "Portal-Test-Only-29!"
			}));
			(await WithUnitOfWorkAsync(() => users.GetByIdAsync(user.Id))).EmailConfirmed.ShouldBeFalse();
			var resetToken = await WithUnitOfWorkAsync(async () => await users.GeneratePasswordResetTokenAsync(await users.GetByIdAsync(user.Id)));
			await accountService.ResetPasswordAsync(new ResetPasswordDto
			{
				UserId = user.Id, ResetToken = resetToken, Password = "Portal-Test-Only-29!"
			});
			(await WithUnitOfWorkAsync(() => users.GetByIdAsync(user.Id))).EmailConfirmed.ShouldBeTrue();
			await Should.ThrowAsync<Exception>(() => accountService.ResetPasswordAsync(new ResetPasswordDto
			{
				UserId = user.Id, ResetToken = resetToken, Password = "Portal-Test-Replay-29!"
			}));
			using (_principalAccessor.Change(EligibilityPrincipal(user.Id, email, role)))
				(await service.GetMyPortalStudentsAsync()).Single().StudentId.ShouldBe(finalized.StudentId);
			using (_currentTenant.Change(Guid.NewGuid()))
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetPortalAccountsAsync(item.Id));
			item = await service.GetAsync(item.Id);
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention); // Fee assignment/payment still missing.
			(type == "student" ? item.StudentAccountStatus : item.GuardianAccountStatus)
				.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
			(await service.GetServiceHandoffsAsync(item.Id)).Single(x => x.ServiceKey == type + "-account")
				.Status.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
		});
}
