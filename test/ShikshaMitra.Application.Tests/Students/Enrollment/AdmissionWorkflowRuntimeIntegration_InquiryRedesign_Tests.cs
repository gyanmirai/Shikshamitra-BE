using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Inquiry_redesign_capture_context_should_keep_closed_offering_visible_for_original_form_edit() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			await WithUnitOfWorkAsync(async () =>
			{
				offering.Deactivate();
				await _offeringRepository.UpdateAsync(offering, true);
			});
			var context = await WithUnitOfWorkAsync(() => GetRequiredService<IAdmissionInquiryCaptureAppService>().GetContextAsync(offering.Id));
			context.Offering.Id.ShouldBe(offering.Id);
			context.Availability.CanCreateInquiry.ShouldBeFalse();
			context.Form.ShouldBeNull();
		});

	[Fact]
	public Task Inquiry_redesign_full_seats_should_allow_capture_when_seat_workflow_allows_waitlist() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateAvailableOfferingAsync((await CreateManagedSeatWorkflowAsync()).Id);
			var pool = new AdmissionSeatPool(Guid.NewGuid(), TestTenantId, offering.Id, 1);
			pool.Reserve(false);
			await WithUnitOfWorkAsync(() => GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionSeatPool, Guid>>().InsertAsync(pool, true));
			var availability = GetRequiredService<AdmissionInquiryAvailabilityService>();
			var result = await WithUnitOfWorkAsync(() => availability.ReadAsync(offering));
			result.CanCreateInquiry.ShouldBeTrue();
			result.WaitlistAllowed.ShouldBeTrue();
			await WithUnitOfWorkAsync(() => availability.EnsureAvailableAsync(offering));
		});

	[Fact]
	public async Task Inquiry_redesign_should_require_open_offering_and_explicit_interest_before_idempotent_handoff()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: TestUserId), true);
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var missing = ManagedInquiryInput(offering);
			missing.AdmissionOfferingId = null;
			(await Should.ThrowAsync<BusinessException>(() => inquiries.CreateAsync(missing))).Code.ShouldBe(AdmissionErrorCodes.InquiryOfferingRequired);
			var inquiry = await inquiries.CreateAsync(ManagedInquiryInput(offering));
			(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id)))
				.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.QualificationRequired);
			inquiry = await inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
			var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id);
			(await _applicationAppService.StartAsync(inquiry.ApplicantId.Value, offering.Id, inquiry.Id)).Id.ShouldBe(application.Id);
			var transferred = await inquiries.GetAsync(inquiry.Id);
			transferred.IsTransferred.ShouldBeTrue();
			transferred.ApplicationId.ShouldBe(application.Id);
			transferred.Status.ShouldBe(AdmissionInquiryStatus.Interested);
			transferred.LegacyStatus.ShouldBe(AdmissionInquiryStatus.ApplicationStarted);
			(await inquiries.GetListAsync(new() { IsTransferred = false })).Items.ShouldNotContain(item => item.Id == inquiry.Id);
			(await inquiries.GetListAsync(new() { IsTransferred = true })).Items.ShouldContain(item => item.Id == inquiry.Id);
			(await Should.ThrowAsync<BusinessException>(() => inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(transferred, AdmissionInquiryStatus.New))))
				.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidState);
		}
	}

	[Fact]
	public async Task Inquiry_redesign_should_edit_original_form_and_sync_registration_identity_atomically()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: TestUserId), true);
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var inquiry = await inquiries.GetAsync((await inquiries.CreateAsync(ManagedInquiryInput(offering))).Id);
			inquiry.FormDefinition.ShouldNotBeNull();
			inquiry.FormResponseDefinitionId.ShouldBe(offering.InquiryFormDefinitionId);
			var update = InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested);
			update.FormValues = inquiry.FormValues;
			update.FormValues.Single(value => value.FieldCode == "APPLICANT_FIRST_NAME").ValueJson = "\"Corrected Aanya\"";
			var edited = await inquiries.UpdateAsync(inquiry.Id, update);
			edited.FormResponseDefinitionId.ShouldBe(inquiry.FormResponseDefinitionId);
			edited.ApplicantName.ShouldContain("Corrected Aanya");
			(await _applicantRepository.GetAsync(inquiry.ApplicantId!.Value)).FirstName.ShouldBe("Corrected Aanya");
			var application = await _applicationAppService.StartAsync(inquiry.ApplicantId.Value, offering.Id, inquiry.Id);
			(await _applicationAppService.GetFormAsync(application.Id)).Values.Single(value => value.FieldCode == "APPLICANT_FIRST_NAME")
				.ValueJson.ShouldBe("\"Corrected Aanya\"");
		}
	}

	[Fact]
	public async Task Inquiry_redesign_should_require_closure_reason_and_normalize_legacy_pending_filters()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: TestUserId), true);
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var inquiry = await inquiries.CreateAsync(ManagedInquiryInput(offering));
			(await Should.ThrowAsync<BusinessException>(() => inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.NotInterested))))
				.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
			var update = InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.NotInterested);
			update.StatusReason = "Family chose another school.";
			var closed = await inquiries.UpdateAsync(inquiry.Id, update);
			closed.Status.ShouldBe(AdmissionInquiryStatus.NotInterested);
			update = InquiryStatusUpdate(closed, AdmissionInquiryStatus.New);
			update.StatusReason = "Family asked to restart the enquiry.";
			(await inquiries.UpdateAsync(inquiry.Id, update)).Status.ShouldBe(AdmissionInquiryStatus.New);
			var repository = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionInquiry, Guid>>();
			var legacy = await repository.GetAsync(inquiry.Id);
			legacy.SetStatus(AdmissionInquiryStatus.Contacted);
			await repository.UpdateAsync(legacy, true);
			(await inquiries.GetListAsync(new() { Status = AdmissionInquiryStatus.New, IsTransferred = false }))
				.Items.ShouldContain(item => item.Id == inquiry.Id && item.Status == AdmissionInquiryStatus.New && item.LegacyStatus == AdmissionInquiryStatus.Contacted);
		}
	}

	private static UpdateAdmissionInquiryDto InquiryStatusUpdate(AdmissionInquiryDto inquiry, AdmissionInquiryStatus status) => new()
	{
		ApplicantName = inquiry.ApplicantName, Phone = inquiry.NormalizedPhone, Email = inquiry.Email,
		Source = inquiry.Source, SourceDetail = inquiry.SourceDetail, PreferredLanguageCode = inquiry.PreferredLanguageCode,
		ConcurrencyStamp = inquiry.ConcurrencyStamp, Status = status
	};
}
