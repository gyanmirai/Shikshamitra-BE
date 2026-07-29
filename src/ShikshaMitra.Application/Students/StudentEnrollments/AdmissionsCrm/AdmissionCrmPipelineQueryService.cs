using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public sealed class AdmissionCrmPipelineReportRow
{
	public Guid LeadId { get; init; }
	public Guid? TargetStandardId { get; init; }
	public string? InquirySourceName { get; init; }
	public AdmissionCrmPipelineColumnKey? Column { get; init; }
	public AdmissionCrmClosedOutcome? ClosedOutcome { get; init; }
	public bool ExamScheduled { get; init; }
	public bool ExamCompleted { get; init; }
	public bool Eligible { get; init; }
	public bool RegistrationVerified { get; init; }
}

public class AdmissionCrmPipelineQueryService : ITransientDependency
{
	private readonly IRepository<AdmissionCrmEntranceExamRecord, Guid> _examRepository;
	private readonly IRepository<AdmissionCrmRegistrationApplication, Guid> _registrationRepository;
	private readonly IRepository<AdmissionCrmAdmissionQuotation, Guid> _quotationRepository;
	private readonly IRepository<AdmissionCrmSeatOffer, Guid> _seatOfferRepository;
	private readonly IRepository<AdmissionCrmActivation, Guid> _activationRepository;
	private readonly IRepository<AdmissionCrmFollowUpTask, Guid> _followUpRepository;
	private readonly IRepository<AdmissionCrmAdmissionYearSetup, Guid> _admissionYearRepository;
	private readonly IRepository<AdmissionCrmCycleConfiguration, Guid> _cycleRepository;

	public AdmissionCrmPipelineQueryService(
		IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
		IRepository<AdmissionCrmRegistrationApplication, Guid> registrationRepository,
		IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
		IRepository<AdmissionCrmSeatOffer, Guid> seatOfferRepository,
		IRepository<AdmissionCrmActivation, Guid> activationRepository,
		IRepository<AdmissionCrmFollowUpTask, Guid> followUpRepository,
		IRepository<AdmissionCrmAdmissionYearSetup, Guid> admissionYearRepository,
		IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository)
	{
		_examRepository = examRepository;
		_registrationRepository = registrationRepository;
		_quotationRepository = quotationRepository;
		_seatOfferRepository = seatOfferRepository;
		_activationRepository = activationRepository;
		_followUpRepository = followUpRepository;
		_admissionYearRepository = admissionYearRepository;
		_cycleRepository = cycleRepository;
	}

	public async Task<IQueryable<AdmissionInquiryLead>> ApplyColumnFilterAsync(
		IQueryable<AdmissionInquiryLead> leads,
		AdmissionCrmPipelineColumnKey column,
		Guid? tenantId)
	{
		var rows = await BuildReportRowsAsync(leads, tenantId);
		var leadIds = rows
			.Where(row => row.Column == column)
			.Select(row => row.LeadId);
		return leads.Where(lead => leadIds.Contains(lead.Id));
	}

