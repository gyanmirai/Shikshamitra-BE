using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmFinalAdmissionAppService : ApplicationService, IAdmissionCrmFinalAdmissionAppService
{
	private static readonly AdmissionCrmSeatOfferStatus[] OpenOfferStatuses =
	[
		AdmissionCrmSeatOfferStatus.Draft,
		AdmissionCrmSeatOfferStatus.ApprovalPending,
		AdmissionCrmSeatOfferStatus.Approved,
		AdmissionCrmSeatOfferStatus.Issued,
		AdmissionCrmSeatOfferStatus.Opened,
		AdmissionCrmSeatOfferStatus.Accepted
	];

	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmPlaybook, Guid> _playbookRepository;
	private readonly IRepository<AdmissionCrmRegistrationApplication, Guid> _registrationRepository;
	private readonly IRepository<AdmissionCrmSeatPool, Guid> _seatPoolRepository;
	private readonly IRepository<AdmissionCrmSeatReservation, Guid> _reservationRepository;
	private readonly IRepository<AdmissionCrmSeatOffer, Guid> _offerRepository;
	private readonly IRepository<AdmissionCrmWaitlistEntry, Guid> _waitlistRepository;
	private readonly IRepository<AdmissionCrmWaitlistHistory, Guid> _waitlistHistoryRepository;
	private readonly IRepository<AdmissionCrmAdmissionDecision, Guid> _decisionRepository;
	private readonly IRepository<AdmissionCrmAdmissionQuotation, Guid> _quotationRepository;
	private readonly IRepository<AdmissionCrmTimelineEvent, Guid> _timelineRepository;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public AdmissionCrmFinalAdmissionAppService(
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmPlaybook, Guid> playbookRepository,
		IRepository<AdmissionCrmRegistrationApplication, Guid> registrationRepository,
		IRepository<AdmissionCrmSeatPool, Guid> seatPoolRepository,
		IRepository<AdmissionCrmSeatReservation, Guid> reservationRepository,
		IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
		IRepository<AdmissionCrmWaitlistEntry, Guid> waitlistRepository,
		IRepository<AdmissionCrmWaitlistHistory, Guid> waitlistHistoryRepository,
		IRepository<AdmissionCrmAdmissionDecision, Guid> decisionRepository,
		IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
		IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
		StudentScopeAccessService studentScopeAccessService)
	{
		_leadRepository = leadRepository;
		_playbookRepository = playbookRepository;
		_registrationRepository = registrationRepository;
		_seatPoolRepository = seatPoolRepository;
		_reservationRepository = reservationRepository;
		_offerRepository = offerRepository;
		_waitlistRepository = waitlistRepository;
		_waitlistHistoryRepository = waitlistHistoryRepository;
		_decisionRepository = decisionRepository;
		_quotationRepository = quotationRepository;
		_timelineRepository = timelineRepository;
		_studentScopeAccessService = studentScopeAccessService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageSeatPlans)]
	public async Task<AdmissionCrmSeatPoolDto> CreateSeatPoolAsync(Guid academicYearId, UpsertAdmissionCrmSeatPoolDto input)
	{
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(CreateSeatPoolAsync));
		var poolQueryable = await _seatPoolRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(poolQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId && x.Code == input.Code.Trim())))
		{
			throw new BusinessException("AdmissionCrm:SeatPoolCodeAlreadyExists");
		}

		var definition = await ResolveSeatPoolDefinitionAsync(academicYearId, input);
		var pool = new AdmissionCrmSeatPool(
			GuidGenerator.Create(), CurrentTenant.Id, academicYearId, input.PlaybookId, input.Code.Trim(), input.Name,
			definition.RoundCode, definition.Campus, definition.DesiredClass, definition.Board, definition.Medium,
			definition.ApplicantCategory, definition.OperationalTarget, definition.ApprovedCapacity);
		if (!input.IsActive)
		{
			pool.UpdateDefinition(input.PlaybookId, input.Name, definition.RoundCode, definition.Campus,
				definition.DesiredClass, definition.Board, definition.Medium, definition.ApplicantCategory,
				definition.OperationalTarget, definition.ApprovedCapacity, false);
		}

		await _seatPoolRepository.InsertAsync(pool, autoSave: true);
		return MapSeatPool(pool);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageSeatPlans)]
	public async Task<AdmissionCrmSeatPoolDto> UpdateSeatPoolAsync(Guid id, UpsertAdmissionCrmSeatPoolDto input)
	{
		var pool = await GetSeatPoolEntityAsync(id, nameof(UpdateSeatPoolAsync));
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			pool.AcademicYearId,
			nameof(UpdateSeatPoolAsync));
		if (!string.IsNullOrWhiteSpace(input.ConcurrencyStamp) &&
			!string.Equals(pool.ConcurrencyStamp, input.ConcurrencyStamp, StringComparison.Ordinal))
		{
			throw new BusinessException("AdmissionCrm:SeatPoolConcurrencyConflict");
		}
		if (!string.Equals(pool.Code, input.Code.Trim(), StringComparison.Ordinal))
		{
			throw new BusinessException("AdmissionCrm:SeatPoolCodeIsImmutable");
		}

		var definition = await ResolveSeatPoolDefinitionAsync(pool.AcademicYearId, input);
		pool.UpdateDefinition(input.PlaybookId, input.Name, definition.RoundCode, definition.Campus,
			definition.DesiredClass, definition.Board, definition.Medium, definition.ApplicantCategory,
			definition.OperationalTarget, definition.ApprovedCapacity, input.IsActive);
		await _seatPoolRepository.UpdateAsync(pool, autoSave: true);
		return MapSeatPool(pool);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewSeatPlans)]
	public async Task<PagedResultDto<AdmissionCrmSeatPoolDto>> GetSeatPoolsAsync(
		Guid academicYearId, GetAdmissionCrmSeatPoolListInput input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetSeatPoolsAsync));
		var queryable = await _seatPoolRepository.GetQueryableAsync();
		var query = queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
		if (!string.IsNullOrWhiteSpace(input.Filter))
		{
			var filter = input.Filter.Trim().ToLower();
			query = query.Where(x => x.Code.ToLower().Contains(filter) || x.Name.ToLower().Contains(filter));
		}
		if (input.Status.HasValue)
		{
			query = query.Where(x => x.Status == input.Status.Value);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var entities = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Code).Skip(input.SkipCount).Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionCrmSeatPoolDto>(totalCount, entities.Select(MapSeatPool).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public async Task<AdmissionCrmSeatOfferPreviewDto> PreviewOfferAsync(Guid leadId, CreateAdmissionCrmSeatOfferDto input)
	{
		var lead = await GetLeadEntityAsync(leadId, nameof(PreviewOfferAsync));
		var pool = await GetSeatPoolEntityAsync(input.SeatPoolId, nameof(PreviewOfferAsync));
		var blockers = new List<string>();
		if (pool.AcademicYearId != lead.AcademicYearId) blockers.Add("AdmissionCrm:SeatPoolAcademicYearMismatch");
		if (input.PlaybookId.HasValue && input.PlaybookId != pool.PlaybookId) blockers.Add("AdmissionCrm:SeatOfferPlaybookMismatch");
		if (pool.Status != AdmissionCrmSeatPoolStatus.Active) blockers.Add("AdmissionCrm:SeatPoolClosed");
		if (pool.RemainingCapacity <= 0) blockers.Add("AdmissionCrm:SeatPoolCapacityExhausted");
		try
		{
			await EnsureOfferReadinessAsync(lead, input.RegistrationApplicationId);
		}
		catch (BusinessException exception)
		{
			blockers.Add(exception.Code ?? "AdmissionCrm:SeatOfferReadinessFailed");
		}

		var offerQueryable = await _offerRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(offerQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId && OpenOfferStatuses.Contains(x.Status))))
		{
			blockers.Add("AdmissionCrm:LeadAlreadyHasOpenOffer");
		}
		try
		{
			_ = new AdmissionCrmSeatOffer(GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId, leadId,
				pool.Id, input.RegistrationApplicationId, pool.PlaybookId, null, "PREVIEW-ONLY", 1,
				input.OfferType, JsonSerializer.Serialize(input.Terms), input.Language, input.ValidFrom, input.ValidUntil);
		}
		catch (BusinessException exception)
		{
			blockers.Add(exception.Code ?? "AdmissionCrm:InvalidSeatOffer");
		}

		return new AdmissionCrmSeatOfferPreviewDto
		{
			LeadId = leadId,
			SeatPoolId = pool.Id,
			RemainingCapacity = pool.RemainingCapacity,
			BlockingReasons = blockers.Distinct().ToList(),
			IsReady = blockers.Count == 0
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public async Task<AdmissionCrmSeatOfferDto> CreateOfferAsync(Guid leadId, CreateAdmissionCrmSeatOfferDto input)
	{
		var lead = await GetLeadEntityAsync(leadId, nameof(CreateOfferAsync));
		var pool = await GetSeatPoolEntityAsync(input.SeatPoolId, nameof(CreateOfferAsync));
		if (pool.AcademicYearId != lead.AcademicYearId)
		{
			throw new BusinessException("AdmissionCrm:SeatPoolAcademicYearMismatch");
		}
		if (input.PlaybookId.HasValue && input.PlaybookId != pool.PlaybookId)
		{
			throw new BusinessException("AdmissionCrm:SeatOfferPlaybookMismatch");
		}
		await EnsureOfferReadinessAsync(lead, input.RegistrationApplicationId);

		var offerQueryable = await _offerRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(offerQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId && OpenOfferStatuses.Contains(x.Status))))
		{
			throw new BusinessException("AdmissionCrm:LeadAlreadyHasOpenOffer");
		}

		var priorOffers = await AsyncExecuter.ToListAsync(offerQueryable
			.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId)
			.OrderByDescending(x => x.Version));
		var version = priorOffers.Count == 0 ? 1 : priorOffers[0].Version + 1;
		var id = GuidGenerator.Create();
		var offer = new AdmissionCrmSeatOffer(
			id, CurrentTenant.Id, lead.AcademicYearId, leadId, pool.Id, input.RegistrationApplicationId,
			pool.PlaybookId, priorOffers.FirstOrDefault()?.Id,
			$"OFF-{Clock.Now:yyyyMMdd}-{id:N}".ToUpperInvariant(), version, input.OfferType,
			JsonSerializer.Serialize(input.Terms), input.Language, input.ValidFrom, input.ValidUntil);

		await _offerRepository.InsertAsync(offer, autoSave: true);
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.OfferCreated, "Seat offer created",
			$"Offer {offer.OfferNumber} version {offer.Version} was created.", offer.Id);
		return await MapOfferAsync(offer);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<PagedResultDto<AdmissionCrmSeatOfferDto>> GetOffersAsync(
		Guid academicYearId, GetAdmissionCrmSeatOfferListInput input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetOffersAsync));
		var leadQueryable = await _leadRepository.GetQueryableAsync();
		var scopedLeadQuery = leadQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId);
		scopedLeadQuery = await _studentScopeAccessService.ApplyCurrentUserAdmissionLeadScopeAsync(scopedLeadQuery);
		var scopedLeadIds = scopedLeadQuery.Select(x => x.Id);
		var queryable = await _offerRepository.GetQueryableAsync();
		var query = queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			scopedLeadIds.Contains(x.LeadId));
		if (input.LeadId.HasValue)
		{
			query = query.Where(x => x.LeadId == input.LeadId.Value);
		}
		if (input.SeatPoolId.HasValue)
		{
			query = query.Where(x => x.SeatPoolId == input.SeatPoolId.Value);
		}
		if (input.Status.HasValue)
		{
			query = query.Where(x => x.Status == input.Status.Value);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var offers = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime)
			.Skip(input.SkipCount).Take(input.MaxResultCount));
		var results = new List<AdmissionCrmSeatOfferDto>();
		foreach (var offer in offers)
		{
			results.Add(await MapOfferAsync(offer));
		}
		return new PagedResultDto<AdmissionCrmSeatOfferDto>(totalCount, results);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmSeatOfferDto> GetOfferAsync(Guid id) =>
		await MapOfferAsync(await GetOfferEntityAsync(id, nameof(GetOfferAsync)));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public async Task<AdmissionCrmSeatOfferDto> SubmitOfferAsync(Guid id)
	{
		var offer = await GetOfferEntityAsync(id, nameof(SubmitOfferAsync));
		offer.SubmitForApproval(GetCurrentUserId(), Clock.Now);
		await _offerRepository.UpdateAsync(offer, autoSave: true);
		await AddTimelineForOfferAsync(offer, AdmissionCrmTimelineEventType.OfferSubmittedForApproval,
			"Seat offer submitted", $"Offer {offer.OfferNumber} was submitted for approval.");
		return await MapOfferAsync(offer);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveOffers)]
	public async Task<AdmissionCrmSeatOfferDto> ApproveOfferAsync(Guid id)
	{
		var offer = await GetOfferEntityAsync(id, nameof(ApproveOfferAsync));
		offer.Approve(GetCurrentUserId(), Clock.Now);
		await _offerRepository.UpdateAsync(offer, autoSave: true);
		await AddTimelineForOfferAsync(offer, AdmissionCrmTimelineEventType.OfferApproved,
			"Seat offer approved", $"Offer {offer.OfferNumber} was approved.");
		return await MapOfferAsync(offer);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageSeatReservations)]
	public async Task<AdmissionCrmSeatOfferDto> IssueOfferAsync(Guid id, IssueAdmissionCrmSeatOfferDto input)
	{
		if (input.CapacityOverride && !await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideSeatCapacity))
		{
			throw new AbpAuthorizationException("Seat-capacity override permission is required.");
		}

		var offer = await GetOfferEntityAsync(id, nameof(IssueOfferAsync));
		var pool = await GetSeatPoolEntityAsync(offer.SeatPoolId, nameof(IssueOfferAsync));
		var reservationQueryable = await _reservationRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(reservationQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.SeatOfferId == offer.Id)))
		{
			throw new BusinessException("AdmissionCrm:SeatOfferAlreadyHasReservation");
		}

		var overrideUsed = pool.ReserveSeat(input.CapacityOverride, input.CapacityOverrideReason);
		var reservation = new AdmissionCrmSeatReservation(
			GuidGenerator.Create(), CurrentTenant.Id, offer.AcademicYearId, offer.LeadId, pool.Id, offer.Id,
			Clock.Now, offer.ValidUntil, overrideUsed, input.CapacityOverrideReason);
		offer.Issue(reservation.Id, Clock.Now);
		await _seatPoolRepository.UpdateAsync(pool, autoSave: true);
		await _reservationRepository.InsertAsync(reservation, autoSave: true);
		await _offerRepository.UpdateAsync(offer, autoSave: true);

		var waitlistQueryable = await _waitlistRepository.GetQueryableAsync();
		var waitlistEntry = await AsyncExecuter.FirstOrDefaultAsync(waitlistQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == offer.LeadId && x.SeatPoolId == pool.Id &&
			x.Status == AdmissionCrmWaitlistStatus.Active));
		if (waitlistEntry != null)
		{
				waitlistEntry.Promote(offer.Id, Clock.Now);
				await AddWaitlistHistoryAsync(waitlistEntry, AdmissionCrmWaitlistHistoryAction.Promoted,
					"Offer issued and the waitlist entry was promoted.", offer.Id);
			pool.RemoveWaitlistedApplicant();
			await _waitlistRepository.UpdateAsync(waitlistEntry, autoSave: true);
			await _seatPoolRepository.UpdateAsync(pool, autoSave: true);
			await AddTimelineForOfferAsync(offer, AdmissionCrmTimelineEventType.WaitlistPromoted,
				"Waitlist entry promoted", $"Waitlist entry was promoted through offer {offer.OfferNumber}.");
		}

		await AddTimelineForOfferAsync(offer, AdmissionCrmTimelineEventType.SeatReserved,
			"Seat reserved", $"Seat pool {pool.Code} reserved a seat for offer {offer.OfferNumber}.");
		await AddTimelineForOfferAsync(offer, AdmissionCrmTimelineEventType.OfferIssued,
			"Seat offer issued", $"Offer {offer.OfferNumber} is valid until {offer.ValidUntil:O}.");
		return await MapOfferAsync(offer);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public async Task<AdmissionCrmSeatOfferDto> AcceptOfferAsync(Guid id, RespondAdmissionCrmSeatOfferDto input)
	{
		var offer = await GetOfferEntityAsync(id, nameof(AcceptOfferAsync));
		await EnsurePaymentGateAsync(offer.LeadId, AdmissionCrmPaymentGateMilestone.OfferAcceptance);
		var reservation = await GetReservationForOfferAsync(offer.Id);
		var pool = await GetSeatPoolEntityAsync(offer.SeatPoolId, nameof(AcceptOfferAsync));
		offer.Accept(input.ResponseSource, Clock.Now, JsonSerializer.Serialize(new
		{
			input.Language, input.Declarations, input.EvidenceReference,
			ActorUserId = CurrentUser.Id, RecordedAt = Clock.Now, TermsVersion = offer.Version
		}));
		reservation.Commit(Clock.Now);
		pool.AcceptReservedSeat();
		await _offerRepository.UpdateAsync(offer, autoSave: true);
		await _reservationRepository.UpdateAsync(reservation, autoSave: true);
		await _seatPoolRepository.UpdateAsync(pool, autoSave: true);

		var waitlistQueryable = await _waitlistRepository.GetQueryableAsync();
		var waitlistEntry = await AsyncExecuter.FirstOrDefaultAsync(waitlistQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.OfferId == offer.Id && x.Status == AdmissionCrmWaitlistStatus.Promoted));
		if (waitlistEntry != null)
		{
			waitlistEntry.MarkAccepted(Clock.Now);
			await _waitlistRepository.UpdateAsync(waitlistEntry, autoSave: true);
		}

		await AddTimelineForOfferAsync(offer, AdmissionCrmTimelineEventType.OfferAccepted,
			"Seat offer accepted", $"Offer {offer.OfferNumber} was accepted through {input.ResponseSource}.");
		return await MapOfferAsync(offer);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public async Task<AdmissionCrmSeatOfferDto> DeclineOfferAsync(Guid id, RespondAdmissionCrmSeatOfferDto input)
	{
		var offer = await GetOfferEntityAsync(id, nameof(DeclineOfferAsync));
		offer.Decline(input.ResponseSource, input.Reason, Clock.Now, JsonSerializer.Serialize(new
		{
			input.Language, input.EvidenceReference,
			ActorUserId = CurrentUser.Id, RecordedAt = Clock.Now, TermsVersion = offer.Version
		}));
		await ReleaseReservationAsync(offer, input.Reason ?? "Offer declined.", false);
		await UpdatePromotedWaitlistAfterOfferClosureAsync(offer, PromotedWaitlistClosure.Declined);
		await _offerRepository.UpdateAsync(offer, autoSave: true);
		await AddTimelineForOfferAsync(offer, AdmissionCrmTimelineEventType.OfferDeclined,
			"Seat offer declined", input.Reason ?? $"Offer {offer.OfferNumber} was declined.");
		return await MapOfferAsync(offer);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public async Task<AdmissionCrmSeatOfferDto> RevokeOfferAsync(Guid id, AdmissionCrmReasonDto input)
	{
		var offer = await GetOfferEntityAsync(id, nameof(RevokeOfferAsync));
		offer.Revoke(input.Reason, Clock.Now);
		await ReleaseReservationAsync(offer, input.Reason, false);
		await UpdatePromotedWaitlistAfterOfferClosureAsync(offer, PromotedWaitlistClosure.Reactivated);
		await _offerRepository.UpdateAsync(offer, autoSave: true);
		await AddTimelineForOfferAsync(offer, AdmissionCrmTimelineEventType.OfferRevoked,
			"Seat offer revoked", input.Reason);
		return await MapOfferAsync(offer);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public async Task<AdmissionCrmSeatOfferDto> SupersedeOfferAsync(Guid id, SupersedeAdmissionCrmSeatOfferDto input)
	{
		var previous = await GetOfferEntityAsync(id, nameof(SupersedeOfferAsync));
		var lead = await GetLeadEntityAsync(previous.LeadId, nameof(SupersedeOfferAsync));
		var pool = await GetSeatPoolEntityAsync(input.SeatPoolId, nameof(SupersedeOfferAsync));
		if (pool.AcademicYearId != previous.AcademicYearId || pool.AcademicYearId != lead.AcademicYearId)
			throw new BusinessException("AdmissionCrm:SeatPoolAcademicYearMismatch");
		if (input.PlaybookId.HasValue && input.PlaybookId != pool.PlaybookId)
			throw new BusinessException("AdmissionCrm:SeatOfferPlaybookMismatch");
		await EnsureOfferReadinessAsync(lead, input.RegistrationApplicationId);

		var offerQueryable = await _offerRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(offerQueryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.LeadId == previous.LeadId && x.Id != previous.Id && OpenOfferStatuses.Contains(x.Status))))
			throw new BusinessException("AdmissionCrm:LeadAlreadyHasOpenOffer");
		var maxVersion = await AsyncExecuter.MaxAsync(offerQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == previous.LeadId).Select(x => x.Version));
		var replacementId = GuidGenerator.Create();
		var replacement = new AdmissionCrmSeatOffer(replacementId, CurrentTenant.Id, previous.AcademicYearId,
			previous.LeadId, pool.Id, input.RegistrationApplicationId, pool.PlaybookId, previous.Id,
			$"OFF-{Clock.Now:yyyyMMdd}-{replacementId:N}".ToUpperInvariant(), maxVersion + 1, input.OfferType,
			JsonSerializer.Serialize(input.Terms), input.Language, input.ValidFrom, input.ValidUntil);

		if (previous.Status is AdmissionCrmSeatOfferStatus.Issued or AdmissionCrmSeatOfferStatus.Opened)
		{
			await ReleaseReservationAsync(previous, $"Superseded: {input.Reason}", false);
			await UpdatePromotedWaitlistAfterOfferClosureAsync(previous, PromotedWaitlistClosure.Reactivated);
		}
		previous.MarkSuperseded(input.Reason, Clock.Now);
		await _offerRepository.UpdateAsync(previous, autoSave: true);
		await _offerRepository.InsertAsync(replacement, autoSave: true);
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.OfferSuperseded, "Seat offer superseded",
			$"Offer {previous.OfferNumber} was superseded by {replacement.OfferNumber}. Reason: {input.Reason}", replacement.Id);
		return await MapOfferAsync(replacement);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public async Task<AdmissionCrmSeatOfferDto> ExpireOfferAsync(Guid id)
	{
		var offer = await GetOfferEntityAsync(id, nameof(ExpireOfferAsync));
		return await ExpireOfferCoreAsync(offer);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAutomation)]
	public async Task<AdmissionCrmOfferExpiryRunResultDto> RunOfferExpiryAsync(Guid academicYearId)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(RunOfferExpiryAsync));
		return await RunOfferExpiryCoreAsync(academicYearId, enforceUserScope: true);
	}

	public Task<AdmissionCrmOfferExpiryRunResultDto> RunOfferExpiryFromAutomationAsync(Guid academicYearId) =>
		RunOfferExpiryCoreAsync(academicYearId, enforceUserScope: false);

	private async Task<AdmissionCrmOfferExpiryRunResultDto> RunOfferExpiryCoreAsync(
		Guid academicYearId,
		bool enforceUserScope)
	{
		var queryable = await _offerRepository.GetQueryableAsync();
		var overdueQuery = queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			(x.Status == AdmissionCrmSeatOfferStatus.Issued || x.Status == AdmissionCrmSeatOfferStatus.Opened) &&
			x.ValidUntil <= Clock.Now);
		if (enforceUserScope)
		{
			var leadQueryable = await _leadRepository.GetQueryableAsync();
			var scopedLeadQuery = leadQueryable.Where(x =>
				x.TenantId == CurrentTenant.Id &&
				x.AcademicYearId == academicYearId);
			scopedLeadQuery = await _studentScopeAccessService.ApplyCurrentUserAdmissionLeadScopeAsync(scopedLeadQuery);
			var scopedLeadIds = scopedLeadQuery.Select(x => x.Id);
			overdueQuery = overdueQuery.Where(x => scopedLeadIds.Contains(x.LeadId));
		}
		var overdue = await AsyncExecuter.ToListAsync(overdueQuery);
		var failed = new List<Guid>();
		var expired = 0;
		foreach (var offer in overdue)
		{
			try
			{
				await ExpireOfferCoreAsync(offer);
				expired++;
			}
			catch (Exception exception) when (exception is BusinessException or AbpAuthorizationException)
			{
				failed.Add(offer.Id);
				Logger.LogError(exception, "Failed to expire admission offer {OfferId}.", offer.Id);
			}
		}
		return new AdmissionCrmOfferExpiryRunResultDto
		{
			AcademicYearId = academicYearId, EvaluatedCount = overdue.Count,
			ExpiredCount = expired, FailedCount = failed.Count, FailedOfferIds = failed
		};
	}

	private async Task<AdmissionCrmSeatOfferDto> ExpireOfferCoreAsync(AdmissionCrmSeatOffer offer)
	{
		offer.Expire(Clock.Now);
		var reservation = await GetReservationForOfferAsync(offer.Id);
		var pool = await GetSeatPoolEntityAsync(offer.SeatPoolId, nameof(ExpireOfferAsync));
		reservation.Expire(Clock.Now);
		pool.ReleaseSeat(false, false);
		await _reservationRepository.UpdateAsync(reservation, autoSave: true);
		await _seatPoolRepository.UpdateAsync(pool, autoSave: true);
		await _offerRepository.UpdateAsync(offer, autoSave: true);
		await UpdatePromotedWaitlistAfterOfferClosureAsync(offer, PromotedWaitlistClosure.Expired);
		await AddTimelineForOfferAsync(offer, AdmissionCrmTimelineEventType.OfferExpired,
			"Seat offer expired", $"Offer {offer.OfferNumber} expired and its seat was released.");
		return await MapOfferAsync(offer);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageWaitlist)]
	public async Task<AdmissionCrmWaitlistEntryDto> AddToWaitlistAsync(Guid leadId, CreateAdmissionCrmWaitlistEntryDto input)
	{
		var lead = await GetLeadEntityAsync(leadId, nameof(AddToWaitlistAsync));
		var pool = await GetSeatPoolEntityAsync(input.SeatPoolId, nameof(AddToWaitlistAsync));
		if (pool.AcademicYearId != lead.AcademicYearId)
		{
			throw new BusinessException("AdmissionCrm:SeatPoolAcademicYearMismatch");
		}

		var queryable = await _waitlistRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId && x.SeatPoolId == pool.Id &&
			x.Status == AdmissionCrmWaitlistStatus.Active)))
		{
			throw new BusinessException("AdmissionCrm:LeadAlreadyWaitlisted");
		}
		var lastSequence = await AsyncExecuter.FirstOrDefaultAsync(queryable
			.Where(x => x.TenantId == CurrentTenant.Id && x.SeatPoolId == pool.Id)
			.OrderByDescending(x => x.PositionSequence));
		var entry = new AdmissionCrmWaitlistEntry(
			GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId, lead.Id, pool.Id,
			input.PriorityScore, (lastSequence?.PositionSequence ?? 0) + 1, Clock.Now);
		pool.AddWaitlistedApplicant();
		lead.ChangeStage(AdmissionInquiryStage.Waitlisted);
		await _waitlistRepository.InsertAsync(entry, autoSave: true);
		await _seatPoolRepository.UpdateAsync(pool, autoSave: true);
		await _leadRepository.UpdateAsync(lead, autoSave: true);
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.Waitlisted, "Applicant waitlisted",
			$"Applicant was added to seat pool {pool.Code} with priority score {entry.PriorityScore}.", entry.Id);
		await AddWaitlistHistoryAsync(entry, AdmissionCrmWaitlistHistoryAction.Added,
			$"Applicant added with priority score {entry.PriorityScore}.");
		return await MapWaitlistEntryAsync(entry, 0);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewSeatPlans)]
	public async Task<IReadOnlyList<AdmissionCrmWaitlistEntryDto>> GetWaitlistAsync(Guid seatPoolId)
	{
		await GetSeatPoolEntityAsync(seatPoolId, nameof(GetWaitlistAsync));
		var leadQueryable = await _leadRepository.GetQueryableAsync();
		var scopedLeadQuery = await _studentScopeAccessService.ApplyCurrentUserAdmissionLeadScopeAsync(
			leadQueryable.Where(x => x.TenantId == CurrentTenant.Id));
		var scopedLeadIds = scopedLeadQuery.Select(x => x.Id);
		var queryable = await _waitlistRepository.GetQueryableAsync();
		var entries = await AsyncExecuter.ToListAsync(queryable
			.Where(x =>
				x.TenantId == CurrentTenant.Id &&
				x.SeatPoolId == seatPoolId &&
				scopedLeadIds.Contains(x.LeadId))
			.OrderBy(x => x.Status == AdmissionCrmWaitlistStatus.Active ? 0 : 1)
			.ThenByDescending(x => x.PriorityScore)
			.ThenBy(x => x.PositionSequence));
		var activePosition = 0;
		var results = new List<AdmissionCrmWaitlistEntryDto>();
		foreach (var entry in entries)
		{
			if (entry.Status == AdmissionCrmWaitlistStatus.Active)
			{
				activePosition++;
			}
			results.Add(await MapWaitlistEntryAsync(entry,
				entry.Status == AdmissionCrmWaitlistStatus.Active ? activePosition : 0));
		}
		return results;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageWaitlist)]
	public async Task<AdmissionCrmSeatOfferDto> PrepareWaitlistPromotionAsync(
		Guid entryId, PrepareAdmissionCrmWaitlistPromotionDto input)
	{
		var entry = await GetWaitlistEntryAsync(entryId, nameof(PrepareWaitlistPromotionAsync));
		if (entry.Status != AdmissionCrmWaitlistStatus.Active)
		{
			throw new BusinessException("AdmissionCrm:WaitlistEntryNotActive");
		}
		if (input.SeatPoolId != entry.SeatPoolId)
		{
			throw new BusinessException("AdmissionCrm:WaitlistSeatPoolMismatch");
		}
		var waitlistQueryable = await _waitlistRepository.GetQueryableAsync();
		var activeEntries = await AsyncExecuter.ToListAsync(waitlistQueryable
			.Where(x => x.TenantId == CurrentTenant.Id && x.SeatPoolId == entry.SeatPoolId &&
				x.Status == AdmissionCrmWaitlistStatus.Active && !x.PromotionPaused));
		var nextEntry = activeEntries
			.OrderByDescending(x => x.PriorityScore)
			.ThenBy(x => x.PositionSequence)
			.FirstOrDefault();
		if (nextEntry?.Id != entry.Id)
		{
			if (!input.OverridePriority)
			{
				throw new BusinessException("AdmissionCrm:HigherPriorityWaitlistEntryExists");
			}
			if (!await AuthorizationService.IsGrantedAsync(
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideEligibility))
			{
				throw new AbpAuthorizationException("Waitlist-priority override permission is required.");
			}
			if (string.IsNullOrWhiteSpace(input.OverrideReason))
			{
				throw new BusinessException("AdmissionCrm:WaitlistPriorityOverrideReasonRequired");
			}
			var lead = await GetLeadEntityAsync(entry.LeadId, nameof(PrepareWaitlistPromotionAsync));
			await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.WaitlistPromotionOverridden,
				"Waitlist priority overridden", input.OverrideReason, entry.Id);
		}
		var offer = await CreateOfferAsync(entry.LeadId, input);
		await AddWaitlistHistoryAsync(entry, AdmissionCrmWaitlistHistoryAction.OfferPrepared,
			"Waitlist promotion prepared a draft offer for approval.", offer.Id);
		return offer;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageWaitlist)]
	public async Task<AdmissionCrmWaitlistEntryDto> WithdrawFromWaitlistAsync(Guid entryId, AdmissionCrmReasonDto input)
	{
		var entry = await GetWaitlistEntryAsync(entryId, nameof(WithdrawFromWaitlistAsync));
		var pool = await GetSeatPoolEntityAsync(entry.SeatPoolId, nameof(WithdrawFromWaitlistAsync));
		entry.Withdraw(input.Reason, Clock.Now);
		await AddWaitlistHistoryAsync(entry, AdmissionCrmWaitlistHistoryAction.Withdrawn, input.Reason);
		pool.RemoveWaitlistedApplicant();
		await _waitlistRepository.UpdateAsync(entry, autoSave: true);
		await _seatPoolRepository.UpdateAsync(pool, autoSave: true);
		var lead = await GetLeadEntityAsync(entry.LeadId, nameof(WithdrawFromWaitlistAsync));
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.SeatReleased,
			"Waitlist entry withdrawn", input.Reason, entry.Id);
		return await MapWaitlistEntryAsync(entry, 0);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageWaitlist)]
	public async Task<AdmissionCrmWaitlistEntryDto> UpdateWaitlistGovernanceAsync(Guid entryId, UpdateAdmissionCrmWaitlistGovernanceDto input)
	{
		var entry = await GetWaitlistEntryAsync(entryId, nameof(UpdateWaitlistGovernanceAsync));
		var previousPriority = entry.PriorityScore;
		var wasProtected = entry.IsProtected;
		var wasPaused = entry.PromotionPaused;
		entry.UpdateGovernance(input.PriorityScore, input.IsProtected, input.PromotionPaused, input.Reason);
		await _waitlistRepository.UpdateAsync(entry, autoSave: true);
		if (previousPriority != entry.PriorityScore)
			await AddWaitlistHistoryAsync(entry, AdmissionCrmWaitlistHistoryAction.PriorityChanged, input.Reason, previousPriorityScore: previousPriority);
		if (wasProtected != entry.IsProtected)
			await AddWaitlistHistoryAsync(entry, entry.IsProtected ? AdmissionCrmWaitlistHistoryAction.Protected : AdmissionCrmWaitlistHistoryAction.ProtectionRemoved, input.Reason);
		if (wasPaused != entry.PromotionPaused)
			await AddWaitlistHistoryAsync(entry, entry.PromotionPaused ? AdmissionCrmWaitlistHistoryAction.PromotionPaused : AdmissionCrmWaitlistHistoryAction.PromotionResumed, input.Reason);
		var lead = await GetLeadEntityAsync(entry.LeadId, nameof(UpdateWaitlistGovernanceAsync));
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.WaitlistGovernanceChanged, "Waitlist governance updated", input.Reason, entry.Id);
		return await MapWaitlistEntryAsync(entry, 0);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewSeatPlans)]
	public async Task<IReadOnlyList<AdmissionCrmWaitlistHistoryDto>> GetWaitlistHistoryAsync(Guid entryId)
	{
		var entry = await GetWaitlistEntryAsync(entryId, nameof(GetWaitlistHistoryAsync));
		var queryable = await _waitlistHistoryRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.WaitlistEntryId == entry.Id));
		return items.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Select(MapWaitlistHistory).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageWaitlist)]
	public async Task<AdmissionCrmWaitlistPromotionRunResultDto> RunWaitlistPromotionAsync(Guid academicYearId, RunAdmissionCrmWaitlistPromotionDto input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(RunWaitlistPromotionAsync));
		if (input.MaximumOffers is < 1 or > 100) throw new BusinessException("AdmissionCrm:InvalidWaitlistPromotionBatch");
		var pool = await GetSeatPoolEntityAsync(input.SeatPoolId, nameof(RunWaitlistPromotionAsync));
		if (pool.AcademicYearId != academicYearId || input.Offer.SeatPoolId != pool.Id)
			throw new BusinessException("AdmissionCrm:WaitlistSeatPoolMismatch");
		var leadQueryable = await _leadRepository.GetQueryableAsync();
		var scopedLeadQuery = leadQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId);
		scopedLeadQuery = await _studentScopeAccessService.ApplyCurrentUserAdmissionLeadScopeAsync(scopedLeadQuery);
		var scopedLeadIds = await AsyncExecuter.ToListAsync(scopedLeadQuery.Select(x => x.Id));
		var queryable = await _waitlistRepository.GetQueryableAsync();
		var entries = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.SeatPoolId == pool.Id &&
			scopedLeadIds.Contains(x.LeadId) &&
			x.Status == AdmissionCrmWaitlistStatus.Active &&
			!x.PromotionPaused));
		var offerQueryable = await _offerRepository.GetQueryableAsync();
		var openLeadIds = (await AsyncExecuter.ToListAsync(offerQueryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && OpenOfferStatuses.Contains(x.Status)))).Select(x => x.LeadId).ToHashSet();
		var candidates = entries.Where(x => !openLeadIds.Contains(x.LeadId))
			.OrderByDescending(x => x.PriorityScore).ThenBy(x => x.PositionSequence)
			.Take(Math.Min(input.MaximumOffers, pool.RemainingCapacity)).ToList();
		var offerIds = new List<Guid>();
		var failedIds = new List<Guid>();
		foreach (var entry in candidates)
		{
			try
			{
				var offer = await CreateOfferAsync(entry.LeadId, input.Offer);
				offerIds.Add(offer.Id);
				await AddWaitlistHistoryAsync(entry, AdmissionCrmWaitlistHistoryAction.OfferPrepared,
					"Automated promotion prepared a draft offer for staff approval.", offer.Id);
			}
			catch (Exception exception) when (exception is BusinessException or AbpAuthorizationException)
			{
				failedIds.Add(entry.Id);
				Logger.LogError(exception, "Failed to prepare waitlist promotion for entry {EntryId}.", entry.Id);
			}
		}
		return new AdmissionCrmWaitlistPromotionRunResultDto
		{
			SeatPoolId = pool.Id, EvaluatedCount = entries.Count, PreparedCount = offerIds.Count,
			OfferIds = offerIds, FailedEntryIds = failedIds
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.MakeAdmissionDecision)]
	public async Task<AdmissionCrmAdmissionDecisionDto> RecordDecisionAsync(
		Guid leadId, RecordAdmissionCrmAdmissionDecisionDto input)
	{
		var lead = await GetLeadEntityAsync(leadId, nameof(RecordDecisionAsync));
		if (input.OverrideUsed && !await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideEligibility))
		{
			throw new AbpAuthorizationException("Admission-decision override permission is required.");
		}
		if (input.Status == AdmissionCrmAdmissionDecisionStatus.Cancelled &&
			!await AuthorizationService.IsGrantedAsync(
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.CancelAdmission))
		{
			throw new AbpAuthorizationException("Admission-cancellation permission is required.");
		}
		AdmissionCrmSeatOffer? offer = null;
		if (input.OfferId.HasValue)
		{
			offer = await GetOfferEntityAsync(input.OfferId.Value, nameof(RecordDecisionAsync));
			if (offer.LeadId != leadId || (input.SeatPoolId.HasValue && offer.SeatPoolId != input.SeatPoolId.Value))
			{
				throw new BusinessException("AdmissionCrm:AdmissionDecisionOfferMismatch");
			}
		}
		if (input.Status == AdmissionCrmAdmissionDecisionStatus.Approved &&
			!input.OverrideUsed && (offer == null || offer.Status != AdmissionCrmSeatOfferStatus.Accepted))
		{
			throw new BusinessException("AdmissionCrm:AcceptedOfferRequiredForApproval");
		}
		if (input.Status == AdmissionCrmAdmissionDecisionStatus.Approved && !input.OverrideUsed)
		{
			await EnsurePaymentGateAsync(leadId, AdmissionCrmPaymentGateMilestone.FinalApproval);
		}
		if (input.Status == AdmissionCrmAdmissionDecisionStatus.Cancelled && offer == null)
		{
			throw new BusinessException("AdmissionCrm:AcceptedOfferRequiredForCancellation");
		}

		var decisionQueryable = await _decisionRepository.GetQueryableAsync();
		var previousDecision = await AsyncExecuter.FirstOrDefaultAsync(decisionQueryable
			.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId)
			.OrderByDescending(x => x.DecidedAt));
		if (previousDecision != null && !input.OverrideUsed)
		{
			throw new BusinessException("AdmissionCrm:AdmissionDecisionOverrideRequired");
		}

		var decision = new AdmissionCrmAdmissionDecision(
			GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId, lead.Id);
		decision.Decide(input.Status, input.SeatPoolId ?? offer?.SeatPoolId, input.OfferId, input.Reason,
			input.OverrideUsed, input.OverrideReason, GetCurrentUserId(), Clock.Now);
		if (input.Status == AdmissionCrmAdmissionDecisionStatus.Cancelled &&
			offer?.Status == AdmissionCrmSeatOfferStatus.Accepted)
		{
			offer.CancelAccepted(input.Reason, Clock.Now);
			await ReleaseReservationAsync(offer, input.Reason, true);
			await _offerRepository.UpdateAsync(offer, autoSave: true);
		}
		ApplyDecisionToLead(lead, input.Status, input.Reason);
		await _decisionRepository.InsertAsync(decision, autoSave: true);
		await _leadRepository.UpdateAsync(lead, autoSave: true);
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.AdmissionDecisionRecorded,
			"Admission decision recorded", $"Decision: {input.Status}. Reason: {input.Reason}", decision.Id);
		return MapDecision(decision);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmAdmissionDecisionDto?> GetDecisionAsync(Guid leadId)
	{
		await GetLeadEntityAsync(leadId, nameof(GetDecisionAsync));
		var queryable = await _decisionRepository.GetQueryableAsync();
		var decision = await AsyncExecuter.FirstOrDefaultAsync(queryable
			.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId)
			.OrderByDescending(x => x.DecidedAt));
		return decision == null ? null : MapDecision(decision);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<IReadOnlyList<AdmissionCrmAdmissionDecisionDto>> GetDecisionHistoryAsync(Guid leadId)
	{
		await GetLeadEntityAsync(leadId, nameof(GetDecisionHistoryAsync));
		var queryable = await _decisionRepository.GetQueryableAsync();
		var decisions = await AsyncExecuter.ToListAsync(queryable
			.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId)
			.OrderByDescending(x => x.DecidedAt));
		return decisions.Select(MapDecision).ToList();
	}

	private async Task EnsurePaymentGateAsync(Guid leadId, AdmissionCrmPaymentGateMilestone milestone)
	{
		var queryable = await _quotationRepository.GetQueryableAsync();
		var quotation = await AsyncExecuter.FirstOrDefaultAsync(queryable
			.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId &&
				x.Status == AdmissionCrmQuotationStatus.Issued)
			.OrderByDescending(x => x.Version));
		if (quotation == null || quotation.GateMilestone > milestone || quotation.IsGateSatisfiedAt(Clock.Now))
		{
			return;
		}

		throw new BusinessException("AdmissionCrm:PaymentGateNotSatisfied")
			.WithData("QuotationId", quotation.Id)
			.WithData("GateType", quotation.GateType)
			.WithData("GateMilestone", quotation.GateMilestone)
			.WithData("FinanceState", quotation.FinanceState)
			.WithData("RequiredAmount", quotation.RequiredAmount)
			.WithData("ConfirmedAmount", quotation.ConfirmedAmount);
	}

	private async Task<SeatPoolDefinition> ResolveSeatPoolDefinitionAsync(
		Guid academicYearId, UpsertAdmissionCrmSeatPoolDto input)
	{
		if (!input.PlaybookId.HasValue)
		{
			return new SeatPoolDefinition(input.RoundCode, input.Campus, input.DesiredClass, input.Board,
				input.Medium, input.ApplicantCategory, input.OperationalTarget, input.ApprovedCapacity);
		}

		var queryable = await _playbookRepository.GetQueryableAsync();
		var playbook = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.Id == input.PlaybookId.Value && x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		if (playbook == null || playbook.Status != AdmissionCrmPlaybookStatus.Published)
		{
			throw new BusinessException("AdmissionCrm:PublishedPlaybookRequiredForSeatPool");
		}

		var seatPlans = JsonSerializer.Deserialize<List<AdmissionCrmSeatPlanDto>>(
			playbook.SeatPlansJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
		var seatPlan = seatPlans.FirstOrDefault(x => string.Equals(x.Code, input.Code, StringComparison.OrdinalIgnoreCase));
		if (seatPlan == null)
		{
			throw new BusinessException("AdmissionCrm:SeatPlanNotFoundInPlaybook");
		}
		return new SeatPoolDefinition(seatPlan.RoundCode, seatPlan.Campus, seatPlan.DesiredClass, seatPlan.Board,
			seatPlan.Medium, seatPlan.ApplicantCategory, seatPlan.PlannedSeats, seatPlan.ApprovedCapacity);
	}

	private async Task EnsureOfferReadinessAsync(AdmissionInquiryLead lead, Guid? registrationApplicationId)
	{
		var registrationQueryable = await _registrationRepository.GetQueryableAsync();
		if (registrationApplicationId.HasValue)
		{
			var registration = await AsyncExecuter.FirstOrDefaultAsync(registrationQueryable.Where(x =>
				x.Id == registrationApplicationId.Value && x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
			if (registration == null || registration.Status != AdmissionCrmRegistrationStatus.AdmissionReady)
			{
				throw new BusinessException("AdmissionCrm:AdmissionReadyRegistrationRequired");
			}
			return;
		}

		var hasRegistration = await AsyncExecuter.AnyAsync(registrationQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		var hasAdmissionReadyRegistration = await AsyncExecuter.AnyAsync(registrationQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id && x.Status == AdmissionCrmRegistrationStatus.AdmissionReady));
		if ((hasRegistration && !hasAdmissionReadyRegistration) ||
			(!hasRegistration && lead.Stage is not (AdmissionInquiryStage.Eligible or AdmissionInquiryStage.Waitlisted)))
		{
			throw new BusinessException("AdmissionCrm:ApplicantNotReadyForSeatOffer");
		}
	}

	private async Task ReleaseReservationAsync(AdmissionCrmSeatOffer offer, string reason, bool cancelled)
	{
		if (!offer.ReservationId.HasValue)
		{
			return;
		}
		var reservation = await GetReservationForOfferAsync(offer.Id);
		if (reservation.Status is AdmissionCrmSeatReservationStatus.Released or
			AdmissionCrmSeatReservationStatus.Cancelled or AdmissionCrmSeatReservationStatus.Expired)
		{
			return;
		}
		var pool = await GetSeatPoolEntityAsync(offer.SeatPoolId, nameof(ReleaseReservationAsync));
		var wasAccepted = reservation.Status == AdmissionCrmSeatReservationStatus.Committed;
		reservation.Release(reason, Clock.Now, cancelled);
		pool.ReleaseSeat(wasAccepted, cancelled);
		await _reservationRepository.UpdateAsync(reservation, autoSave: true);
		await _seatPoolRepository.UpdateAsync(pool, autoSave: true);
		await AddTimelineForOfferAsync(offer, AdmissionCrmTimelineEventType.SeatReleased,
			"Seat released", reason);
	}

	private async Task UpdatePromotedWaitlistAfterOfferClosureAsync(
		AdmissionCrmSeatOffer offer,
		PromotedWaitlistClosure closure)
	{
		var queryable = await _waitlistRepository.GetQueryableAsync();
		var entry = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.OfferId == offer.Id &&
			x.Status == AdmissionCrmWaitlistStatus.Promoted));
		if (entry == null)
		{
			return;
		}
		if (closure == PromotedWaitlistClosure.Reactivated)
		{
			entry.ReactivatePromotedOffer();
			var pool = await GetSeatPoolEntityAsync(entry.SeatPoolId, nameof(UpdatePromotedWaitlistAfterOfferClosureAsync));
			pool.AddWaitlistedApplicant();
			await _seatPoolRepository.UpdateAsync(pool, autoSave: true);
		}
		else if (closure == PromotedWaitlistClosure.Expired)
		{
			entry.ExpirePromotedOffer(Clock.Now);
		}
		else
		{
			entry.ClosePromotedOffer(offer.ResponseReason ?? "Offer declined.", Clock.Now);
		}
		await _waitlistRepository.UpdateAsync(entry, autoSave: true);
	}

	private async Task<AdmissionInquiryLead> GetLeadEntityAsync(Guid id, string operation)
	{
		var queryable = await _leadRepository.GetQueryableAsync();
		var lead = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.Id == id && x.TenantId == CurrentTenant.Id));
		if (lead == null)
		{
			throw new BusinessException("AdmissionCrm:LeadNotFound");
		}
		await _studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return lead;
	}

	private async Task<AdmissionCrmSeatPool> GetSeatPoolEntityAsync(Guid id, string operation)
	{
		var queryable = await _seatPoolRepository.GetQueryableAsync();
		var pool = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.Id == id && x.TenantId == CurrentTenant.Id));
		if (pool == null)
		{
			throw new BusinessException("AdmissionCrm:SeatPoolNotFound");
		}
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(pool.AcademicYearId, operation);
		return pool;
	}

	private async Task<AdmissionCrmSeatOffer> GetOfferEntityAsync(Guid id, string operation)
	{
		var queryable = await _offerRepository.GetQueryableAsync();
		var offer = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.Id == id && x.TenantId == CurrentTenant.Id));
		if (offer == null)
		{
			throw new BusinessException("AdmissionCrm:SeatOfferNotFound");
		}
		var lead = await _leadRepository.GetAsync(offer.LeadId);
		await _studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return offer;
	}

	private async Task<AdmissionCrmSeatReservation> GetReservationForOfferAsync(Guid offerId)
	{
		var queryable = await _reservationRepository.GetQueryableAsync();
		var reservation = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.SeatOfferId == offerId));
		return reservation ?? throw new BusinessException("AdmissionCrm:SeatReservationNotFound");
	}

	private async Task<AdmissionCrmWaitlistEntry> GetWaitlistEntryAsync(Guid id, string operation)
	{
		var queryable = await _waitlistRepository.GetQueryableAsync();
		var entry = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.Id == id && x.TenantId == CurrentTenant.Id));
		if (entry == null)
		{
			throw new BusinessException("AdmissionCrm:WaitlistEntryNotFound");
		}
		var lead = await _leadRepository.GetAsync(entry.LeadId);
		await _studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return entry;
	}

	private Guid GetCurrentUserId() => CurrentUser.Id ??
		throw new AbpAuthorizationException("An authenticated user is required.");

	private void ApplyDecisionToLead(
		AdmissionInquiryLead lead, AdmissionCrmAdmissionDecisionStatus status, string reason)
	{
		switch (status)
		{
			case AdmissionCrmAdmissionDecisionStatus.Approved:
				lead.ChangeStage(AdmissionInquiryStage.Eligible);
				break;
			case AdmissionCrmAdmissionDecisionStatus.Rejected:
				lead.ChangeStage(AdmissionInquiryStage.Rejected);
				break;
			case AdmissionCrmAdmissionDecisionStatus.Waitlisted:
				lead.ChangeStage(AdmissionInquiryStage.Waitlisted);
				break;
			case AdmissionCrmAdmissionDecisionStatus.Withdrawn:
			case AdmissionCrmAdmissionDecisionStatus.Expired:
			case AdmissionCrmAdmissionDecisionStatus.Cancelled:
				lead.Archive(reason, Clock.Now);
				break;
		}
	}

	private async Task AddTimelineForOfferAsync(
		AdmissionCrmSeatOffer offer, AdmissionCrmTimelineEventType type, string title, string details)
	{
		var lead = await GetLeadEntityAsync(offer.LeadId, nameof(AddTimelineForOfferAsync));
		await AddTimelineAsync(lead, type, title, details, offer.Id);
	}

	private async Task AddTimelineAsync(
		AdmissionInquiryLead lead, AdmissionCrmTimelineEventType type, string title, string? details, Guid relatedEntityId)
	{
		await _timelineRepository.InsertAsync(new AdmissionCrmTimelineEvent(
			GuidGenerator.Create(), CurrentTenant.Id, lead.Id, lead.AcademicYearId, type, title, details,
			relatedEntityId, CurrentUser.Id, Clock.Now), autoSave: true);
	}

	private static AdmissionCrmSeatPoolDto MapSeatPool(AdmissionCrmSeatPool pool) => new()
	{
		Id = pool.Id,
		AcademicYearId = pool.AcademicYearId,
		PlaybookId = pool.PlaybookId,
		Code = pool.Code,
		Name = pool.Name,
		RoundCode = pool.RoundCode,
		Campus = pool.Campus,
		DesiredClass = pool.DesiredClass,
		Board = pool.Board,
		Medium = pool.Medium,
		ApplicantCategory = pool.ApplicantCategory,
		OperationalTarget = pool.OperationalTarget,
		ApprovedCapacity = pool.ApprovedCapacity,
		ReservedSeats = pool.ReservedSeats,
		AcceptedSeats = pool.AcceptedSeats,
		ActivatedSeats = pool.ActivatedSeats,
		WaitlistedApplicants = pool.WaitlistedApplicants,
		ReleasedSeats = pool.ReleasedSeats,
		CancelledSeats = pool.CancelledSeats,
		OccupiedSeats = pool.OccupiedSeats,
		RemainingCapacity = pool.RemainingCapacity,
		IsOverCapacity = pool.IsOverCapacity,
		Status = pool.Status,
		ConcurrencyStamp = pool.ConcurrencyStamp
	};

	private async Task<AdmissionCrmSeatOfferDto> MapOfferAsync(AdmissionCrmSeatOffer offer)
	{
		var lead = await _leadRepository.GetAsync(offer.LeadId);
		await _studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(lead, nameof(MapOfferAsync));
		AdmissionCrmSeatReservationDto? reservationDto = null;
		if (offer.ReservationId.HasValue)
		{
			var reservation = await GetReservationForOfferAsync(offer.Id);
			reservationDto = MapReservation(reservation);
		}
		return new AdmissionCrmSeatOfferDto
		{
			Id = offer.Id,
			AcademicYearId = offer.AcademicYearId,
			LeadId = offer.LeadId,
			SeatPoolId = offer.SeatPoolId,
			RegistrationApplicationId = offer.RegistrationApplicationId,
			PlaybookId = offer.PlaybookId,
			PreviousOfferId = offer.PreviousOfferId,
			ReservationId = offer.ReservationId,
			OfferNumber = offer.OfferNumber,
			Version = offer.Version,
			OfferType = offer.OfferType,
			Status = offer.Status,
			Terms = JsonSerializer.Deserialize<Dictionary<string, string?>>(offer.TermsSnapshotJson) ?? new(),
			Language = offer.Language,
			ValidFrom = offer.ValidFrom,
			ValidUntil = offer.ValidUntil,
			SubmittedByUserId = offer.SubmittedByUserId,
			SubmittedAt = offer.SubmittedAt,
			ApprovedByUserId = offer.ApprovedByUserId,
			ApprovedAt = offer.ApprovedAt,
			IssuedAt = offer.IssuedAt,
			OpenedAt = offer.OpenedAt,
			RespondedAt = offer.RespondedAt,
			ResponseSource = offer.ResponseSource,
			ResponseReason = offer.ResponseReason,
			ResponseSnapshot = JsonSerializer.Deserialize<Dictionary<string, object?>>(offer.ResponseSnapshotJson) ?? new(),
			Reservation = reservationDto
		};
	}

	private static AdmissionCrmSeatReservationDto MapReservation(AdmissionCrmSeatReservation reservation) => new()
	{
		Id = reservation.Id,
		LeadId = reservation.LeadId,
		SeatPoolId = reservation.SeatPoolId,
		SeatOfferId = reservation.SeatOfferId,
		Status = reservation.Status,
		ReservedAt = reservation.ReservedAt,
		ExpiresAt = reservation.ExpiresAt,
		CommittedAt = reservation.CommittedAt,
		ReleasedAt = reservation.ReleasedAt,
		ReleaseReason = reservation.ReleaseReason,
		CapacityOverrideUsed = reservation.CapacityOverrideUsed,
		CapacityOverrideReason = reservation.CapacityOverrideReason
	};

	private async Task<AdmissionCrmWaitlistEntryDto> MapWaitlistEntryAsync(
		AdmissionCrmWaitlistEntry entry, int currentPosition)
	{
		var lead = await _leadRepository.GetAsync(entry.LeadId);
		await _studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(lead, nameof(MapWaitlistEntryAsync));
		return new AdmissionCrmWaitlistEntryDto
		{
			Id = entry.Id,
			AcademicYearId = entry.AcademicYearId,
			LeadId = entry.LeadId,
			SeatPoolId = entry.SeatPoolId,
			PriorityScore = entry.PriorityScore,
			PositionSequence = entry.PositionSequence,
			CurrentPosition = currentPosition,
			Status = entry.Status,
			AddedAt = entry.AddedAt,
			PromotedAt = entry.PromotedAt,
			OfferId = entry.OfferId,
			ClosedAt = entry.ClosedAt,
			ClosureReason = entry.ClosureReason,
			IsProtected = entry.IsProtected,
			ProtectionReason = entry.ProtectionReason,
			PromotionPaused = entry.PromotionPaused,
			PromotionPauseReason = entry.PromotionPauseReason
		};
	}

	private Task AddWaitlistHistoryAsync(AdmissionCrmWaitlistEntry entry, AdmissionCrmWaitlistHistoryAction action,
		string reason, Guid? offerId = null, int? previousPriorityScore = null) =>
		_waitlistHistoryRepository.InsertAsync(new AdmissionCrmWaitlistHistory(GuidGenerator.Create(), CurrentTenant.Id,
			entry.AcademicYearId, entry.Id, entry.LeadId, entry.SeatPoolId, action, previousPriorityScore,
			entry.PriorityScore, reason, offerId, CurrentUser.Id, Clock.Now), autoSave: true);

	private static AdmissionCrmWaitlistHistoryDto MapWaitlistHistory(AdmissionCrmWaitlistHistory x) => new()
	{
		Id = x.Id, WaitlistEntryId = x.WaitlistEntryId, Action = x.Action,
		PreviousPriorityScore = x.PreviousPriorityScore, PriorityScore = x.PriorityScore,
		Reason = x.Reason, RelatedOfferId = x.RelatedOfferId, ActorUserId = x.ActorUserId, OccurredAt = x.OccurredAt
	};

	private static AdmissionCrmAdmissionDecisionDto MapDecision(AdmissionCrmAdmissionDecision decision) => new()
	{
		Id = decision.Id,
		AcademicYearId = decision.AcademicYearId,
		LeadId = decision.LeadId,
		SeatPoolId = decision.SeatPoolId,
		OfferId = decision.OfferId,
		Status = decision.Status,
		Reason = decision.Reason,
		OverrideUsed = decision.OverrideUsed,
		OverrideReason = decision.OverrideReason,
		DecidedByUserId = decision.DecidedByUserId,
		DecidedAt = decision.DecidedAt
	};

	private sealed record SeatPoolDefinition(
		string? RoundCode,
		string? Campus,
		string? DesiredClass,
		string? Board,
		string? Medium,
		string? ApplicantCategory,
		int OperationalTarget,
		int ApprovedCapacity);

	private enum PromotedWaitlistClosure
	{
		Declined,
		Reactivated,
		Expired
	}
}
