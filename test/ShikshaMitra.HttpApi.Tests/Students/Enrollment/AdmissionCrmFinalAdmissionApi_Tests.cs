using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Encryption;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AdmissionCrmFinalAdmissionApi_Tests : ShikshaMitraApiTestBase
{
	public AdmissionCrmFinalAdmissionApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Supersede_An_Issued_Offer_Without_Editing_Its_Snapshot()
	{
		var pool = await CreateSeatPoolAsync(1);
		var lead = await CreateEligibleLeadAsync("Offer supersession");
		var original = await CreateOfferAsync(lead.Id, pool.Id);
		await SubmitOfferAsDifferentMakerAsync(original.Id);
		await PostOfferActionAsync($"{original.Id}/approve");
		original = await IssueOfferAsync(original.Id, false, null, HttpStatusCode.OK);

		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/final-admission/offers/{original.Id}/supersede",
			new SupersedeAdmissionCrmSeatOfferDto
			{
				Reason = "Parent requested a corrected response deadline.", SeatPoolId = pool.Id,
				OfferType = AdmissionCrmSeatOfferType.Final, Language = "en",
				Terms = new System.Collections.Generic.Dictionary<string, string?> { ["conditions"] = "Corrected offer terms." },
				ValidFrom = DateTime.UtcNow.AddMinutes(-1), ValidUntil = DateTime.UtcNow.AddDays(10)
			}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var replacement = (await response.Content.ReadFromJsonAsync<AdmissionCrmSeatOfferDto>(JsonOptions))!;
		replacement.Status.ShouldBe(AdmissionCrmSeatOfferStatus.Draft);
		replacement.PreviousOfferId.ShouldBe(original.Id);
		replacement.Version.ShouldBe(original.Version + 1);

		var originalResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/final-admission/offers/{original.Id}");
		var preserved = (await originalResponse.Content.ReadFromJsonAsync<AdmissionCrmSeatOfferDto>(JsonOptions))!;
		preserved.Status.ShouldBe(AdmissionCrmSeatOfferStatus.Superseded);
		preserved.Reservation!.Status.ShouldBe(AdmissionCrmSeatReservationStatus.Released);
		preserved.Terms["acceptance"].ShouldBe("Accept before the expiry time.");
		preserved.Terms.ContainsKey("conditions").ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Roll_Back_Failed_Offer_Acceptance_And_Run_Decision_History_And_Cancellation_Workflow()
	{
		var pool = await CreateSeatPoolAsync(1);
		var staleUpdateResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/final-admission/seat-pools/{pool.Id}",
			CreateSeatPoolInput(pool.Code, 1, "stale-concurrency-stamp"), JsonOptions);
		staleUpdateResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var lead = await CreateEligibleLeadAsync("Offer acceptance");
		var offer = await CreateOfferAsync(lead.Id, pool.Id);
		await SubmitOfferAsDifferentMakerAsync(offer.Id);
		offer = await PostOfferActionAsync($"{offer.Id}/approve");
		offer.Status.ShouldBe(AdmissionCrmSeatOfferStatus.Approved);
		offer = await IssueOfferAsync(offer.Id, false, null, HttpStatusCode.OK);
		offer.Reservation.ShouldNotBeNull();
		offer.Reservation.Status.ShouldBe(AdmissionCrmSeatReservationStatus.Active);

		var statusToken = await CreateApplicantStatusAccessAsync(lead.Id);
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var otpResponse = await Client.PostAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{statusToken}/otp", null);
		otpResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await otpResponse.Content.ReadAsStringAsync());
		var otp = await GetOtpCodeAsync(lead.Id);
		var verifyResponse = await Client.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{statusToken}/verify",
			new VerifyAdmissionApplicantStatusOtpDto { OtpCode = otp }, JsonOptions);
		verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await verifyResponse.Content.ReadAsStringAsync());
		var applicantStatus = await verifyResponse.Content.ReadFromJsonAsync<AdmissionApplicantStatusDto>(JsonOptions);
		applicantStatus.ShouldNotBeNull();
		applicantStatus.Offer.ShouldNotBeNull();
		applicantStatus.Offer.OfferId.ShouldBe(offer.Id);
		applicantStatus.Offer.Status.ShouldBe(AdmissionCrmSeatOfferStatus.Opened);

		var acceptInput = new RespondAdmissionApplicantOfferDto
		{
			AccessSessionToken = applicantStatus.AccessSessionToken!,
			Accept = true,
			Language = "en",
			Declarations = new System.Collections.Generic.Dictionary<string, bool> { ["offerTermsAccepted"] = true }
		};
		using (Factory.FailNextAdmissionCrmTimelineSave(AdmissionCrmTimelineEventType.OfferAccepted))
		{
			var failedAcceptance = await Client.PostAsJsonAsync(
				$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{statusToken}/offers/{offer.Id}/respond",
				acceptInput, JsonOptions);
			failedAcceptance.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await failedAcceptance.Content.ReadAsStringAsync());
		}
		var afterFailedAcceptanceResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/final-admission/offers/{offer.Id}");
		var afterFailedAcceptance = (await afterFailedAcceptanceResponse.Content
			.ReadFromJsonAsync<AdmissionCrmSeatOfferDto>(JsonOptions))!;
		afterFailedAcceptance.Status.ShouldBe(AdmissionCrmSeatOfferStatus.Opened);
		afterFailedAcceptance.Reservation!.Status.ShouldBe(AdmissionCrmSeatReservationStatus.Active);
		var poolAfterFailedAcceptance = await GetSeatPoolAsync(pool.Id);
		poolAfterFailedAcceptance.ReservedSeats.ShouldBe(1);
		poolAfterFailedAcceptance.AcceptedSeats.ShouldBe(0);

		var acceptResponse = await Client.PostAsJsonAsync(
			$"/api/public/student-enrollment/admissions-crm/{tenantId}/status/{statusToken}/offers/{offer.Id}/respond",
			acceptInput, JsonOptions);
		acceptResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await acceptResponse.Content.ReadAsStringAsync());
		var applicantOffer = await acceptResponse.Content.ReadFromJsonAsync<AdmissionApplicantStatusOfferDto>(JsonOptions);
		applicantOffer.ShouldNotBeNull();
		applicantOffer.Status.ShouldBe(AdmissionCrmSeatOfferStatus.Accepted);

		var approvalResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/final-admission/decision",
			new RecordAdmissionCrmAdmissionDecisionDto
			{
				Status = AdmissionCrmAdmissionDecisionStatus.Approved,
				SeatPoolId = pool.Id,
				OfferId = offer.Id,
				Reason = "Offer accepted and admission approved."
			}, JsonOptions);
		approvalResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await approvalResponse.Content.ReadAsStringAsync());

		var overwriteResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/final-admission/decision",
			new RecordAdmissionCrmAdmissionDecisionDto
			{
				Status = AdmissionCrmAdmissionDecisionStatus.Rejected,
				Reason = "Attempted silent replacement."
			}, JsonOptions);
		overwriteResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var cancellationResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/final-admission/decision",
			new RecordAdmissionCrmAdmissionDecisionDto
			{
				Status = AdmissionCrmAdmissionDecisionStatus.Cancelled,
				SeatPoolId = pool.Id,
				OfferId = offer.Id,
				Reason = "Parent withdrew before student activation.",
				OverrideUsed = true,
				OverrideReason = "Principal approved same-cycle cancellation."
			}, JsonOptions);
		cancellationResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await cancellationResponse.Content.ReadAsStringAsync());

		var historyResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/final-admission/decisions");
		historyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await historyResponse.Content.ReadAsStringAsync());
		var history = await historyResponse.Content.ReadFromJsonAsync<AdmissionCrmAdmissionDecisionDto[]>(JsonOptions);
		history.ShouldNotBeNull();
		history.Length.ShouldBe(2);
		history[0].Status.ShouldBe(AdmissionCrmAdmissionDecisionStatus.Cancelled);
		history[1].Status.ShouldBe(AdmissionCrmAdmissionDecisionStatus.Approved);

		var refreshedOfferResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/final-admission/offers/{offer.Id}");
		var refreshedOffer = await refreshedOfferResponse.Content.ReadFromJsonAsync<AdmissionCrmSeatOfferDto>(JsonOptions);
		refreshedOffer.ShouldNotBeNull();
		refreshedOffer.Status.ShouldBe(AdmissionCrmSeatOfferStatus.Cancelled);
		refreshedOffer.Reservation!.Status.ShouldBe(AdmissionCrmSeatReservationStatus.Cancelled);

		var refreshedPool = await GetSeatPoolAsync(pool.Id);
		refreshedPool.AcceptedSeats.ShouldBe(0);
		refreshedPool.CancelledSeats.ShouldBe(1);
		refreshedPool.RemainingCapacity.ShouldBe(1);

		var analyticsResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/analytics/summary");
		analyticsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await analyticsResponse.Content.ReadAsStringAsync());
		var analytics = await analyticsResponse.Content.ReadFromJsonAsync<AdmissionCrmAnalyticsSummaryDto>(JsonOptions);
		analytics.ShouldNotBeNull();
		analytics.FinalAdmissionAnalytics.SeatPoolCount.ShouldBeGreaterThanOrEqualTo(1);
		analytics.FinalAdmissionAnalytics.CancelledOfferCount.ShouldBeGreaterThanOrEqualTo(1);
		analytics.FinalAdmissionAnalytics.CancelledDecisionCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Enforce_Waitlist_Priority_And_Reasoned_Capacity_Override()
	{
		var pool = await CreateSeatPoolAsync(1);
		var lowerPriorityLead = await CreateEligibleLeadAsync("Lower priority waitlist");
		var higherPriorityLead = await CreateEligibleLeadAsync("Higher priority waitlist");
		var lower = await AddToWaitlistAsync(lowerPriorityLead.Id, pool.Id, 10);
		var higher = await AddToWaitlistAsync(higherPriorityLead.Id, pool.Id, 100);

		var lowerPromotionResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/final-admission/waitlist/{lower.Id}/prepare-promotion",
			CreatePromotionInput(pool.Id), JsonOptions);
		lowerPromotionResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await lowerPromotionResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:HigherPriorityWaitlistEntryExists");

		var promotionResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/final-admission/waitlist/{higher.Id}/prepare-promotion",
			CreatePromotionInput(pool.Id), JsonOptions);
		promotionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await promotionResponse.Content.ReadAsStringAsync());
		var firstOffer = await promotionResponse.Content.ReadFromJsonAsync<AdmissionCrmSeatOfferDto>(JsonOptions);
		firstOffer.ShouldNotBeNull();
		await SubmitOfferAsDifferentMakerAsync(firstOffer.Id);
		await PostOfferActionAsync($"{firstOffer.Id}/approve");
		await IssueOfferAsync(firstOffer.Id, false, null, HttpStatusCode.OK);

		var secondLead = await CreateEligibleLeadAsync("Capacity override");
		var secondOffer = await CreateOfferAsync(secondLead.Id, pool.Id);
		await SubmitOfferAsDifferentMakerAsync(secondOffer.Id);
		await PostOfferActionAsync($"{secondOffer.Id}/approve");
		await IssueOfferAsync(secondOffer.Id, false, null, HttpStatusCode.BadRequest);
		await IssueOfferAsync(secondOffer.Id, true, null, HttpStatusCode.BadRequest);
		var overriddenOffer = await IssueOfferAsync(
			secondOffer.Id, true, "Principal approved one additional seat.", HttpStatusCode.OK);
		overriddenOffer.Reservation!.CapacityOverrideUsed.ShouldBeTrue();

		var refreshedPool = await GetSeatPoolAsync(pool.Id);
		refreshedPool.ReservedSeats.ShouldBe(2);
		refreshedPool.IsOverCapacity.ShouldBeTrue();
		refreshedPool.WaitlistedApplicants.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Preserve_Waitlist_Governance_History_And_Skip_Paused_Promotions()
	{
		var pool = await CreateSeatPoolAsync(2);
		var pausedLead = await CreateEligibleLeadAsync("Paused waitlist");
		var activeLead = await CreateEligibleLeadAsync("Active waitlist");
		var paused = await AddToWaitlistAsync(pausedLead.Id, pool.Id, 100);
		var active = await AddToWaitlistAsync(activeLead.Id, pool.Id, 80);

		var governanceResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/final-admission/waitlist/{paused.Id}/governance",
			new UpdateAdmissionCrmWaitlistGovernanceDto
			{
				PriorityScore = 100, IsProtected = true, PromotionPaused = true,
				Reason = "Parent requested a documented hold until transfer evidence arrives."
			}, JsonOptions);
		governanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await governanceResponse.Content.ReadAsStringAsync());

		var runResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/final-admission/waitlist/run-promotion",
			new RunAdmissionCrmWaitlistPromotionDto
			{
				SeatPoolId = pool.Id, MaximumOffers = 2, Offer = CreatePromotionInput(pool.Id)
			}, JsonOptions);
		runResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await runResponse.Content.ReadAsStringAsync());
		var run = (await runResponse.Content.ReadFromJsonAsync<AdmissionCrmWaitlistPromotionRunResultDto>(JsonOptions))!;
		run.PreparedCount.ShouldBe(1);

		var pausedHistoryResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/final-admission/waitlist/{paused.Id}/history");
		pausedHistoryResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await pausedHistoryResponse.Content.ReadAsStringAsync());
		var pausedHistory = (await pausedHistoryResponse.Content.ReadFromJsonAsync<AdmissionCrmWaitlistHistoryDto[]>(JsonOptions))!;
		pausedHistory.Select(x => x.Action).ShouldContain(AdmissionCrmWaitlistHistoryAction.Protected);
		pausedHistory.Select(x => x.Action).ShouldContain(AdmissionCrmWaitlistHistoryAction.PromotionPaused);
		pausedHistory.Select(x => x.Action).ShouldNotContain(AdmissionCrmWaitlistHistoryAction.OfferPrepared);

		var activeHistoryResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/final-admission/waitlist/{active.Id}/history");
		var activeHistory = (await activeHistoryResponse.Content.ReadFromJsonAsync<AdmissionCrmWaitlistHistoryDto[]>(JsonOptions))!;
		activeHistory.Select(x => x.Action).ShouldContain(AdmissionCrmWaitlistHistoryAction.OfferPrepared);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Block_Offer_Acceptance_Until_Finance_Confirms_The_Configured_Gate()
	{
		var lead = await CreateEligibleLeadAsync("Finance-gated offer acceptance");
		var policyResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/financial/fee-policies",
			new CreateAdmissionCrmFeePolicyDto
			{
				Code = "FIN-" + Guid.NewGuid().ToString("N")[..8],
				Name = "Admission deposit",
				Applicability = new AdmissionCrmFeePolicyApplicabilityDto { DesiredClass = "Grade 1" },
				Components =
				[
					new AdmissionCrmFeeComponentDto
					{
						Key = "deposit",
						Name = "Admission deposit",
						Amount = 1000,
						IsRefundable = true,
						ParentDescription = "Seat-confirmation deposit."
					}
				],
				GateType = AdmissionCrmPaymentGateType.FinanceCleared,
				GateMilestone = AdmissionCrmPaymentGateMilestone.OfferAcceptance,
				RequiredAmount = 1000,
				EffectiveFrom = DateTime.UtcNow.AddDays(-1),
				EffectiveUntil = DateTime.UtcNow.AddDays(30)
			}, JsonOptions);
		policyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await policyResponse.Content.ReadAsStringAsync());
		var policy = await policyResponse.Content.ReadFromJsonAsync<AdmissionCrmFeePolicyDto>(JsonOptions);
		policy.ShouldNotBeNull();
		await PublishFeePolicyAsDifferentMakerAsync(policy.Id);

		var quotationResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/financial/quotations",
			new IssueAdmissionCrmQuotationDto
			{
				FeePolicyId = policy.Id,
				ValidUntil = DateTime.UtcNow.AddDays(2)
			}, JsonOptions);
		quotationResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await quotationResponse.Content.ReadAsStringAsync());
		var quotation = await quotationResponse.Content.ReadFromJsonAsync<AdmissionCrmAdmissionQuotationDto>(JsonOptions);
		quotation.ShouldNotBeNull();
		quotation.IsGateSatisfied.ShouldBeFalse();

		var pool = await CreateSeatPoolAsync(1);
		var offer = await CreateOfferAsync(lead.Id, pool.Id);
		await SubmitOfferAsDifferentMakerAsync(offer.Id);
		await PostOfferActionAsync($"{offer.Id}/approve");
		await IssueOfferAsync(offer.Id, false, null, HttpStatusCode.OK);

		var blockedAcceptance = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/final-admission/offers/{offer.Id}/accept",
			new RespondAdmissionCrmSeatOfferDto { ResponseSource = "Staff confirmation" }, JsonOptions);
		blockedAcceptance.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await blockedAcceptance.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:PaymentGateNotSatisfied");

		var blockedReadinessResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/financial/readiness");
		blockedReadinessResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await blockedReadinessResponse.Content.ReadAsStringAsync());
		var blockedReadiness = await blockedReadinessResponse.Content
			.ReadFromJsonAsync<AdmissionCrmFinancialReadinessDto>(JsonOptions);
		blockedReadiness.ShouldNotBeNull();
		blockedReadiness.CanAcceptOffer.ShouldBeFalse();
		blockedReadiness.CanApproveAdmission.ShouldBeFalse();
		blockedReadiness.CanActivateStudent.ShouldBeFalse();

		var financeResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/financial/quotations/{quotation.Id}/finance-state",
			new RecordAdmissionCrmFinanceStateDto
			{
				State = AdmissionCrmFinanceState.Confirmed,
				ConfirmedAmount = 1000,
				FinanceReference = "FIN-RECEIPT-100",
				Source = "Finance reconciliation",
				ConfirmedAt = DateTime.UtcNow
			}, JsonOptions);
		financeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await financeResponse.Content.ReadAsStringAsync());
		var confirmed = await financeResponse.Content.ReadFromJsonAsync<AdmissionCrmAdmissionQuotationDto>(JsonOptions);
		confirmed.ShouldNotBeNull();
		confirmed.IsGateSatisfied.ShouldBeTrue();

		var acceptedResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/final-admission/offers/{offer.Id}/accept",
			new RespondAdmissionCrmSeatOfferDto { ResponseSource = "Staff confirmation" }, JsonOptions);
		acceptedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await acceptedResponse.Content.ReadAsStringAsync());

		var readinessResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/financial/readiness");
		readinessResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await readinessResponse.Content.ReadAsStringAsync());
		var readiness = await readinessResponse.Content.ReadFromJsonAsync<AdmissionCrmFinancialReadinessDto>(JsonOptions);
		readiness.ShouldNotBeNull();
		readiness.CanAcceptOffer.ShouldBeTrue();
		readiness.CurrentQuotation!.FinanceReference.ShouldBe("FIN-RECEIPT-100");
	}

	private async Task<AdmissionCrmSeatPoolDto> CreateSeatPoolAsync(int capacity)
	{
		var code = "POOL-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/final-admission/seat-pools",
			CreateSeatPoolInput(code, capacity, null), JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmSeatPoolDto>(JsonOptions))!;
	}

	private static UpsertAdmissionCrmSeatPoolDto CreateSeatPoolInput(
		string code, int capacity, string? concurrencyStamp) => new()
	{
		Code = code,
		Name = "Grade 1 general seats",
		DesiredClass = "Grade 1",
		Board = "CBSE",
		Medium = "English",
		ApplicantCategory = "General",
		OperationalTarget = capacity,
		ApprovedCapacity = capacity,
		ConcurrencyStamp = concurrencyStamp
	};

	private async Task<AdmissionCrmLeadDto> CreateEligibleLeadAsync(string intent)
	{
		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = intent,
				DesiredClassName = "Grade 1",
				PrimaryContactNo = "9" + Random.Shared.NextInt64(100000000, 999999999),
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Applicant"
			}, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var lead = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions);
		lead.ShouldNotBeNull();

		await SetLeadStageAsync(lead.Id, AdmissionInquiryStage.Eligible);
		return lead;
	}

	private async Task SetLeadStageAsync(Guid leadId, AdmissionInquiryStage stage)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionInquiryLead, Guid>>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var lead = await repository.GetAsync(leadId);
			lead.ChangeStage(stage);
			await repository.UpdateAsync(lead);
			await uow.CompleteAsync();
		}
	}

	private async Task<AdmissionCrmSeatOfferDto> CreateOfferAsync(Guid leadId, Guid seatPoolId)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/final-admission/offers",
			new CreateAdmissionCrmSeatOfferDto
			{
				SeatPoolId = seatPoolId,
				OfferType = AdmissionCrmSeatOfferType.Final,
				Terms = new System.Collections.Generic.Dictionary<string, string?>
				{
					["acceptance"] = "Accept before the expiry time."
				},
				Language = "en",
				ValidFrom = DateTime.UtcNow.AddMinutes(-1),
				ValidUntil = DateTime.UtcNow.AddDays(2)
			}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmSeatOfferDto>(JsonOptions))!;
	}

	private static PrepareAdmissionCrmWaitlistPromotionDto CreatePromotionInput(Guid seatPoolId) => new()
	{
		SeatPoolId = seatPoolId,
		OfferType = AdmissionCrmSeatOfferType.Final,
		Terms = new System.Collections.Generic.Dictionary<string, string?> { ["source"] = "waitlist" },
		Language = "en",
		ValidFrom = DateTime.UtcNow.AddMinutes(-1),
		ValidUntil = DateTime.UtcNow.AddDays(2)
	};

	private async Task SubmitOfferAsDifferentMakerAsync(Guid offerId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionCrmSeatOffer, Guid>>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var offer = await repository.GetAsync(offerId);
			offer.SubmitForApproval(Guid.NewGuid(), DateTime.UtcNow);
			await repository.UpdateAsync(offer);
			await uow.CompleteAsync();
		}
	}

	private async Task PublishFeePolicyAsDifferentMakerAsync(Guid policyId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionCrmFeePolicy, Guid>>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var policy = await repository.GetAsync(policyId);
			policy.Submit(Guid.NewGuid(), DateTime.UtcNow);
			policy.Approve(Guid.NewGuid(), DateTime.UtcNow);
			policy.Publish(DateTime.UtcNow);
			await repository.UpdateAsync(policy);
			await uow.CompleteAsync();
		}
	}

	private async Task<string> CreateApplicantStatusAccessAsync(Guid leadId)
	{
		var token = "status-" + Guid.NewGuid().ToString("N");
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionApplicantStatusAccess, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await repository.InsertAsync(new AdmissionApplicantStatusAccess(
				Guid.NewGuid(), tenantId, leadId, HashSecret(token), null,
				DateTime.Now.AddDays(90), DateTime.Now));
			await uow.CompleteAsync();
		}
		return token;
	}

	private async Task<string> GetOtpCodeAsync(Guid leadId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionApplicantStatusAccess, Guid>>();
		var encryption = scope.ServiceProvider.GetRequiredService<IStringEncryptionService>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var queryable = await repository.GetQueryableAsync();
			var access = queryable.Single(x => x.LeadId == leadId);
			var otp = encryption.Decrypt(access.EncryptedOtpCode!);
			await uow.CompleteAsync();
			return otp!;
		}
	}

	private static string HashSecret(string value) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()))).ToLowerInvariant();

	private async Task<AdmissionCrmSeatOfferDto> PostOfferActionAsync(string action)
	{
		var response = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/final-admission/offers/{action}", null);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmSeatOfferDto>(JsonOptions))!;
	}

	private async Task<AdmissionCrmSeatOfferDto> IssueOfferAsync(
		Guid offerId, bool capacityOverride, string? reason, HttpStatusCode expectedStatus)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/final-admission/offers/{offerId}/issue",
			new IssueAdmissionCrmSeatOfferDto
			{
				CapacityOverride = capacityOverride,
				CapacityOverrideReason = reason
			}, JsonOptions);
		response.StatusCode.ShouldBe(expectedStatus, await response.Content.ReadAsStringAsync());
		return expectedStatus == HttpStatusCode.OK
			? (await response.Content.ReadFromJsonAsync<AdmissionCrmSeatOfferDto>(JsonOptions))!
			: new AdmissionCrmSeatOfferDto();
	}

	private async Task<AdmissionCrmWaitlistEntryDto> AddToWaitlistAsync(
		Guid leadId, Guid seatPoolId, int priorityScore)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{leadId}/final-admission/waitlist",
			new CreateAdmissionCrmWaitlistEntryDto
			{
				SeatPoolId = seatPoolId,
				PriorityScore = priorityScore
			}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmWaitlistEntryDto>(JsonOptions))!;
	}

	private async Task<AdmissionCrmSeatPoolDto> GetSeatPoolAsync(Guid poolId)
	{
		var response = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/final-admission/seat-pools?maxResultCount=100");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var page = await response.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmSeatPoolDto>>(JsonOptions);
		page.ShouldNotBeNull();
		return page.Items.Single(x => x.Id == poolId);
	}
}
