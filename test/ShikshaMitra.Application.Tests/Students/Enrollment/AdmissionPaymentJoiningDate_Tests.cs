using System;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Collections;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Payment_joining_date_preview_and_capture_should_not_prepare_finance() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("JoiningDate");
		var service = GetRequiredService<IAdmissionPaymentJoiningDateAppService>();
		var before = await service.GetAsync(fixture.Application.Id);
		before.JoiningDate.ShouldBeNull();
		before.CanEdit.ShouldBeTrue();
		var date = before.AcademicYearStartDate.AddDays(1);
		var preview = await service.PreviewAsync(fixture.Application.Id, new() { JoiningDate = date.ToString("yyyy-MM-dd") });
		preview.TotalAmount.ShouldBe(1000m);
		(await AccountCountAsync(fixture.Application.Id)).ShouldBe(0);
		(await service.GetAsync(fixture.Application.Id)).JoiningDate.ShouldBeNull();
		var saved = await service.SaveAsync(fixture.Application.Id, new() { JoiningDate = date.ToString("yyyy-MM-dd"), ConcurrencyStamp = before.ConcurrencyStamp });
		saved.JoiningDate.ShouldBe(date);
		(await AccountCountAsync(fixture.Application.Id)).ShouldBe(0);
		(await GetRequiredService<IAdmissionFinanceContextReader>().GetAsync(fixture.Application.Id)).JoiningDate.ShouldBe(date);
		await WithUnitOfWorkAsync(async () => AdmissionAdditionalStudentFieldCatalog.GetValue("Academic.JoiningDate",
			await GetRequiredService<IRepository<AdmissionApplicant, Guid>>().GetAsync(fixture.Application.ApplicantId)).ShouldBe(date.ToString("yyyy-MM-dd")));
		(await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(fixture.Application.Id,
			new() { JoiningDate = date.AddDays(1).ToString("yyyy-MM-dd"), ConcurrencyStamp = before.ConcurrencyStamp }))).Code
			.ShouldBe(AdmissionFeeSelectionErrorCodes.ConcurrencyConflict);
	});

	[Fact]
	public Task Prepared_fee_schedule_should_lock_the_shared_joining_date() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("JoiningDateLock");
		var service = GetRequiredService<IAdmissionPaymentJoiningDateAppService>();
		var before = await service.GetAsync(fixture.Application.Id);
		var date = before.AcademicYearStartDate.AddDays(1);
		await service.SaveAsync(fixture.Application.Id, new() { JoiningDate = date.ToString("yyyy-MM-dd"), ConcurrencyStamp = before.ConcurrencyStamp });
		await GetRequiredService<IAdmissionFeeCollectionAppService>().PrepareAsync(fixture.Application.Id, new());
		var locked = await service.GetAsync(fixture.Application.Id);
		locked.IsLocked.ShouldBeTrue();
		locked.CanEdit.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(fixture.Application.Id,
			new() { JoiningDate = date.AddDays(1).ToString("yyyy-MM-dd"), ConcurrencyStamp = locked.ConcurrencyStamp }))).Code
			.ShouldBe(AdmissionFeeSelectionErrorCodes.Locked);
		(await GetRequiredService<IAdmissionFinanceContextReader>().GetAsync(fixture.Application.Id)).JoiningDate.ShouldBe(date);
	});

	[Fact]
	public Task Payment_date_should_unblock_proration_without_a_registration_field_and_preview_should_stay_readonly() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("PaymentDateProration", amount: 12000m, oneTime: false);
		await ConfigureProrationAsync(fixture);
		var service = GetRequiredService<IAdmissionPaymentJoiningDateAppService>();
		var collection = GetRequiredService<IAdmissionFeeCollectionAppService>();
		(await collection.GetSummaryAsync(fixture.Application.Id)).CanCollect.ShouldBeFalse();
		var state = await service.GetAsync(fixture.Application.Id);
		state.CanPreview.ShouldBeTrue();
		var candidate = state.AcademicYearStartDate.AddMonths(2).ToString("yyyy-MM-dd");
		var preview = await service.PreviewAsync(fixture.Application.Id, new() { JoiningDate = candidate });
		preview.ReductionAmount.ShouldBeGreaterThan(0m);
		preview.Proration.ShouldNotBeNull();
		(await AccountCountAsync(fixture.Application.Id)).ShouldBe(0);
		(await service.GetAsync(fixture.Application.Id)).JoiningDate.ShouldBeNull();
		await service.SaveAsync(fixture.Application.Id, new() { JoiningDate = candidate, ConcurrencyStamp = state.ConcurrencyStamp });
		var prepared = await collection.PrepareAsync(fixture.Application.Id, new());
		prepared.TotalAmount.ShouldBe(preview.TotalAmount);
		prepared.Proration!.JoiningDate.ShouldBe(preview.JoiningDate);
		prepared.Proration.PolicyVersion.ShouldBe(preview.Proration.PolicyVersion);
	});

	[Fact]
	public Task Payment_date_should_synchronize_submitted_registration_and_reject_later_form_date_changes() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("PaymentDateSync");
		var service = GetRequiredService<IAdmissionPaymentJoiningDateAppService>();
		var initial = await service.GetAsync(fixture.Application.Id);
		await SaveJoiningAnswerAsync(fixture, initial.AcademicYearStartDate.ToString("yyyy-MM-dd"));
		var before = await service.GetAsync(fixture.Application.Id);
		var date = before.AcademicYearStartDate.AddDays(2);
		var submittedBefore = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionFormSubmission, Guid>>()
			.GetListAsync(s => s.ApplicationId == fixture.Application.Id));
		submittedBefore.ShouldNotBeEmpty();
		await service.SaveAsync(fixture.Application.Id, new() { JoiningDate = date.ToString("yyyy-MM-dd"), ConcurrencyStamp = before.ConcurrencyStamp });
		var form = await _applicationAppService.GetFormAsync(fixture.Application.Id);
		form.Status.ShouldBe(AdmissionFormInstanceStatus.Submitted);
		var submittedAfter = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionFormSubmission, Guid>>()
			.GetListAsync(s => s.ApplicationId == fixture.Application.Id));
		submittedAfter.Select(s => (s.Id, s.SnapshotJson)).ShouldBe(submittedBefore.Select(s => (s.Id, s.SnapshotJson)));
		AdmissionApplicationValueConverter.Unwrap(form.Values.Single(v => v.FieldCode == "ACTUAL_JOINING_DATE").ValueJson).ShouldBe(date.ToString("yyyy-MM-dd"));
		await GetRequiredService<IAdmissionFeeCollectionAppService>().PrepareAsync(fixture.Application.Id, new());
		await WithUnitOfWorkAsync(async () =>
		{
			var application = await _applicationRepository.GetAsync(fixture.Application.Id);
			var definitions = await _formFieldRepository.GetListAsync(f => f.FormDefinitionId == form.FormDefinitionId);
			(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<AdmissionJoiningDateCapture>().GuardFormChangeAsync(application,
				definitions, [new() { FieldCode = "ACTUAL_JOINING_DATE", ValueJson = JsonSerializer.Serialize(date.AddDays(1).ToString("yyyy-MM-dd")) }]))).Code
				.ShouldBe(AdmissionFeeSelectionErrorCodes.Locked);
		});
	});

	[Theory]
	[InlineData("")]
	[InlineData("2026-02-30")]
	[InlineData("2026-06-02T00:00:00Z")]
	[InlineData("9999-12-31")]
	public Task Payment_date_rejects_invalid_dates_without_writing(string candidate) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("InvalidPaymentDate");
		var service = GetRequiredService<IAdmissionPaymentJoiningDateAppService>();
		var rejected = await Should.ThrowAsync<Exception>(() => service.PreviewAsync(fixture.Application.Id, new() { JoiningDate = candidate }));
		(rejected is BusinessException or Volo.Abp.Validation.AbpValidationException).ShouldBeTrue();
		(await service.GetAsync(fixture.Application.Id)).JoiningDate.ShouldBeNull();
		(await AccountCountAsync(fixture.Application.Id)).ShouldBe(0);
	});

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
	[InlineData(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit)]
	public Task Payment_date_rechecks_exact_collection_and_owner_permissions(string deniedPermission) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("DeniedPaymentDate");
		var existing = await GetRequiredService<IAdmissionPaymentJoiningDateAppService>().GetAsync(fixture.Application.Id);
		var provider = GetRequiredService<IServiceProvider>();
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>()).Returns(call =>
			Task.FromResult(call.ArgAt<string>(2) == deniedPermission ? AuthorizationResult.Failed() : AuthorizationResult.Success()));
		var denied = ActivatorUtilities.CreateInstance<AdmissionPaymentJoiningDateAppService>(provider);
		denied.LazyServiceProvider = new AbpLazyServiceProvider(new CredentialsAuthorizationProvider(provider, authorization));
		await WithUnitOfWorkAsync(() => Should.ThrowAsync<AbpAuthorizationException>(() => denied.SaveAsync(fixture.Application.Id,
			new() { JoiningDate = existing.AcademicYearStartDate.ToString("yyyy-MM-dd"), ConcurrencyStamp = existing.ConcurrencyStamp })));
		if (deniedPermission == ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)
			await WithUnitOfWorkAsync(() => Should.ThrowAsync<AbpAuthorizationException>(() => denied.GetAsync(fixture.Application.Id)));
		else
			(await WithUnitOfWorkAsync(() => denied.GetAsync(fixture.Application.Id))).CanEdit.ShouldBeFalse();
		(await GetRequiredService<IAdmissionPaymentJoiningDateAppService>().GetAsync(fixture.Application.Id)).JoiningDate.ShouldBeNull();
		(await AccountCountAsync(fixture.Application.Id)).ShouldBe(0);
	});

	[Fact]
	public Task Payment_date_rejects_an_application_from_another_tenant() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("TenantPaymentDate");
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var service = GetRequiredService<IAdmissionPaymentJoiningDateAppService>();
			var read = await Should.ThrowAsync<Exception>(() => service.GetAsync(fixture.Application.Id));
			var preview = await Should.ThrowAsync<Exception>(() => service.PreviewAsync(fixture.Application.Id, new() { JoiningDate = "2026-06-02" }));
			var write = await Should.ThrowAsync<Exception>(() => service.SaveAsync(fixture.Application.Id, new() { JoiningDate = "2026-06-02", ConcurrencyStamp = fixture.Application.ConcurrencyStamp! }));
			foreach (var rejection in new[] { read, preview, write })
				(rejection is AbpAuthorizationException or Volo.Abp.Domain.Entities.EntityNotFoundException).ShouldBeTrue();
		}
		(await AccountCountAsync(fixture.Application.Id)).ShouldBe(0);
	});

	[Fact]
	public Task Payment_date_can_be_confirmed_during_deferred_onboarding_without_creating_dues() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("OnboardingPaymentDate");
		await CompleteCollectibleDecisionAsync(fixture.Application.Id);
		var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "joining-date-onboarding");
		var service = GetRequiredService<IAdmissionPaymentJoiningDateAppService>();
		var before = await service.GetAsync(fixture.Application.Id);
		before.CanEdit.ShouldBeTrue();
		var date = before.AcademicYearStartDate.AddDays(1);
		await service.SaveAsync(fixture.Application.Id, new() { JoiningDate = date.ToString("yyyy-MM-dd"), ConcurrencyStamp = before.ConcurrencyStamp });
		(await GetRequiredService<IAdmissionFinanceContextReader>().GetAsync(fixture.Application.Id)).JoiningDate.ShouldBe(date);
		(await ReadAsync<Student>(finalized.StudentId)).ExtraProperties["AdmissionStudentField.Academic.JoiningDate"].ShouldNotBeNull();
		(await AccountCountAsync(fixture.Application.Id)).ShouldBe(0);
	});
}