	public async Task<IQueryable<AdmissionCrmPipelineReportRow>> BuildReportRowsAsync(
		IQueryable<AdmissionInquiryLead> leads,
		Guid? tenantId)
	{
		var exams = await _examRepository.GetQueryableAsync();
		var registrations = await _registrationRepository.GetQueryableAsync();
		var quotations = await _quotationRepository.GetQueryableAsync();
		var seatOffers = await _seatOfferRepository.GetQueryableAsync();
		var activations = await _activationRepository.GetQueryableAsync();
		var followUps = await _followUpRepository.GetQueryableAsync();
		var admissionYears = await _admissionYearRepository.GetQueryableAsync();
		var cycles = await _cycleRepository.GetQueryableAsync();

		var latestExamIds = exams
			.Where(item => item.TenantId == tenantId)
			.GroupBy(item => item.LeadId)
			.Select(group => group
				.OrderByDescending(item => item.CreationTime)
				.Select(item => item.Id)
				.First());
		var latestRegistrationIds = registrations
			.Where(item => item.TenantId == tenantId)
			.GroupBy(item => item.LeadId)
			.Select(group => group
				.OrderByDescending(item => item.CreationTime)
				.Select(item => item.Id)
				.First());
		var latestQuotationIds = quotations
			.Where(item =>
				item.TenantId == tenantId &&
				item.Status != AdmissionCrmQuotationStatus.Superseded)
			.GroupBy(item => item.LeadId)
			.Select(group => group
				.OrderByDescending(item => item.Version)
				.ThenByDescending(item => item.CreationTime)
				.Select(item => item.Id)
				.First());
		var latestSeatOfferIds = seatOffers
			.Where(item =>
				item.TenantId == tenantId &&
				item.Status != AdmissionCrmSeatOfferStatus.Superseded)
			.GroupBy(item => item.LeadId)
			.Select(group => group
				.OrderByDescending(item => item.Version)
				.ThenByDescending(item => item.CreationTime)
				.Select(item => item.Id)
				.First());
		var latestActivationIds = activations
			.Where(item => item.TenantId == tenantId)
			.GroupBy(item => item.LeadId)
			.Select(group => group
				.OrderByDescending(item => item.CreationTime)
				.Select(item => item.Id)
				.First());

		var latestExams = exams.Where(item => latestExamIds.Contains(item.Id));
		var latestRegistrations = registrations.Where(item => latestRegistrationIds.Contains(item.Id));
		var latestQuotations = quotations.Where(item => latestQuotationIds.Contains(item.Id));
		var latestSeatOffers = seatOffers.Where(item => latestSeatOfferIds.Contains(item.Id));
		var latestActivations = activations.Where(item => latestActivationIds.Contains(item.Id));

		var lifecycleRows =
			from lead in leads
			join exam in latestExams on lead.Id equals exam.LeadId into examGroup
			from exam in examGroup.DefaultIfEmpty()
			join registration in latestRegistrations on lead.Id equals registration.LeadId into registrationGroup
			from registration in registrationGroup.DefaultIfEmpty()
			join quotation in latestQuotations on lead.Id equals quotation.LeadId into quotationGroup
			from quotation in quotationGroup.DefaultIfEmpty()
			join seatOffer in latestSeatOffers on lead.Id equals seatOffer.LeadId into seatOfferGroup
			from seatOffer in seatOfferGroup.DefaultIfEmpty()
			join activation in latestActivations on lead.Id equals activation.LeadId into activationGroup
			from activation in activationGroup.DefaultIfEmpty()
			join admissionYear in admissionYears.Where(item => item.TenantId == tenantId)
				on lead.AdmissionYearSetupId equals (Guid?)admissionYear.Id into admissionYearGroup
			from admissionYear in admissionYearGroup.DefaultIfEmpty()
			join cycle in cycles.Where(item => item.TenantId == tenantId)
				on lead.AcademicYearId equals cycle.AcademicYearId into cycleGroup
			from cycle in cycleGroup.DefaultIfEmpty()
			select new
			{
				Lead = lead,
				Exam = exam,
				Registration = registration,
				Quotation = quotation,
				SeatOffer = seatOffer,
				Activation = activation,
				EntranceExamRequired = admissionYear != null
					? admissionYear.EntranceExamRequired
					: cycle == null || cycle.EntranceExamRequired,
				FinanceRequired = admissionYear != null && admissionYear.FinanceVerificationRequired,
				PlacementTiming = admissionYear != null
					? admissionYear.PlacementTiming
					: AdmissionCrmPlacementTiming.StandardBeforeActivation,
				HasOpenFollowUp = followUps.Any(task =>
					task.TenantId == tenantId &&
					task.LeadId == lead.Id &&
					task.Status == AdmissionCrmTaskStatus.Pending)
			};

		var states = lifecycleRows.Select(row => new
		{
			row.Lead,
			row.Exam,
			row.Registration,
			Closed =
				row.Lead.MergedIntoLeadId.HasValue ||
				row.Lead.Stage == AdmissionInquiryStage.Rejected ||
				row.Lead.Stage == AdmissionInquiryStage.Archived ||
				row.Lead.Stage == AdmissionInquiryStage.Stale ||
				(row.Exam != null &&
				 row.Exam.EligibilityDecision == AdmissionCrmEligibilityDecision.Rejected) ||
				(row.Registration != null &&
				 row.Registration.Status == AdmissionCrmRegistrationStatus.Withdrawn) ||
				(row.Activation != null &&
				 row.Activation.Status == AdmissionCrmActivationStatus.Cancelled),
			ClosedOutcome =
				row.Lead.MergedIntoLeadId.HasValue
					? (AdmissionCrmClosedOutcome?)AdmissionCrmClosedOutcome.Duplicate
					: row.Activation != null &&
					  row.Activation.Status == AdmissionCrmActivationStatus.Cancelled
						? AdmissionCrmClosedOutcome.Cancelled
						: row.Exam != null &&
						  row.Exam.EligibilityDecision == AdmissionCrmEligibilityDecision.Rejected
							? AdmissionCrmClosedOutcome.Ineligible
							: row.Registration != null &&
							  row.Registration.Status == AdmissionCrmRegistrationStatus.Withdrawn
								? AdmissionCrmClosedOutcome.Withdrawn
								: row.Lead.Stage == AdmissionInquiryStage.Rejected
									? AdmissionCrmClosedOutcome.Rejected
									: row.Lead.Stage == AdmissionInquiryStage.Archived
										? AdmissionCrmClosedOutcome.Withdrawn
										: row.Lead.Stage == AdmissionInquiryStage.Stale
											? AdmissionCrmClosedOutcome.NotInterested
											: null,
			ActivationComplete =
				row.Lead.StudentId.HasValue ||
				(row.Activation != null &&
				 row.Activation.Status == AdmissionCrmActivationStatus.FirstDayReady),
			RegistrationExists = row.Registration != null,
			RegistrationComplete =
				row.Registration != null &&
				(row.Registration.Status == AdmissionCrmRegistrationStatus.Verified ||
				 row.Registration.Status == AdmissionCrmRegistrationStatus.AdmissionReady),
			row.FinanceRequired,
			FinanceComplete =
				!row.FinanceRequired ||
				(row.Quotation != null &&
				 (row.Quotation.FinanceState == AdmissionCrmFinanceState.Confirmed ||
				  row.Quotation.FinanceState == AdmissionCrmFinanceState.Waived ||
				  row.Quotation.FinanceState == AdmissionCrmFinanceState.Sponsored)),
			PlacementComplete =
				(row.Activation != null &&
				 row.Activation.StandardId != Guid.Empty &&
				 (row.PlacementTiming == AdmissionCrmPlacementTiming.StandardBeforeActivation ||
				  row.Activation.DivisionId != Guid.Empty)) ||
				(row.SeatOffer != null &&
				 row.SeatOffer.Status == AdmissionCrmSeatOfferStatus.Accepted),
			HasPlacementWork = row.SeatOffer != null || row.Activation != null,
			ExamExists = row.Exam != null,
			ExamEligible =
				row.Exam != null &&
				row.Exam.EligibilityDecision == AdmissionCrmEligibilityDecision.Eligible,
			ExamDone =
				row.Exam != null &&
				(row.Exam.ResultRecordedAt.HasValue ||
				 row.Exam.EligibilityDecision == AdmissionCrmEligibilityDecision.Waitlisted ||
				 row.Exam.EligibilityDecision == AdmissionCrmEligibilityDecision.Rejected),
			row.EntranceExamRequired,
			row.HasOpenFollowUp
		});

		return states.Select(state => new AdmissionCrmPipelineReportRow
		{
			LeadId = state.Lead.Id,
			TargetStandardId = state.Lead.TargetStandardId,
			InquirySourceName = state.Lead.InquirySourceSnapshot,
			ClosedOutcome = state.ClosedOutcome,
			ExamScheduled = state.ExamExists,
			ExamCompleted = state.ExamDone,
			Eligible = state.ExamEligible,
			RegistrationVerified = state.RegistrationComplete,
			Column = state.Closed
				? null
				: state.ActivationComplete
					? AdmissionCrmPipelineColumnKey.Activated
					: state.RegistrationComplete &&
					  state.FinanceComplete &&
					  state.PlacementComplete
						? AdmissionCrmPipelineColumnKey.ReadyForActivation
						: state.RegistrationComplete &&
						  state.FinanceComplete &&
						  !state.PlacementComplete &&
						  (state.FinanceRequired || state.HasPlacementWork)
							? AdmissionCrmPipelineColumnKey.PlacementPending
							: state.RegistrationComplete &&
							  state.FinanceRequired &&
							  !state.FinanceComplete
								? AdmissionCrmPipelineColumnKey.FinanceVerification
								: state.RegistrationComplete &&
								  !state.FinanceRequired &&
								  !state.HasPlacementWork
									? AdmissionCrmPipelineColumnKey.RegistrationDone
									: !state.RegistrationComplete &&
									  (state.RegistrationExists ||
									   (!state.EntranceExamRequired &&
										!state.ExamExists &&
										!state.HasOpenFollowUp &&
										state.Lead.Stage != AdmissionInquiryStage.NewInquiry &&
										state.Lead.Stage != AdmissionInquiryStage.FollowUp) ||
									   (state.EntranceExamRequired &&
										!state.ExamExists &&
										state.Lead.Stage == AdmissionInquiryStage.Eligible))
										? AdmissionCrmPipelineColumnKey.RegistrationPending
										: !state.RegistrationExists && state.ExamEligible
											? AdmissionCrmPipelineColumnKey.Eligible
											: !state.RegistrationExists &&
											  !state.ExamEligible &&
											  state.ExamDone
												? AdmissionCrmPipelineColumnKey.ExamDone
												: !state.RegistrationExists &&
												  !state.ExamDone &&
												  !state.ExamEligible &&
												  (state.ExamExists ||
												   state.Lead.Stage == AdmissionInquiryStage.ExamReady ||
												   state.Lead.Stage == AdmissionInquiryStage.ExamScheduled)
													? AdmissionCrmPipelineColumnKey.ExamPending
													: !state.RegistrationExists &&
													  !state.ExamExists &&
													  (state.HasOpenFollowUp ||
													   state.Lead.Stage == AdmissionInquiryStage.FollowUp)
														? AdmissionCrmPipelineColumnKey.FollowUp
														: AdmissionCrmPipelineColumnKey.NewInquiry
		});
	}
}
