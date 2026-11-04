using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Pending_family_confirmation_joins_only_target_then_registration_reuses_available_parent_details_and_retry_is_safe() => RunCertificationScenarioAsync(async () =>
	{
		var (offering, source, target) = await CreatePendingFamilyConfirmationFixtureAsync();
		var sourceBefore = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(source.ApplicantId!.Value));
		var sourceApplicantId = source.ApplicantId!.Value;
		var targetApplicantId = target.ApplicantId!.Value;
		var sourceJson = JsonSerializer.Serialize(sourceBefore);
		var targetBefore = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(target.ApplicantId!.Value));
		var targetSnapshot = target.FormResponseSnapshotJson;
		var sourceFamily = await WithUnitOfWorkAsync(() => _familyRepository.GetAsync(source.FamilyId!.Value));
		var guardianBefore = JsonSerializer.Serialize(await WithUnitOfWorkAsync(() => _guardianRepository.GetAsync(sourceFamily.PrimaryGuardianId!.Value)));
		var confirmed = await ConfirmPendingFamilyAsync(target.Id, source.FamilyId!.Value, source.ApplicantId!.Value, "father", target.ConcurrencyStamp);
		confirmed.FamilyId.ShouldBe(source.FamilyId);
		ConfirmedFamilyFlag(confirmed).ShouldBeTrue();
		confirmed.ParentRelationship.ShouldBe("father");
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await _applicantRepository.GetAsync(target.ApplicantId!.Value);
			stored.FamilyId.ShouldBe(source.FamilyId!.Value);
			stored.ReservedStudentId.ShouldBe(targetBefore.ReservedStudentId);
			stored.FirstName.ShouldBe(targetBefore.FirstName);
			var proof = AdmissionSiblingParentSelection.ReadConfirmation(stored).ShouldNotBeNull();
			proof.SourceApplicantId.ShouldBe(source.ApplicantId!.Value);
			proof.ConfirmedBy.ShouldBe(TestUserId);
			var current = await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(target.Id);
			current.InquiryNumber.ShouldBe(target.InquiryNumber);
			current.FormResponseSnapshotJson.ShouldBe(targetSnapshot);
			JsonSerializer.Serialize(await _applicantRepository.GetAsync(source.ApplicantId!.Value)).ShouldBe(sourceJson);
			JsonSerializer.Serialize(await _guardianRepository.GetAsync(sourceFamily.PrimaryGuardianId!.Value)).ShouldBe(guardianBefore);
			(await _familyRepository.FindAsync(target.FamilyId!.Value)).ShouldNotBeNull();
		});
		await MarkInheritedInquiryInterestedAsync(target.Id);
		var application = await _applicationAppService.StartAsync(target.ApplicantId!.Value, offering.Id, target.Id);
		var form = await _applicationAppService.GetFormAsync(application.Id);
		AdmissionApplicationValueConverter.Unwrap(form.Values.Single(value => value.FieldCode == "FATHER_OCCUPATION").ValueJson).ShouldBe("Engineer");
		await WithUnitOfWorkAsync(async () =>
		{
			var changedSource = await _applicantRepository.GetAsync(sourceApplicantId);
			changedSource.SetProperty("AdmissionStudentField.Father.Email", "new-parent@example.com");
			await _applicantRepository.UpdateAsync(changedSource, true);
		});
		// Exact already-confirmed retry remains safe with its old stamp, even after source contact edits and target start.
		var retry = await ConfirmPendingFamilyAsync(target.Id, source.FamilyId!.Value, source.ApplicantId!.Value, "father", target.ConcurrencyStamp);
		ConfirmedFamilyFlag(retry).ShouldBeTrue();
		(await WithUnitOfWorkAsync(async () => AdmissionSiblingParentSelection.ReadConfirmation(await _applicantRepository.GetAsync(target.ApplicantId!.Value))!.Email)).ShouldBe("amit@example.com");
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionActivity, Guid>>().CountAsync(item => item.InquiryId == target.Id && item.ActivityType == "SiblingFamilyConfirmed"))).ShouldBe(1);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionActivity, Guid>>().CountAsync(item => item.InquiryId == source.Id && item.ActivityType == "SiblingFamilyConfirmed"))).ShouldBe(0);
	});

	[Fact]
	public Task Pending_family_confirmation_rejects_stale_wrong_role_contact_shared_and_downstream_targets() => RunCertificationScenarioAsync(async () =>
	{
		var (offering, source, target) = await CreatePendingFamilyConfirmationFixtureAsync();
		var sourceId = source.ApplicantId!.Value; var sourceFamilyId = source.FamilyId!.Value;
		(await Should.ThrowAsync<BusinessException>(() => ConfirmPendingFamilyAsync(target.Id, sourceFamilyId, sourceId, "father", "stale"))).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.ConcurrencyConflict);
		await Should.ThrowAsync<BusinessException>(() => ConfirmPendingFamilyAsync(target.Id, sourceFamilyId, target.ApplicantId!.Value, "father", target.ConcurrencyStamp));
		await Should.ThrowAsync<Exception>(() => ConfirmPendingFamilyAsync(target.Id, sourceFamilyId, sourceId, "guardian", target.ConcurrencyStamp));
		await Should.ThrowAsync<Exception>(() => ConfirmPendingFamilyAsync(target.Id, Guid.NewGuid(), sourceId, "father", target.ConcurrencyStamp));
		await Should.ThrowAsync<Exception>(() => ConfirmPendingFamilyAsync(target.Id, sourceFamilyId, Guid.NewGuid(), "father", target.ConcurrencyStamp));
		await WithUnitOfWorkAsync(async () =>
		{
			var inquiry = await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(target.Id);
			inquiry.UpdateDetails(inquiry.ApplicantName, "9876543212", "unmatched@example.com", inquiry.Source, inquiry.SourceDetail, inquiry.PreferredLanguageCode);
			await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().UpdateAsync(inquiry, true);
		});
		var current = await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(target.Id);
		(await Should.ThrowAsync<BusinessException>(() => ConfirmPendingFamilyAsync(target.Id, sourceFamilyId, sourceId, "father", current.ConcurrencyStamp))).Code.ShouldBe(AdmissionErrorCodes.InquiryFamilyMismatch);
		await WithUnitOfWorkAsync(async () =>
		{
			var inquiry = await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(target.Id);
			inquiry.UpdateDetails(inquiry.ApplicantName, "9876543210", "amit@example.com", inquiry.Source, inquiry.SourceDetail, inquiry.PreferredLanguageCode);
			await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().UpdateAsync(inquiry, true);
			await _applicantRepository.InsertAsync(new(Guid.NewGuid(), TestTenantId, target.FamilyId!.Value, "Other family member", null, null, null, null, null, Guid.NewGuid()), true);
		});
		current = await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(target.Id);
		(await Should.ThrowAsync<BusinessException>(() => ConfirmPendingFamilyAsync(target.Id, sourceFamilyId, sourceId, "father", current.ConcurrencyStamp))).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidState);
		foreach (var status in new[] { AdmissionInquiryStatus.Interested, AdmissionInquiryStatus.NotInterested, AdmissionInquiryStatus.Duplicate })
		{
			var closed = await CreateAdditionalPendingConfirmationTargetAsync(offering, "Closed " + status);
			await WithUnitOfWorkAsync(async () => { closed.SetStatus(status); await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().UpdateAsync(closed, true); });
			var dto = await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(closed.Id);
			(await Should.ThrowAsync<BusinessException>(() => ConfirmPendingFamilyAsync(closed.Id, sourceFamilyId, sourceId, "father", dto.ConcurrencyStamp))).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidState);
		}
		var started = await CreateAdditionalPendingConfirmationTargetAsync(offering, "Already started child");
		await MarkInheritedInquiryInterestedAsync(started.Id);
		var application = await _applicationAppService.StartAsync(started.ApplicantId!.Value, offering.Id, started.Id);
		var startedDto = await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(started.Id);
		await Should.ThrowAsync<BusinessException>(() => ConfirmPendingFamilyAsync(started.Id, sourceFamilyId, sourceId, "father", startedDto.ConcurrencyStamp));
		(await _applicationAppService.GetAsync(application.Id)).ApplicantId.ShouldBe(started.ApplicantId!.Value);
		var financeTarget = await CreateAdditionalPendingConfirmationTargetAsync(offering, "Financial evidence child");
		var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(offering.AdmissionCycleId));
		var account = new FinanceFeeAccount(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), financeTarget.ApplicantId!.Value, cycle.AcademicYearId,
			offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId, "Financial evidence child");
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().InsertAsync(account, true));
		(await Should.ThrowAsync<BusinessException>(() => ConfirmPendingFamilyAsync(financeTarget.Id, sourceFamilyId, sourceId, "father", financeTarget.ConcurrencyStamp))).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidState);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().GetAsync(account.Id))).ApplicantId.ShouldBe(financeTarget.ApplicantId!.Value);
		(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(target.ApplicantId!.Value))).FamilyId.ShouldBe(target.FamilyId!.Value);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionActivity, Guid>>().CountAsync(item => item.ActivityType == "SiblingFamilyConfirmed"))).ShouldBe(0);
	});

	[Fact]
	public Task Pending_family_confirmation_source_action_and_source_scope_are_independent_of_target_manage() => RunCertificationScenarioAsync(async () =>
	{
		var (offering, source, target) = await CreatePendingFamilyConfirmationFixtureAsync();
		var permissions = Substitute.For<IPermissionChecker>();
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage).Returns(true);
		await Should.ThrowAsync<AbpAuthorizationException>(() => ConfirmPendingFamilyWithPermissionsAsync(target, source, permissions));
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View).Returns(true);
		await Should.ThrowAsync<Exception>(() => ConfirmPendingFamilyWithPermissionsAsync(target, source, permissions)); // Application View cannot read a pending Inquiry-only source.
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View).Returns(true);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => ConfirmPendingFamilyWithPermissionsAsync(target, source, permissions));
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage).Returns(true);
		var (foreignOffering, foreignScopeSource, _) = await CreatePendingFamilyConfirmationFixtureAsync(separateStandard: true);
		foreignOffering.StandardId.ShouldNotBe(offering.StandardId);
		var actor = Guid.NewGuid(); await EnsureTestIdentityUserAsync(actor, "family-confirmation-scoped-" + actor.ToString("N"));
		var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(offering.AdmissionCycleId));
		await WithUnitOfWorkAsync(() => _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: actor,
			academicYearId: cycle.AcademicYearId, educationBoardId: offering.BoardId, educationMediumId: offering.MediumId,
			educationMediumStageId: offering.StageId, standardId: offering.StandardId), true));
		using (_principalAccessor.Change(ProfilePrincipal(actor)))
		{
			(await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionInquiryScopeAccessService>().GetAsync(target.Id))).Id.ShouldBe(target.Id);
			await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(() =>
				WithUnitOfWorkAsync(() => GetRequiredService<AdmissionInquiryScopeAccessService>().GetAsync(foreignScopeSource.Id)));
			await Should.ThrowAsync<Exception>(() => ConfirmPendingFamilyWithPermissionsAsync(target, foreignScopeSource, permissions));
		}
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<Exception>(() => ConfirmPendingFamilyWithPermissionsAsync(target, source, permissions));
		(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(target.ApplicantId!.Value))).FamilyId.ShouldBe(target.FamilyId!.Value);
	});

	[Fact]
	public Task Pending_family_confirmation_projects_selected_mother_into_generic_aliases_without_rewriting_capture() => RunCertificationScenarioAsync(async () =>
	{
		var (_, source, target) = await CreatePendingFamilyConfirmationFixtureAsync(genericContact: true);
		await WithUnitOfWorkAsync(async () =>
		{
			var saved = await _applicantRepository.GetAsync(source.ApplicantId!.Value);
			saved.SetProperty("AdmissionStudentField.Mother.Name", "Meera Shah");
			saved.SetProperty("AdmissionStudentField.Mother.Mobile", "9876543210");
			saved.SetProperty("AdmissionStudentField.Mother.Email", "mother@example.com");
			await _applicantRepository.UpdateAsync(saved, true);
		});
		var result = await ConfirmPendingFamilyAsync(target.Id, source.FamilyId!.Value, source.ApplicantId!.Value, "mother", target.ConcurrencyStamp);
		result.GuardianFirstName.ShouldBe("Meera"); result.GuardianLastName.ShouldBe("Shah"); result.Email.ShouldBe("mother@example.com");
		AdmissionApplicationValueConverter.Unwrap(result.FormValues.Single(value => value.FieldCode == "ADULT").ValueJson).ShouldBe("Meera");
		AdmissionApplicationValueConverter.Unwrap(result.FormValues.Single(value => value.FieldCode == "EMAIL").ValueJson).ShouldBe("mother@example.com");
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(target.Id))).FormResponseSnapshotJson.ShouldBe(target.FormResponseSnapshotJson);
		await Should.ThrowAsync<BusinessException>(() => ConfirmPendingFamilyAsync(target.Id, source.FamilyId!.Value, source.ApplicantId!.Value, "father", target.ConcurrencyStamp));
	});

	[Theory]
	[InlineData("father")]
	[InlineData("mother")]
	public Task Pending_family_confirmation_preserves_no_student_legacy_source_and_requires_explicit_role(string role) => RunCertificationScenarioAsync(async () =>
	{
		var (_, source, target) = await CreatePendingFamilyConfirmationFixtureAsync(genericContact: true);
		var original = JsonSerializer.Serialize(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(source.ApplicantId!.Value)));
		var result = await ConfirmPendingFamilyAsync(target.Id, source.FamilyId!.Value, source.ApplicantId!.Value, role, target.ConcurrencyStamp);
		ConfirmedFamilyFlag(result).ShouldBeTrue(); result.ParentRelationship.ShouldBe(role);
		JsonSerializer.Serialize(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(source.ApplicantId!.Value))).ShouldBe(original);
	});

	[Fact]
	public Task Pending_family_confirmation_current_student_role_wins_over_stale_father_and_source_parent_account_is_unchanged() => RunCertificationScenarioAsync(async () =>
	{
		var (offering, source, target) = await CreatePendingFamilyConfirmationFixtureAsync();
		var users = GetRequiredService<Volo.Abp.Identity.IdentityUserManager>();
		var parentUser = new Volo.Abp.Identity.IdentityUser(Guid.NewGuid(), "existing-parent-" + Guid.NewGuid().ToString("N"), "amit@example.com", TestTenantId);
		(await WithUnitOfWorkAsync(() => users.CreateAsync(parentUser, "ExistingParent!29"))).Succeeded.ShouldBeTrue();
		var student = await WithUnitOfWorkAsync(async () =>
		{
			var child = await CreateExistingStudentAsync(offering, "Existing source", "Child");
			child.UpdateFamilyInfo(null, new ParentGuardianDetails("Meera Shah", null, "Teacher", "mother@example.com", "9876543210", null, null, null, null, null, null, null, null), null, null);
			await _studentRepository.UpdateAsync(child, true);
			var applicant = await _applicantRepository.GetAsync(source.ApplicantId!.Value); applicant.LinkStudent(child.Id); await _applicantRepository.UpdateAsync(applicant, true);
			return child;
		});
		var link = new StudentPortalAccountLink(Guid.NewGuid(), TestTenantId, student.Id, parentUser.Id, "guardian", "father", TestUserId);
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(link, true));
		var hash = parentUser.PasswordHash;
		var staleFather = await Should.ThrowAsync<BusinessException>(() => ConfirmPendingFamilyAsync(target.Id, source.FamilyId!.Value, source.ApplicantId!.Value, "father", target.ConcurrencyStamp));
		staleFather.Code.ShouldBe(AdmissionErrorCodes.InquiryFamilyMismatch);
		var confirmedMother = await ConfirmPendingFamilyAsync(target.Id, source.FamilyId!.Value, source.ApplicantId!.Value, "mother", target.ConcurrencyStamp);
		confirmedMother.GuardianFirstName.ShouldBe("Meera");
		confirmedMother.MainSchoolContact!.Relationship.ShouldBe("mother");
		ConfirmedFamilyFlag(confirmedMother).ShouldBeTrue();
		(await WithUnitOfWorkAsync(() => users.GetByIdAsync(parentUser.Id))).PasswordHash.ShouldBe(hash);
		var unchangedLink = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().GetAsync(link.Id));
		unchangedLink.UserId.ShouldBe(parentUser.Id); unchangedLink.StudentId.ShouldBe(student.Id); unchangedLink.Relationship.ShouldBe("father");
		var currentSourceFamily = await WithUnitOfWorkAsync(() => _familyRepository.GetAsync(source.FamilyId!.Value));
		(await WithUnitOfWorkAsync(() => _guardianRepository.GetAsync(currentSourceFamily.PrimaryGuardianId!.Value))).FirstName.ShouldBe("Amit Shah");
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(target.Id))).FormResponseSnapshotJson.ShouldBe(target.FormResponseSnapshotJson);
	});

	private async Task<AdmissionInquiry> CreateAdditionalPendingConfirmationTargetAsync(AdmissionOffering offering, string name)
	{
		var service = GetRequiredService<IAdmissionPublicInquiryAppService>(); var link = await CreatePublicSiblingLinkAsync(service, offering);
		using (_currentTenant.Change(null))
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
			await service.SubmitPublicAsync(link.Token, PublicSiblingInput(offering, name));
		return await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(item => item.AdmissionOfferingId == offering.Id && item.ApplicantName == name));
	}

	private async Task ConfirmPendingFamilyWithPermissionsAsync(AdmissionInquiry target, AdmissionInquiry source, IPermissionChecker permissions)
	{
		var type = typeof(AdmissionInquiryAppService).Assembly.GetType("ShikshaMitra.Students.StudentEnrollments.Admissions.AdmissionInquiryFamilyConfirmationService").ShouldNotBeNull();
		var service = (ApplicationService)ActivatorUtilities.CreateInstance(GetRequiredService<IServiceProvider>(), type, permissions);
		service.LazyServiceProvider = new AbpLazyServiceProvider(GetRequiredService<IServiceProvider>());
		var method = type.GetMethod("ConfirmAsync")!;
		var input = JsonSerializer.Deserialize(JsonSerializer.Serialize(new { ExistingFamilyId = source.FamilyId, ExistingParentApplicantId = source.ApplicantId,
			ParentRelationship = "father", ConcurrencyStamp = target.ConcurrencyStamp }), method.GetParameters()[1].ParameterType)!;
		try { await (Task)method.Invoke(service, [target.Id, input])!; }
		catch (TargetInvocationException error) when (error.InnerException != null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
	}

	private async Task<(AdmissionOffering Offering, AdmissionInquiry Source, AdmissionInquiry Target)> CreatePendingFamilyConfirmationFixtureAsync(bool genericContact = false, bool separateStandard = false)
	{
		var offering = await CreateConfiguredInquiryOfferingAsync(genericContact: genericContact);
		if (separateStandard)
		{
			offering = await WithUnitOfWorkAsync(async () =>
			{
				var standard = await _standardManager.CreateAsync(offering.StageId, "Separate scoped standard", 20, null);
				await _standardRepository.InsertAsync(standard, true);
				var current = await _offeringRepository.GetAsync(offering.Id);
				current.UpdateIdentity(current.CampusId, current.BoardId, current.MediumId, current.StageId, standard.Id, false);
				return await _offeringRepository.UpdateAsync(current, true);
			});
		}
		await WithUnitOfWorkAsync(async () =>
		{
			foreach (var (formId, code) in new[] { (offering.InquiryFormDefinitionId!.Value, "PARENT_OCCUPATION"), (offering.ApplicationFormDefinitionId!.Value, "FATHER_OCCUPATION") })
			{
				var section = (await _formSectionRepository.GetListAsync(item => item.FormDefinitionId == formId)).First();
				await _formFieldRepository.InsertAsync(new(Guid.NewGuid(), formId, section.Id, code, "Father occupation", AdmissionFormFieldType.Text,
					AdmissionFormBindingType.System, "Father.Occupation", false, false, 20, null, null, null, null, null), true);
			}
		});
		var publicService = GetRequiredService<IAdmissionPublicInquiryAppService>();
		var link = await CreatePublicSiblingLinkAsync(publicService, offering);
		using (_currentTenant.Change(null))
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
		{
			var first = PublicSiblingInput(offering, "Older pending child");
			first.FormValues.Add(new() { FieldCode = "PARENT_OCCUPATION", ValueJson = "\"Engineer\"" });
			await publicService.SubmitPublicAsync(link.Token, first);
			await publicService.SubmitPublicAsync(link.Token, PublicSiblingInput(offering, "Younger pending child"));
		}
		var inquiries = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetListAsync(item => item.AdmissionOfferingId == offering.Id));
		return (offering, inquiries.Single(item => item.ApplicantName == "Older pending child"), inquiries.Single(item => item.ApplicantName == "Younger pending child"));
	}

	private async Task<AdmissionInquiryDto> ConfirmPendingFamilyAsync(Guid id, Guid familyId, Guid sourceId, string relationship, string stamp)
	{
		var method = typeof(IAdmissionInquiryAppService).GetMethod("ConfirmFamilyAsync");
		method.ShouldNotBeNull("Staff must be able to confirm two already-captured pending inquiries as siblings.");
		var json = JsonSerializer.Serialize(new { ExistingFamilyId = familyId, ExistingParentApplicantId = sourceId, ParentRelationship = relationship, ConcurrencyStamp = stamp });
		var input = JsonSerializer.Deserialize(json, method.GetParameters()[1].ParameterType)!;
		try { return await (Task<AdmissionInquiryDto>)method.Invoke(GetRequiredService<IAdmissionInquiryAppService>(), [id, input])!; }
		catch (TargetInvocationException error) when (error.InnerException != null)
		{
			ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw;
		}
	}

	private static bool ConfirmedFamilyFlag(AdmissionInquiryDto inquiry) =>
		(bool)(typeof(AdmissionInquiryDto).GetProperty("SiblingFamilyConfirmed")?.GetValue(inquiry) ?? false);
}

