using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmApplicantProgressReadService : ITransientDependency
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private readonly IRepository<AdmissionCrmEntranceExamRecord, Guid> _examRepository;
	private readonly IRepository<AdmissionCrmRegistrationApplication, Guid> _registrationRepository;
	private readonly IRepository<AdmissionCrmAdmissionQuotation, Guid> _quotationRepository;
	private readonly IRepository<AdmissionCrmSeatOffer, Guid> _seatOfferRepository;
	private readonly IRepository<AdmissionCrmActivation, Guid> _activationRepository;
	private readonly IRepository<AdmissionCrmFollowUpTask, Guid> _followUpRepository;
	private readonly IRepository<AdmissionCrmCycleConfiguration, Guid> _cycleRepository;
	private readonly AdmissionCrmApplicantProgressProjector _projector;

	public AdmissionCrmApplicantProgressReadService(
		IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
		IRepository<AdmissionCrmRegistrationApplication, Guid> registrationRepository,
		IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
		IRepository<AdmissionCrmSeatOffer, Guid> seatOfferRepository,
		IRepository<AdmissionCrmActivation, Guid> activationRepository,
		IRepository<AdmissionCrmFollowUpTask, Guid> followUpRepository,
		IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
		AdmissionCrmApplicantProgressProjector projector)
	{
		_examRepository = examRepository;
		_registrationRepository = registrationRepository;
		_quotationRepository = quotationRepository;
		_seatOfferRepository = seatOfferRepository;
		_activationRepository = activationRepository;
		_followUpRepository = followUpRepository;
		_cycleRepository = cycleRepository;
		_projector = projector;
	}

	public async Task<IReadOnlyDictionary<Guid, AdmissionCrmApplicantProgressDto>> GetForLeadsAsync(
		IReadOnlyCollection<AdmissionInquiryLead> leads)
	{
		if (leads.Count == 0)
		{
			return new Dictionary<Guid, AdmissionCrmApplicantProgressDto>();
		}

		var tenantId = leads.First().TenantId;
		if (leads.Any(lead => lead.TenantId != tenantId))
		{
			throw new BusinessException("AdmissionCrm:CrossTenantProgressRead");
		}

		var leadIds = leads.Select(lead => lead.Id).Distinct().ToList();
		var academicYearIds = leads.Select(lead => lead.AcademicYearId).Distinct().ToList();
		var exams = await _examRepository.GetListAsync(item =>
			item.TenantId == tenantId && leadIds.Contains(item.LeadId));
		var registrations = await _registrationRepository.GetListAsync(item =>
			item.TenantId == tenantId && leadIds.Contains(item.LeadId));
		var quotations = await _quotationRepository.GetListAsync(item =>
			item.TenantId == tenantId &&
			leadIds.Contains(item.LeadId) &&
			item.Status != AdmissionCrmQuotationStatus.Superseded);
		var seatOffers = await _seatOfferRepository.GetListAsync(item =>
			item.TenantId == tenantId &&
			leadIds.Contains(item.LeadId) &&
			item.Status != AdmissionCrmSeatOfferStatus.Superseded);
		var activations = await _activationRepository.GetListAsync(item =>
			item.TenantId == tenantId && leadIds.Contains(item.LeadId));
		var openFollowUps = await _followUpRepository.GetListAsync(item =>
			item.TenantId == tenantId &&
			leadIds.Contains(item.LeadId) &&
			item.Status == AdmissionCrmTaskStatus.Pending);
		var cycleConfigurations = await _cycleRepository.GetListAsync(item =>
			item.TenantId == tenantId && academicYearIds.Contains(item.AcademicYearId));

		var latestExamByLead = LatestByLead(exams, item => item.CreationTime);
		var latestRegistrationByLead = LatestByLead(registrations, item => item.CreationTime);
		var latestQuotationByLead = quotations
			.GroupBy(item => item.LeadId)
			.ToDictionary(
				group => group.Key,
				group => group
					.OrderByDescending(item => item.Version)
					.ThenByDescending(item => item.CreationTime)
					.First());
		var latestSeatOfferByLead = seatOffers
			.GroupBy(item => item.LeadId)
			.ToDictionary(
				group => group.Key,
				group => group
					.OrderByDescending(item => item.Version)
					.ThenByDescending(item => item.CreationTime)
					.First());
		var latestActivationByLead = LatestByLead(activations, item => item.CreationTime);
		var leadsWithOpenFollowUps = openFollowUps.Select(item => item.LeadId).ToHashSet();
		var cycleByAcademicYear = cycleConfigurations
			.GroupBy(item => item.AcademicYearId)
			.ToDictionary(group => group.Key, group => group.First());

		return leads.ToDictionary(
			lead => lead.Id,
			lead => _projector.Project(
				lead,
				ResolveRules(
					lead,
					cycleByAcademicYear.GetValueOrDefault(lead.AcademicYearId)),
				latestExamByLead.GetValueOrDefault(lead.Id),
				latestRegistrationByLead.GetValueOrDefault(lead.Id),
				latestQuotationByLead.GetValueOrDefault(lead.Id),
				latestSeatOfferByLead.GetValueOrDefault(lead.Id),
				latestActivationByLead.GetValueOrDefault(lead.Id),
				leadsWithOpenFollowUps.Contains(lead.Id)));
	}

	private static Dictionary<Guid, T> LatestByLead<T>(
		IEnumerable<T> items,
		Func<T, DateTime> creationTime)
		where T : class
	{
		return items
			.GroupBy(item => GetLeadId(item))
			.ToDictionary(
				group => group.Key,
				group => group.OrderByDescending(creationTime).First());
	}

	private static Guid GetLeadId<T>(T item) => item switch
	{
		AdmissionCrmEntranceExamRecord exam => exam.LeadId,
		AdmissionCrmRegistrationApplication registration => registration.LeadId,
		AdmissionCrmActivation activation => activation.LeadId,
		_ => throw new InvalidOperationException($"Unsupported applicant progress record {typeof(T).Name}.")
	};

	private static AdmissionCrmApplicantProgressRules ResolveRules(
		AdmissionInquiryLead lead,
		AdmissionCrmCycleConfiguration? configuration)
	{
		if (!string.IsNullOrWhiteSpace(lead.AdmissionYearSetupSnapshotJson))
		{
			try
			{
				var snapshot = JsonSerializer.Deserialize<AdmissionYearRulesSnapshot>(
					lead.AdmissionYearSetupSnapshotJson,
					JsonOptions);
				if (snapshot is not null)
				{
					return new AdmissionCrmApplicantProgressRules(
						snapshot.EntranceExamRequired,
						snapshot.FinanceVerificationRequired,
						snapshot.PlacementTiming);
				}
			}
			catch (JsonException exception)
			{
				throw new BusinessException("AdmissionCrm:AdmissionYearSnapshotInvalid")
					.WithData("LeadId", lead.Id)
					.WithData("Error", exception.Message);
			}
		}

		return new AdmissionCrmApplicantProgressRules(
			configuration?.EntranceExamRequired ?? true,
			false,
			AdmissionCrmPlacementTiming.StandardBeforeActivation);
	}

	private sealed class AdmissionYearRulesSnapshot
	{
		public bool EntranceExamRequired { get; set; } = true;
		public bool FinanceVerificationRequired { get; set; }
		public AdmissionCrmPlacementTiming PlacementTiming { get; set; } =
			AdmissionCrmPlacementTiming.StandardBeforeActivation;
	}
}
