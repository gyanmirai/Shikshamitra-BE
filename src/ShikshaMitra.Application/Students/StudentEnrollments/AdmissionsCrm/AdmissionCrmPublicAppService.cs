using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Encryption;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[AllowAnonymous]
[RemoteService(false)]
public class AdmissionCrmPublicAppService : ApplicationService, IAdmissionCrmPublicAppService
{
	private const int StatusTokenExpiryDays = 90;
	private const int OtpExpiryMinutes = 10;
	private const int OtpResendCooldownSeconds = 60;
	private const int AccessSessionMinutes = 30;
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly IReadOnlyDictionary<string, (string Gujarati, string Hindi)> ApplicantTranslations =
		new Dictionary<string, (string, string)>
		{
			["Completed"] = ("પૂર્ણ", "पूर्ण"), ["Current"] = ("ચાલુ", "वर्तमान"), ["Pending"] = ("બાકી", "लंबित"),
			["Inquiry received"] = ("પૂછપરછ પ્રાપ્ત થઈ", "पूछताछ प्राप्त हुई"),
			["Admissions follow-up"] = ("પ્રવેશ અનુસરણ", "प्रवेश अनुवर्ती कार्रवाई"),
			["Entrance exam schedule"] = ("પ્રવેશ પરીક્ષા સમયપત્રક", "प्रवेश परीक्षा समय-सारणी"),
			["Entrance exam result"] = ("પ્રવેશ પરીક્ષા પરિણામ", "प्रवेश परीक्षा परिणाम"),
			["Eligibility decision"] = ("પાત્રતા નિર્ણય", "पात्रता निर्णय"),
			["New Inquiry"] = ("નવી પૂછપરછ", "नई पूछताछ"), ["Follow-up"] = ("અનુસરણ", "अनुवर्ती कार्रवाई"),
			["Exam Ready"] = ("પરીક્ષા માટે તૈયાર", "परीक्षा के लिए तैयार"), ["Exam Scheduled"] = ("પરીક્ષા નિર્ધારિત", "परीक्षा निर्धारित"),
			["Exam Completed"] = ("પરીક્ષા પૂર્ણ", "परीक्षा पूर्ण"), ["Eligible"] = ("પાત્ર", "पात्र"),
			["Waitlisted"] = ("પ્રતીક્ષા સૂચિ", "प्रतीक्षा सूची"), ["Rejected"] = ("નામંજૂર", "अस्वीकृत"),
			["Stale"] = ("અનુસરણ બાકી", "अनुवर्ती लंबित"), ["Archived"] = ("આર્કાઇવ", "संग्रहीत"),
			["The admissions team will review the inquiry and follow up."] = ("પ્રવેશ ટીમ પૂછપરછની સમીક્ષા કરીને સંપર્ક કરશે.", "प्रवेश टीम पूछताछ की समीक्षा करके संपर्क करेगी।"),
			["The admissions team is following up on the inquiry."] = ("પ્રવેશ ટીમ પૂછપરછ પર અનુસરણ કરી રહી છે.", "प्रवेश टीम पूछताछ पर अनुवर्ती कार्रवाई कर रही है।"),
			["The inquiry is ready for entrance exam scheduling."] = ("પૂછપરછ પ્રવેશ પરીક્ષા નક્કી કરવા તૈયાર છે.", "पूछताछ प्रवेश परीक्षा निर्धारित करने के लिए तैयार है।"),
			["The entrance exam has been scheduled."] = ("પ્રવેશ પરીક્ષા નક્કી થઈ ગઈ છે.", "प्रवेश परीक्षा निर्धारित हो गई है।"),
			["The entrance exam result is being reviewed."] = ("પ્રવેશ પરીક્ષાના પરિણામની સમીક્ષા થઈ રહી છે.", "प्रवेश परीक्षा परिणाम की समीक्षा हो रही है।"),
			["The applicant is eligible for admission handoff."] = ("અરજદાર પ્રવેશ હેન્ડઓફ માટે પાત્ર છે.", "आवेदक प्रवेश हस्तांतरण के लिए पात्र है।"),
			["The applicant is currently waitlisted."] = ("અરજદાર હાલમાં પ્રતીક્ષા સૂચિમાં છે.", "आवेदक वर्तमान में प्रतीक्षा सूची में है।"),
			["The applicant is not eligible at this stage."] = ("અરજદાર આ તબક્કે પાત્ર નથી.", "आवेदक इस चरण में पात्र नहीं है।"),
			["The inquiry needs renewed follow-up."] = ("પૂછપરછ માટે ફરી અનુસરણ જરૂરી છે.", "पूछताछ के लिए दोबारा अनुवर्ती कार्रवाई आवश्यक है।"),
			["The inquiry is archived."] = ("પૂછપરછ આર્કાઇવ કરવામાં આવી છે.", "पूछताछ संग्रहीत की गई है।"),
			["The admissions team will share the next step."] = ("પ્રવેશ ટીમ આગળનું પગલું જણાવશે.", "प्रवेश टीम अगला चरण बताएगी।"),
			["Inquiry submitted"] = ("પૂછપરછ મોકલાઈ", "पूछताछ भेजी गई"),
			["Entrance exam scheduled"] = ("પ્રવેશ પરીક્ષા નિર્ધારિત", "प्रवेश परीक्षा निर्धारित"),
			["Entrance exam attendance updated"] = ("પ્રવેશ પરીક્ષા હાજરી સુધારાઈ", "प्रवेश परीक्षा उपस्थिति अपडेट हुई"),
			["Entrance exam result updated"] = ("પ્રવેશ પરીક્ષા પરિણામ સુધારાયું", "प्रवेश परीक्षा परिणाम अपडेट हुआ"),
			["Applicant checklist updated"] = ("અરજદાર ચેકલિસ્ટ સુધારાયું", "आवेदक चेकलिस्ट अपडेट हुई"),
			["Admission offer issued"] = ("પ્રવેશ ઓફર જારી", "प्रवेश प्रस्ताव जारी"),
			["Admission offer accepted"] = ("પ્રવેશ ઓફર સ્વીકારાઈ", "प्रवेश प्रस्ताव स्वीकार"),
			["Admission offer declined"] = ("પ્રવેશ ઓફર નકારી", "प्रवेश प्रस्ताव अस्वीकार"),
			["Student activation in progress"] = ("વિદ્યાર્થી સક્રિયકરણ ચાલુ", "छात्र सक्रियण जारी"),
			["Student ready to join"] = ("વિદ્યાર્થી જોડાવા તૈયાર", "छात्र प्रवेश के लिए तैयार")
		};

	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionInquiryFormTemplate, Guid> _formTemplateRepository;
	private readonly IRepository<AdmissionInquiryFormSubmission, Guid> _submissionRepository;
	private readonly IRepository<AdmissionInquiryConsentRecord, Guid> _consentRepository;
	private readonly IRepository<AdmissionApplicantStatusAccess, Guid> _statusAccessRepository;
	private readonly IRepository<AdmissionCrmPipelineStage, Guid> _stageRepository;
	private readonly IRepository<AdmissionCrmCycleConfiguration, Guid> _cycleRepository;
	private readonly IRepository<AdmissionCrmTimelineEvent, Guid> _timelineRepository;
	private readonly IRepository<AdmissionCrmEntranceExamRecord, Guid> _examRepository;
	private readonly IRepository<AdmissionCrmCommunicationProviderConfiguration, Guid> _providerRepository;
	private readonly IRepository<AdmissionCrmCommunicationOptOut, Guid> _optOutRepository;
	private readonly IRepository<AdmissionCrmMessageDeliveryLog, Guid> _deliveryLogRepository;
	private readonly IRepository<AdmissionCrmApplicantChecklistProgress, Guid> _applicantChecklistProgressRepository;
	private readonly IRepository<AdmissionCrmRegistrationApplication, Guid> _registrationApplicationRepository;
	private readonly IRepository<AdmissionCrmPlaybook, Guid> _playbookRepository;
	private readonly IRepository<AdmissionCrmSeatOffer, Guid> _seatOfferRepository;
	private readonly IRepository<AdmissionCrmSeatReservation, Guid> _seatReservationRepository;
	private readonly IRepository<AdmissionCrmSeatPool, Guid> _seatPoolRepository;
	private readonly IRepository<AdmissionCrmWaitlistEntry, Guid> _waitlistRepository;
	private readonly IRepository<AdmissionCrmAdmissionQuotation, Guid> _quotationRepository;
	private readonly IRepository<AdmissionCrmRouteAssignment, Guid> _routeAssignmentRepository;
	private readonly IRepository<AdmissionCrmActivation, Guid> _activationRepository;
	private readonly IRepository<AdmissionCrmExamSlot, Guid> _slotRepository;
	private readonly IRepository<AdmissionCrmInquirySource, Guid> _inquirySourceRepository;
	private readonly IRepository<AdmissionCrmInquiryType, Guid> _inquiryTypeRepository;
	private readonly IRepository<AdmissionCrmAdmissionYearSetup, Guid> _admissionYearRepository;
	private readonly IAdmissionCrmCommunicationDispatcher _dispatcher;
	private readonly IAdmissionCrmLeadAppService _leadAppService;
	private readonly IAdmissionCrmDocumentAppService _documentAppService;
	private readonly IAdmissionCrmEntranceExamAppService _entranceExamAppService;
	private readonly IStringEncryptionService _stringEncryptionService;
	private readonly AdmissionCrmStaffEligibilityService _staffEligibilityService;

	public AdmissionCrmPublicAppService(
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionInquiryFormTemplate, Guid> formTemplateRepository,
		IRepository<AdmissionInquiryFormSubmission, Guid> submissionRepository,
		IRepository<AdmissionInquiryConsentRecord, Guid> consentRepository,
		IRepository<AdmissionApplicantStatusAccess, Guid> statusAccessRepository,
		IRepository<AdmissionCrmPipelineStage, Guid> stageRepository,
		IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
		IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
		IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
		IRepository<AdmissionCrmCommunicationProviderConfiguration, Guid> providerRepository,
		IRepository<AdmissionCrmCommunicationOptOut, Guid> optOutRepository,
		IRepository<AdmissionCrmMessageDeliveryLog, Guid> deliveryLogRepository,
		IRepository<AdmissionCrmApplicantChecklistProgress, Guid> applicantChecklistProgressRepository,
		IRepository<AdmissionCrmRegistrationApplication, Guid> registrationApplicationRepository,
		IRepository<AdmissionCrmPlaybook, Guid> playbookRepository,
		IRepository<AdmissionCrmSeatOffer, Guid> seatOfferRepository,
		IRepository<AdmissionCrmSeatReservation, Guid> seatReservationRepository,
		IRepository<AdmissionCrmSeatPool, Guid> seatPoolRepository,
		IRepository<AdmissionCrmWaitlistEntry, Guid> waitlistRepository,
		IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
		IRepository<AdmissionCrmRouteAssignment, Guid> routeAssignmentRepository,
		IRepository<AdmissionCrmActivation, Guid> activationRepository,
		IRepository<AdmissionCrmExamSlot, Guid> slotRepository,
		IRepository<AdmissionCrmInquirySource, Guid> inquirySourceRepository,
		IRepository<AdmissionCrmInquiryType, Guid> inquiryTypeRepository,
		IRepository<AdmissionCrmAdmissionYearSetup, Guid> admissionYearRepository,
		IAdmissionCrmCommunicationDispatcher dispatcher,
		IAdmissionCrmLeadAppService leadAppService,
		IAdmissionCrmDocumentAppService documentAppService,
		IAdmissionCrmEntranceExamAppService entranceExamAppService,
		IStringEncryptionService stringEncryptionService,
		AdmissionCrmStaffEligibilityService staffEligibilityService)
	{
		_leadRepository = leadRepository;
		_formTemplateRepository = formTemplateRepository;
		_submissionRepository = submissionRepository;
		_consentRepository = consentRepository;
		_statusAccessRepository = statusAccessRepository;
		_stageRepository = stageRepository;
		_cycleRepository = cycleRepository;
		_timelineRepository = timelineRepository;
		_examRepository = examRepository;
		_providerRepository = providerRepository;
		_optOutRepository = optOutRepository;
		_deliveryLogRepository = deliveryLogRepository;
		_applicantChecklistProgressRepository = applicantChecklistProgressRepository;
		_registrationApplicationRepository = registrationApplicationRepository;
		_playbookRepository = playbookRepository;
		_seatOfferRepository = seatOfferRepository;
		_seatReservationRepository = seatReservationRepository;
		_seatPoolRepository = seatPoolRepository;
		_waitlistRepository = waitlistRepository;
		_quotationRepository = quotationRepository;
		_routeAssignmentRepository = routeAssignmentRepository;
		_activationRepository = activationRepository;
		_slotRepository = slotRepository;
		_inquirySourceRepository = inquirySourceRepository;
		_inquiryTypeRepository = inquiryTypeRepository;
		_admissionYearRepository = admissionYearRepository;
		_dispatcher = dispatcher;
		_leadAppService = leadAppService;
		_documentAppService = documentAppService;
		_entranceExamAppService = entranceExamAppService;
		_stringEncryptionService = stringEncryptionService;
		_staffEligibilityService = staffEligibilityService;
	}

	public async Task<PublicAdmissionInquiryFormDto> GetPublicFormAsync(Guid tenantId, Guid templateId, string? language = null)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var template = await GetActivePublicTemplateAsync(templateId);
			await EnsurePublicFormChannelEnabledAsync(template);
			return ToPublicFormDto(tenantId, template, language);
		}
	}

	public async Task<PublicAdmissionInquirySubmitResultDto> SubmitPublicFormAsync(
		Guid tenantId,
		Guid templateId,
		PublicAdmissionInquirySubmitDto input,
		string? ipAddress,
		string? userAgent)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var template = await GetActivePublicTemplateAsync(templateId);
			var configuration = await GetCycleConfigurationOrDefaultAsync(template.AcademicYearId);
			var channelSetting = AdmissionCrmChannelSettingsSerializer.GetChannelSetting(
				configuration.ChannelSettingsJson,
				template.Channel);
			if (!channelSetting.IsEnabled)
			{
				throw new BusinessException("AdmissionCrm:ChannelDisabled")
					.WithData("Channel", template.Channel);
			}

			var fields = DeserializeFields(template.FieldsJson);
			ValidatePublicSubmission(template, fields, input);

			var mapped = MapSystemFields(fields, input.FieldValues);
			var admissionYearSetup = await GetAdmissionYearSetupForCaptureAsync(template.AcademicYearId);
			var targetStandardId = ParseGuid(GetValue(mapped, "TargetStandardId"));
			var intendedRouteType = ResolveDefaultPublicRoute(admissionYearSetup);
			if (admissionYearSetup is not null)
			{
				EnsurePublishedAdmissionYearCaptureFields(mapped);
				ValidateAdmissionYearScope(
					admissionYearSetup,
					targetStandardId,
					intendedRouteType);
			}

			var routingSettings = AdmissionCrmLeadRoutingRuleMatcher.DeserializeSettings(
				configuration.LeadRoutingRulesJson);
			var trackedOwnerUserId = ResolvePublicTrackedOwnerUserId(
				template.Channel,
				mapped,
				channelSetting,
				routingSettings);
			var ownerUserId = trackedOwnerUserId ?? channelSetting.DefaultOwnerUserId;
			var now = Clock.Now;
			var inquirySource = await ResolvePublicInquirySourceAsync(
				template.AcademicYearId,
				template.Channel,
				channelSetting.DefaultInquirySourceId,
				admissionYearSetup is not null);
			var inquiryType = await ResolveDefaultInquiryTypeAsync(
				template.AcademicYearId,
				admissionYearSetup is not null);
			var lead = new AdmissionInquiryLead(
				GuidGenerator.Create(),
				tenantId,
				template.AcademicYearId,
				inquirySource?.Id,
				inquirySource?.DisplayName,
				template.Channel,
				GetValue(mapped, "CampaignName"),
				GetValue(mapped, "CampusBranchName"),
				GetValue(mapped, "PreferredCommunicationLanguage"),
				GetValue(mapped, "FirstName"),
				GetValue(mapped, "MiddleName"),
				GetValue(mapped, "LastName"),
				ParseDateTimeOffset(GetValue(mapped, "DateOfBirth")),
				GetValue(mapped, "ParentGuardianName"),
				GetValue(mapped, "PrimaryContactNo"),
				GetValue(mapped, "Email"),
				GetRequiredValue(mapped, "AdmissionIntent", "AdmissionCrm:PublicSubmissionIntentRequired"),
				targetStandardId,
				GetValue(mapped, "DesiredClassName"),
				notes: null,
				now,
				intendedRouteType,
				intendedRouteDefinitionId: null,
				intendedRouteDefinitionVersion: null,
				intendedRouteNameSnapshot: intendedRouteType.HasValue
					? GetAdmissionRouteLabel(intendedRouteType.Value)
					: null);
			lead.ApplyInquiryType(inquiryType?.Id, inquiryType?.DisplayName);
			if (admissionYearSetup is not null)
			{
				lead.ApplyAdmissionYearSetup(
					admissionYearSetup.Id,
					admissionYearSetup.Version,
					admissionYearSetup.PublishedSnapshotJson!);
			}
			else
			{
				lead.ApplyAdmissionYearSetup(
					null,
					0,
					BuildLegacyAdmissionYearSnapshot(configuration));
			}
			lead.UpdateScore(AdmissionCrmLeadScoreCalculator.Calculate(
				lead,
				AdmissionCrmLeadScoreCalculator.DeserializeRules(configuration.LeadScoringRulesJson)));
			if (ownerUserId.HasValue &&
				await _staffEligibilityService.IsOwnerEligibleAsync(ownerUserId.Value, template.AcademicYearId))
			{
				lead.AssignOwner(ownerUserId.Value);
			}

			var duplicateCount = await CountLikelyDuplicatesForPublicSubmissionAsync(lead);
			if (duplicateCount > 0)
			{
				lead.RecordDuplicateContinue(
					"Public form submitted with likely duplicate details. Review before follow-up.");
			}

			await _leadRepository.InsertAsync(lead, autoSave: false);

			var submittedFieldsJson = JsonSerializer.Serialize(input.FieldValues, JsonOptions);
			await _submissionRepository.InsertAsync(
				new AdmissionInquiryFormSubmission(
					GuidGenerator.Create(),
					tenantId,
					lead.Id,
					template.Id,
					template.TemplateName,
					submittedFieldsJson,
					now,
					ipAddress,
					userAgent),
				autoSave: false);

			await _consentRepository.InsertAsync(
				new AdmissionInquiryConsentRecord(
					GuidGenerator.Create(),
					tenantId,
					lead.Id,
					template.Id,
					template.TemplateName,
					template.ConsentText!,
					template.PrivacyNoticeUrl!,
					template.ConcurrencyStamp,
					SerializeCommunicationPreferences(input.CommunicationPreferences),
					now,
					ipAddress,
					userAgent),
				autoSave: false);

			var statusToken = CreateRawToken();
			var statusAccess = new AdmissionApplicantStatusAccess(
				GuidGenerator.Create(),
				tenantId,
				lead.Id,
				HashSecret(statusToken),
				EncryptRequired(statusToken),
				now.AddDays(StatusTokenExpiryDays),
				now);
			await _timelineRepository.InsertAsync(
				new AdmissionCrmTimelineEvent(
					GuidGenerator.Create(),
					tenantId,
					lead.Id,
					lead.AcademicYearId,
					AdmissionCrmTimelineEventType.PublicFormSubmitted,
					"Public form submitted",
					template.TemplateName,
					template.Id,
					actorUserId: null,
					occurredAt: new DateTimeOffset(now)),
				autoSave: false);
			if (ownerUserId.HasValue)
			{
				await _timelineRepository.InsertAsync(
					new AdmissionCrmTimelineEvent(
						GuidGenerator.Create(),
						tenantId,
						lead.Id,
						lead.AcademicYearId,
						AdmissionCrmTimelineEventType.OwnerAssigned,
						trackedOwnerUserId.HasValue
							? "Owner assigned from staff-shared link"
							: "Owner assigned automatically",
						$"Owner user: {ownerUserId}",
						ownerUserId,
						actorUserId: null,
						occurredAt: new DateTimeOffset(now)),
					autoSave: false);
			}
			if (duplicateCount > 0)
			{
				await _timelineRepository.InsertAsync(
					new AdmissionCrmTimelineEvent(
						GuidGenerator.Create(),
						tenantId,
						lead.Id,
						lead.AcademicYearId,
						AdmissionCrmTimelineEventType.DuplicateContinued,
						"Possible duplicate detected",
						"Public form matched an existing lead by contact, email, or applicant profile. No duplicate details were exposed to the applicant.",
						relatedEntityId: null,
						actorUserId: null,
						occurredAt: new DateTimeOffset(now)),
					autoSave: false);
			}

			await _statusAccessRepository.InsertAsync(statusAccess, autoSave: true);
			var statusPath = CreateStatusPath(tenantId, statusToken);
			await _leadAppService.TrySendAcknowledgementFromAutomationAsync(
				lead.Id,
				channelSetting.DefaultAcknowledgementTemplateId,
				new Dictionary<string, string>
				{
					["statusLink"] = statusPath
				});
			await _leadAppService.RunJourneyRulesFromAutomationAsync(
				lead.AcademicYearId,
				new RunAdmissionCrmJourneyRulesDto
				{
					Trigger = AdmissionCrmJourneyTrigger.LeadCreated,
					LeadId = lead.Id
				});

			return new PublicAdmissionInquirySubmitResultDto
			{
				LeadId = lead.Id,
				AcknowledgementMessage = "Inquiry submitted successfully.",
				StatusToken = statusToken,
				StatusPath = statusPath,
				Stage = lead.Stage
			};
		}
	}

	public async Task<AdmissionApplicantStatusOtpChallengeDto> IssueStatusOtpAsync(Guid tenantId, string token)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var (access, lead) = await GetStatusAccessAndLeadAsync(token);
			var now = Clock.Now;
			access.EnsureCanIssueOtp(now, TimeSpan.FromSeconds(OtpResendCooldownSeconds));
			var otpCode = CreateOtpCode();
			access.IssueOtp(
				HashSecret($"{token.Trim()}:{otpCode}"),
				EncryptRequired(otpCode),
				now,
				now.AddMinutes(OtpExpiryMinutes));

			var delivery = await DispatchStatusOtpAsync(lead, otpCode, now);
			await _statusAccessRepository.UpdateAsync(access, autoSave: true);

			return new AdmissionApplicantStatusOtpChallengeDto
			{
				MaskedDestination = MaskDestination(lead),
				OtpExpiresAt = access.OtpExpiresAt!.Value,
				DeliveryConfigured = delivery.DeliveryConfigured,
				DeliveryStatus = delivery.DeliveryStatus
			};
		}
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionApplicantStatusDto> VerifyStatusOtpAsync(
		Guid tenantId,
		string token,
		VerifyAdmissionApplicantStatusOtpDto input)
	{
		using (CurrentTenant.Change(tenantId))
		using (var uow = UnitOfWorkManager.Begin(
			       new AbpUnitOfWorkOptions { IsTransactional = true },
			       requiresNew: true))
		{
			var (access, lead) = await GetStatusAccessAndLeadAsync(token);
			var now = Clock.Now;
			access.EnsureCanVerify(now);

			var providedHash = HashSecret($"{token.Trim()}:{input.OtpCode.Trim()}");
			if (!string.Equals(access.OtpCodeHash, providedHash, StringComparison.Ordinal))
			{
				access.RecordOtpFailure(now);
				await _statusAccessRepository.UpdateAsync(access, autoSave: true);
				await uow.CompleteAsync();
				throw new BusinessException("AdmissionCrm:InvalidStatusOtp");
			}

			access.MarkOtpVerified(now);
			var accessSessionToken = CreateRawToken();
			var accessSessionExpiresAt = now.AddMinutes(AccessSessionMinutes);
			access.IssueAccessSession(HashSecret(accessSessionToken), accessSessionExpiresAt, now);
			await _statusAccessRepository.UpdateAsync(access, autoSave: true);
			var status = await ToStatusDtoAsync(lead, input.Language);
			status.AccessSessionToken = accessSessionToken;
			status.AccessSessionExpiresAt = accessSessionExpiresAt;
			await uow.CompleteAsync();
			return status;
		}
	}

	public async Task<AdmissionCrmRegistrationApplicationDto> SaveRegistrationAsync(
		Guid tenantId,
		string token,
		SavePublicAdmissionCrmRegistrationDto input)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var (access, lead) = await GetStatusAccessAndLeadAsync(token);
			access.EnsureAccessSession(HashSecret(input.AccessSessionToken), Clock.Now);
			var application = await GetRegistrationApplicationAsync(lead.Id);
			var template = DeserializeRegistrationTemplate(application.TemplateSnapshotJson);
			if (!template.AllowPartialSave)
			{
				throw new BusinessException("AdmissionCrm:RegistrationPartialSaveDisabled");
			}
			var completion = AdmissionCrmRegistrationResponseValidator.Validate(
				application,
				template,
				input.Responses,
				requireComplete: false);
			application.Save(JsonSerializer.Serialize(input.Responses, JsonOptions), completion, Clock.Now);
			await _registrationApplicationRepository.UpdateAsync(application, autoSave: true);
			await AddRegistrationTimelineAsync(lead, application.Id, AdmissionCrmTimelineEventType.RegistrationSaved,
				"Registration progress saved", $"Completion: {completion:0.##}%.");
			return ToPublicRegistrationDto(application);
		}
	}

	public async Task<AdmissionCrmRegistrationApplicationDto> SubmitRegistrationAsync(
		Guid tenantId,
		string token,
		SavePublicAdmissionCrmRegistrationDto input)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var (access, lead) = await GetStatusAccessAndLeadAsync(token);
			access.EnsureAccessSession(HashSecret(input.AccessSessionToken), Clock.Now);
			var application = await GetRegistrationApplicationAsync(lead.Id);
			var template = DeserializeRegistrationTemplate(application.TemplateSnapshotJson);
			AdmissionCrmRegistrationResponseValidator.Validate(
				application,
				template,
				input.Responses,
				requireComplete: true);
			var isResubmission = application.Status == AdmissionCrmRegistrationStatus.ReturnedForCorrection;
			application.Submit(JsonSerializer.Serialize(input.Responses, JsonOptions), Clock.Now, isResubmission);
			await _registrationApplicationRepository.UpdateAsync(application, autoSave: true);
			await AddRegistrationTimelineAsync(lead, application.Id, AdmissionCrmTimelineEventType.RegistrationSubmitted,
				isResubmission ? "Registration corrections submitted" : "Registration submitted", null);
			return ToPublicRegistrationDto(application);
		}
	}

	public async Task<AdmissionCrmApplicantDocumentChecklistDto> UploadRegistrationDocumentAsync(
		Guid tenantId,
		string token,
		string accessSessionToken,
		ProcessedAdmissionCrmApplicantDocumentUploadDto input)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var (access, lead) = await GetStatusAccessAndLeadAsync(token);
			access.EnsureAccessSession(HashSecret(accessSessionToken), Clock.Now);
			return await _documentAppService.UploadFromApplicantAsync(lead.Id, input);
		}
	}

	public async Task<AdmissionCrmPublicExamSlotListDto> GetAvailableExamSlotsAsync(
		Guid tenantId,
		string token,
		string accessSessionToken)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var (access, lead) = await GetStatusAccessAndLeadAsync(token);
			access.EnsureAccessSession(HashSecret(accessSessionToken), Clock.Now);
			if (!await IsApplicantSelfSchedulingEnabledAsync(lead))
			{
				return new AdmissionCrmPublicExamSlotListDto { SelfSchedulingEnabled = false };
			}

			var slotQueryable = await _slotRepository.GetQueryableAsync();
			var slots = await AsyncExecuter.ToListAsync(slotQueryable.Where(x =>
				x.TenantId == tenantId && x.AcademicYearId == lead.AcademicYearId));
			var examQueryable = await _examRepository.GetQueryableAsync();
			var exams = await AsyncExecuter.ToListAsync(examQueryable.Where(x =>
				x.TenantId == tenantId && x.AcademicYearId == lead.AcademicYearId));
			var counts = exams.GroupBy(x => x.EntranceExamSlotId).ToDictionary(x => x.Key, x => x.Count());
			var now = DateTimeOffset.UtcNow;
			return new AdmissionCrmPublicExamSlotListDto
			{
				SelfSchedulingEnabled = true,
				Slots = slots
					.Where(x => x.StartDate > now && x.StudentLimit > counts.GetValueOrDefault(x.Id))
					.OrderBy(x => x.StartDate)
					.Select(x => new AdmissionCrmPublicExamSlotDto
					{
						EntranceExamSlotId = x.Id,
						SlotName = x.SlotName ?? "Entrance exam",
						StartTime = x.StartDate,
						EndTime = x.EndDate,
						RoomNo = x.RoomNo,
						AvailableSeats = x.StudentLimit - counts.GetValueOrDefault(x.Id)
					})
					.ToList()
			};
		}
	}

	public async Task<AdmissionCrmEntranceExamDto> ScheduleExamAsync(
		Guid tenantId,
		string token,
		ScheduleAdmissionCrmPublicExamDto input)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var (access, lead) = await GetStatusAccessAndLeadAsync(token);
			access.EnsureAccessSession(HashSecret(input.AccessSessionToken), Clock.Now);
			if (!await IsApplicantSelfSchedulingEnabledAsync(lead))
			{
				throw new BusinessException("AdmissionCrm:ApplicantSelfSchedulingNotEnabled");
			}

			return await _entranceExamAppService.ScheduleFromApplicantAsync(lead.Id, input.EntranceExamSlotId);
		}
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionApplicantStatusOfferDto> RespondToOfferAsync(
		Guid tenantId,
		string token,
		Guid offerId,
		RespondAdmissionApplicantOfferDto input)
	{
		using (CurrentTenant.Change(tenantId))
		using (var uow = UnitOfWorkManager.Begin(
			       new AbpUnitOfWorkOptions { IsTransactional = true },
			       requiresNew: true))
		{
			var (access, lead) = await GetStatusAccessAndLeadAsync(token);
			access.EnsureAccessSession(HashSecret(input.AccessSessionToken), Clock.Now);

			var offerQueryable = await _seatOfferRepository.GetQueryableAsync();
			var offer = await AsyncExecuter.FirstOrDefaultAsync(offerQueryable.Where(x =>
				x.Id == offerId && x.TenantId == tenantId && x.LeadId == lead.Id));
			if (offer == null)
			{
				throw new BusinessException("AdmissionCrm:SeatOfferNotFound");
			}

			var reservationQueryable = await _seatReservationRepository.GetQueryableAsync();
			var reservation = await AsyncExecuter.FirstOrDefaultAsync(reservationQueryable.Where(x =>
				x.TenantId == tenantId && x.SeatOfferId == offer.Id));
			if (reservation == null)
			{
				throw new BusinessException("AdmissionCrm:SeatReservationNotFound");
			}
			var poolQueryable = await _seatPoolRepository.GetQueryableAsync();
			var pool = await AsyncExecuter.FirstOrDefaultAsync(poolQueryable.Where(x =>
				x.Id == offer.SeatPoolId && x.TenantId == tenantId));
			if (pool == null)
			{
				throw new BusinessException("AdmissionCrm:SeatPoolNotFound");
			}

			var now = Clock.Now;
			if (input.Accept)
			{
				if (!input.Declarations.TryGetValue("offerTermsAccepted", out var termsAccepted) || !termsAccepted)
				{
					throw new BusinessException("AdmissionCrm:OfferTermsAcceptanceRequired");
				}
				await EnsureApplicantOfferPaymentGateAsync(lead.Id, now);
				offer.Accept("Applicant OTP link", now, JsonSerializer.Serialize(new
				{
					input.Language, input.Declarations, Actor = "ApplicantOtpSession",
					RespondedAt = now, TermsVersion = offer.Version
				}));
				reservation.Commit(now);
				pool.AcceptReservedSeat();
				await UpdatePromotedWaitlistAsync(offer, now, accepted: true, null);
			}
			else
			{
				var reason = string.IsNullOrWhiteSpace(input.Reason)
					? "Applicant declined the offer."
					: input.Reason.Trim();
				offer.Decline("Applicant OTP link", reason, now, JsonSerializer.Serialize(new
				{
					input.Language, Actor = "ApplicantOtpSession", RespondedAt = now,
					TermsVersion = offer.Version
				}));
				reservation.Release(reason, now);
				pool.ReleaseSeat(wasAccepted: false, cancelled: false);
				await UpdatePromotedWaitlistAsync(offer, now, accepted: false, reason);
			}

			await _seatOfferRepository.UpdateAsync(offer, autoSave: true);
			await _seatReservationRepository.UpdateAsync(reservation, autoSave: true);
			await _seatPoolRepository.UpdateAsync(pool, autoSave: true);
			await _timelineRepository.InsertAsync(new AdmissionCrmTimelineEvent(
				GuidGenerator.Create(), tenantId, lead.Id, lead.AcademicYearId,
				input.Accept ? AdmissionCrmTimelineEventType.OfferAccepted : AdmissionCrmTimelineEventType.OfferDeclined,
				input.Accept ? "Seat offer accepted" : "Seat offer declined",
				input.Accept ? $"Offer {offer.OfferNumber} was accepted through the applicant status link." : offer.ResponseReason,
				offer.Id, null, now), autoSave: true);
			await uow.CompleteAsync();
			return ToApplicantStatusOfferDto(offer);
		}
	}

	public async Task<AdmissionCrmMessageDeliveryLogDto> ApplyDeliveryCallbackAsync(
		Guid tenantId,
		Guid deliveryLogId,
		ApplyAdmissionCrmDeliveryCallbackDto input)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var log = await _deliveryLogRepository.GetAsync(deliveryLogId);
			if (log.TenantId != CurrentTenant.Id)
			{
				throw new BusinessException("AdmissionCrm:MessageDeliveryLogNotFound")
					.WithData("DeliveryLogId", deliveryLogId);
			}

			var provider = await GetProviderOrDefaultAsync(log.AcademicYearId, log.Channel);
			var callback = ResolveDeliveryCallback(provider?.SettingsJson, input);
			EnsureDeliveryCallbackSecret(provider, callback, log.Channel, deliveryLogId);
			return await ApplyResolvedDeliveryCallbackAsync(log, callback);
		}
	}

	public async Task<AdmissionCrmMessageDeliveryLogDto> ApplyDeliveryCallbackByProviderMessageAsync(
		Guid tenantId,
		Guid academicYearId,
		AdmissionCrmMessageChannel channel,
		ApplyAdmissionCrmDeliveryCallbackDto input)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var provider = await GetProviderOrDefaultAsync(academicYearId, channel);
			var callback = ResolveDeliveryCallback(provider?.SettingsJson, input);
			EnsureDeliveryCallbackSecret(provider, callback, channel);

			var providerMessageId = NormalizeProviderMessageId(callback.ProviderMessageId);
			if (string.IsNullOrWhiteSpace(providerMessageId))
			{
				throw new BusinessException("AdmissionCrm:DeliveryCallbackProviderMessageIdRequired")
					.WithData("AcademicYearId", academicYearId)
					.WithData("Channel", channel);
			}

			var log = await GetDeliveryLogByProviderMessageIdAsync(academicYearId, channel, providerMessageId);
			return await ApplyResolvedDeliveryCallbackAsync(
				log,
				callback with { ProviderMessageId = providerMessageId });
		}
	}

	private async Task<AdmissionInquiryFormTemplate> GetActivePublicTemplateAsync(Guid templateId)
	{
		var template = await _formTemplateRepository.GetAsync(templateId);
		if (template.TenantId != CurrentTenant.Id || !template.IsPublic || !template.IsActive)
		{
			throw new BusinessException("AdmissionCrm:PublicFormNotFound")
				.WithData("TemplateId", templateId);
		}

		return template;
	}

	private async Task EnsurePublicFormChannelEnabledAsync(AdmissionInquiryFormTemplate template)
	{
		var configuration = await GetCycleConfigurationOrDefaultAsync(template.AcademicYearId);
		var channelSetting = AdmissionCrmChannelSettingsSerializer.GetChannelSetting(
			configuration.ChannelSettingsJson,
			template.Channel);
		if (!channelSetting.IsEnabled)
		{
			throw new BusinessException("AdmissionCrm:PublicFormNotFound")
				.WithData("TemplateId", template.Id);
		}

		var admissionYearSetup = await GetAdmissionYearSetupForCaptureAsync(template.AcademicYearId);
		await ResolvePublicInquirySourceAsync(
			template.AcademicYearId,
			template.Channel,
			channelSetting.DefaultInquirySourceId,
			admissionYearSetup is not null);
		await ResolveDefaultInquiryTypeAsync(
			template.AcademicYearId,
			admissionYearSetup is not null);
	}

	private async Task<AdmissionCrmCycleConfiguration> GetCycleConfigurationOrDefaultAsync(Guid academicYearId)
	{
		var queryable = await _cycleRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId));

		return existing ?? new AdmissionCrmCycleConfiguration(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			"Default Admission Year");
	}

	private async Task<AdmissionCrmAdmissionYearSetup?> GetAdmissionYearSetupForCaptureAsync(
		Guid academicYearId)
	{
		var setups = await _admissionYearRepository.GetListAsync(setup =>
			setup.TenantId == CurrentTenant.Id &&
			setup.AcademicYearId == academicYearId);
		if (setups.Count == 0)
		{
			return null;
		}

		var published = setups.SingleOrDefault(setup =>
			setup.Status == AdmissionCrmAdmissionYearSetupStatus.Published);
		if (published is null)
		{
			var latest = setups.OrderByDescending(setup => setup.Version).First();
			throw new BusinessException("AdmissionCrm:AdmissionYearNotOpen")
				.WithData("AcademicYearId", academicYearId)
				.WithData("Status", latest.Status)
				.WithData("Version", latest.Version);
		}

		published.EnsureInquiryCaptureAllowed(Clock.Now);
		if (string.IsNullOrWhiteSpace(published.PublishedSnapshotJson))
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSnapshotMissing")
				.WithData("SetupId", published.Id)
				.WithData("Version", published.Version);
		}

		return published;
	}

	private async Task<AdmissionCrmInquirySource?> ResolvePublicInquirySourceAsync(
		Guid academicYearId,
		AdmissionInquiryChannel channel,
		Guid? inquirySourceId,
		bool required)
	{
		if (!inquirySourceId.HasValue)
		{
			if (required)
			{
				throw new BusinessException("AdmissionCrm:InquirySourceRequired")
					.WithData("AcademicYearId", academicYearId)
					.WithData("Channel", channel);
			}

			return null;
		}

		var source = await _inquirySourceRepository.FindAsync(inquirySourceId.Value);
		if (source is null ||
			source.TenantId != CurrentTenant.Id ||
			source.AcademicYearId != academicYearId ||
			!source.IsActive ||
			!source.Supports(channel))
		{
			throw new BusinessException("AdmissionCrm:InquirySourceUnavailable")
				.WithData("InquirySourceId", inquirySourceId.Value)
				.WithData("AcademicYearId", academicYearId)
				.WithData("Channel", channel);
		}

		return source;
	}

	private async Task<AdmissionCrmInquiryType?> ResolveDefaultInquiryTypeAsync(
		Guid academicYearId,
		bool required)
	{
		var queryable = await _inquiryTypeRepository.GetQueryableAsync();
		var inquiryType = await AsyncExecuter.FirstOrDefaultAsync(
			queryable
				.Where(type =>
					type.TenantId == CurrentTenant.Id &&
					type.AcademicYearId == academicYearId &&
					type.IsActive)
				.OrderBy(type => type.DisplayOrder)
				.ThenBy(type => type.DisplayName));
		if (inquiryType is null && required)
		{
			throw new BusinessException("AdmissionCrm:InquiryTypeRequired")
				.WithData("AcademicYearId", academicYearId);
		}

		return inquiryType;
	}

	private static AdmissionCrmRouteType? ResolveDefaultPublicRoute(
		AdmissionCrmAdmissionYearSetup? setup)
	{
		if (setup is null)
		{
			return null;
		}

		var routeTypes =
			JsonSerializer.Deserialize<HashSet<AdmissionCrmRouteType>>(setup.RouteTypesJson, JsonOptions) ?? [];
		if (routeTypes.Count == 0)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearRoutesRequired")
				.WithData("AcademicYearId", setup.AcademicYearId)
				.WithData("SetupVersion", setup.Version);
		}

		return routeTypes.Contains(AdmissionCrmRouteType.Fresh)
			? AdmissionCrmRouteType.Fresh
			: routeTypes.OrderBy(route => (int)route).First();
	}

	private static void ValidateAdmissionYearScope(
		AdmissionCrmAdmissionYearSetup setup,
		Guid? targetStandardId,
		AdmissionCrmRouteType? routeType)
	{
		if (!targetStandardId.HasValue)
		{
			throw new BusinessException("AdmissionCrm:TargetStandardRequired")
				.WithData("AcademicYearId", setup.AcademicYearId)
				.WithData("SetupVersion", setup.Version);
		}

		var standardIds =
			JsonSerializer.Deserialize<HashSet<Guid>>(setup.TargetStandardIdsJson, JsonOptions) ?? [];
		if (!standardIds.Contains(targetStandardId.Value))
		{
			throw new BusinessException("AdmissionCrm:StandardNotOpenForAdmission")
				.WithData("AcademicYearId", setup.AcademicYearId)
				.WithData("TargetStandardId", targetStandardId.Value)
				.WithData("SetupVersion", setup.Version);
		}

		var routeTypes =
			JsonSerializer.Deserialize<HashSet<AdmissionCrmRouteType>>(setup.RouteTypesJson, JsonOptions) ?? [];
		var effectiveRoute = routeType ?? AdmissionCrmRouteType.Fresh;
		if (!routeTypes.Contains(effectiveRoute))
		{
			throw new BusinessException("AdmissionCrm:RouteNotOpenForAdmission")
				.WithData("AcademicYearId", setup.AcademicYearId)
				.WithData("RouteType", effectiveRoute)
				.WithData("SetupVersion", setup.Version);
		}
	}

	private static void EnsurePublishedAdmissionYearCaptureFields(
		IReadOnlyDictionary<string, string?> mapped)
	{
		if (string.IsNullOrWhiteSpace(GetValue(mapped, "ParentGuardianName")))
		{
			throw new BusinessException("AdmissionCrm:ParentGuardianNameRequired");
		}

		if (string.IsNullOrWhiteSpace(GetValue(mapped, "PrimaryContactNo")))
		{
			throw new BusinessException("AdmissionCrm:GuardianMobileRequired");
		}
	}

	private static string BuildLegacyAdmissionYearSnapshot(
		AdmissionCrmCycleConfiguration configuration)
	{
		return JsonSerializer.Serialize(
			new
			{
				schemaVersion = 0,
				source = "legacy-cycle-configuration",
				configuration.AcademicYearId,
				configuration.EntranceExamRequired,
				FinanceVerificationRequired = false,
				PlacementTiming = AdmissionCrmPlacementTiming.StandardBeforeActivation
			},
			JsonOptions);
	}

	private static string GetAdmissionRouteLabel(AdmissionCrmRouteType routeType) => routeType switch
	{
		AdmissionCrmRouteType.Fresh => "Fresh admission",
		AdmissionCrmRouteType.SiblingPriority => "Sibling priority",
		AdmissionCrmRouteType.AlumniPreference => "Alumni preference",
		AdmissionCrmRouteType.StaffChild => "Staff child",
		AdmissionCrmRouteType.RteEws => "RTE / EWS",
		AdmissionCrmRouteType.Transfer => "Transfer",
		AdmissionCrmRouteType.Lateral => "Lateral entry",
		AdmissionCrmRouteType.MidYear => "Mid-year admission",
		AdmissionCrmRouteType.ReturningStudent => "Returning student",
		AdmissionCrmRouteType.Interstate => "Interstate transfer",
		AdmissionCrmRouteType.OverseasReturn => "Overseas return",
		AdmissionCrmRouteType.Direct => "Direct registration",
		AdmissionCrmRouteType.ManagementApproved => "Management approved",
		AdmissionCrmRouteType.SchoolDefined => "School-defined",
		_ => routeType.ToString()
	};

	private async Task<(AdmissionApplicantStatusAccess Access, AdmissionInquiryLead Lead)> GetStatusAccessAndLeadAsync(
		string token)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			throw new BusinessException("AdmissionCrm:StatusTokenInvalid");
		}

		var tokenHash = HashSecret(token);
		var accessQueryable = await _statusAccessRepository.GetQueryableAsync();
		var access = await AsyncExecuter.FirstOrDefaultAsync(accessQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.TokenHash == tokenHash));
		if (access == null)
		{
			throw new BusinessException("AdmissionCrm:StatusTokenInvalid");
		}

		var lead = await _leadRepository.GetAsync(access.LeadId);
		if (lead.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:StatusTokenInvalid");
		}

		return (access, lead);
	}

	private async Task<bool> IsApplicantSelfSchedulingEnabledAsync(AdmissionInquiryLead lead)
	{
		var queryable = await _playbookRepository.GetQueryableAsync();
		var playbooks = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == lead.AcademicYearId &&
			x.Status == AdmissionCrmPlaybookStatus.Published));
		foreach (var playbook in playbooks.OrderBy(x => x.IsFallback))
		{
			var scope = JsonSerializer.Deserialize<AdmissionCrmPlaybookApplicabilityDto>(playbook.ApplicabilityJson, JsonOptions)
			            ?? new AdmissionCrmPlaybookApplicabilityDto();
			if (!playbook.IsFallback && !MatchesApplicantScope(scope, lead))
			{
				continue;
			}

			var rules = JsonSerializer.Deserialize<List<AdmissionCrmSlotAssignmentRuleDto>>(playbook.AssignmentRulesJson, JsonOptions) ?? [];
			if (rules.Any(x => x.IsActive && string.Equals(x.Trigger, "ApplicantSelfSchedule", StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}
		}
		return false;
	}

	private static bool MatchesApplicantScope(
		AdmissionCrmPlaybookApplicabilityDto configured,
		AdmissionInquiryLead lead)
	{
		return MatchesOptional(configured.Campus, lead.CampusBranchName) &&
		       MatchesOptional(configured.DesiredClass, lead.DesiredClassName) &&
		       string.IsNullOrWhiteSpace(configured.Board) &&
		       string.IsNullOrWhiteSpace(configured.Medium) &&
		       string.IsNullOrWhiteSpace(configured.ApplicantCategory) &&
		       string.IsNullOrWhiteSpace(configured.AdmissionRound) &&
		       string.IsNullOrWhiteSpace(configured.AdmissionRoute);
	}

	private static bool MatchesOptional(string? configured, string? actual)
	{
		return string.IsNullOrWhiteSpace(configured) ||
		       string.Equals(configured.Trim(), actual?.Trim(), StringComparison.OrdinalIgnoreCase);
	}

	private static void ValidatePublicSubmission(
		AdmissionInquiryFormTemplate template,
		IReadOnlyList<AdmissionCrmFormFieldDto> fields,
		PublicAdmissionInquirySubmitDto input)
	{
		if (!input.ConsentAccepted)
		{
			throw new BusinessException("AdmissionCrm:ConsentRequired");
		}

		if (!string.Equals(input.ConsentVersion, template.ConcurrencyStamp, StringComparison.Ordinal))
		{
			throw new BusinessException("AdmissionCrm:ConsentVersionExpired");
		}

		if (input.CommunicationPreferences.Count == 0)
		{
			throw new BusinessException("AdmissionCrm:CommunicationPreferenceRequired");
		}

		foreach (var preference in input.CommunicationPreferences)
		{
			Check.Length(preference.Trim(), nameof(input.CommunicationPreferences), 80);
		}

		foreach (var field in fields.Where(x => x.Required && IsFieldVisible(x, input.FieldValues)))
		{
			if (string.IsNullOrWhiteSpace(GetSubmittedOrDefaultFieldValue(field, input.FieldValues)))
			{
				throw new BusinessException("AdmissionCrm:RequiredPublicFieldMissing")
					.WithData("FieldKey", field.Key);
			}
		}

		var mapped = MapSystemFields(fields, input.FieldValues);
		if (string.IsNullOrWhiteSpace(GetValue(mapped, "PrimaryContactNo")) &&
		    string.IsNullOrWhiteSpace(GetValue(mapped, "Email")))
		{
			throw new BusinessException("AdmissionCrm:PublicSubmissionContactRequired");
		}

		_ = GetRequiredValue(mapped, "AdmissionIntent", "AdmissionCrm:PublicSubmissionIntentRequired");
	}

	private async Task<int> CountLikelyDuplicatesForPublicSubmissionAsync(AdmissionInquiryLead lead)
	{
		var profile = DuplicateProfileProbe.FromLead(lead);
		var hasContactCriteria =
			!string.IsNullOrWhiteSpace(lead.NormalizedPrimaryContactNo) ||
			!string.IsNullOrWhiteSpace(lead.NormalizedEmail);
		var hasProfileCriteria = profile.HasMinimumProfile();
		if (!hasContactCriteria && !hasProfileCriteria)
		{
			return 0;
		}

		var queryable = await _leadRepository.GetQueryableAsync();
		var candidates = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == lead.AcademicYearId &&
			x.Id != lead.Id &&
			x.MergedIntoLeadId == null &&
			((lead.NormalizedPrimaryContactNo != null &&
			  x.NormalizedPrimaryContactNo == lead.NormalizedPrimaryContactNo) ||
			 (lead.NormalizedEmail != null &&
			  x.NormalizedEmail == lead.NormalizedEmail) ||
			 (hasProfileCriteria &&
			  x.FirstName != null &&
			  x.FirstName.ToLower() == profile.FirstName &&
			  (x.ParentGuardianName != null || x.DateOfBirth.HasValue) &&
			  ((profile.TargetStandardId.HasValue && x.TargetStandardId == profile.TargetStandardId.Value) ||
			   (profile.DesiredClassName != null &&
			    x.DesiredClassName != null &&
			    x.DesiredClassName.ToLower() == profile.DesiredClassName))))));
		return candidates.Count(x => IsLikelyDuplicate(lead, x, profile));
	}

	private async Task<AdmissionApplicantStatusDto> ToStatusDtoAsync(AdmissionInquiryLead lead, string? requestedLanguage)
	{
		var stageName = await GetStageNameAsync(lead.AcademicYearId, lead.Stage);
		var exam = await GetExamRecordOrDefaultAsync(lead.Id);
		var applicantChecklist = await BuildApplicantChecklistAsync(lead);
		var registration = await GetRegistrationApplicationOrDefaultAsync(lead.Id);
		var documents = await _documentAppService.GetChecklistFromApplicantAsync(lead.Id);
		var offer = await GetApplicantOfferOrDefaultAsync(lead.Id);
		var financialStatus = await GetApplicantFinancialStatusOrDefaultAsync(lead.Id);
		var admissionRoute = await GetApplicantRouteStatusOrDefaultAsync(lead.Id);
		var activation = await GetApplicantActivationStatusOrDefaultAsync(lead.Id);
		if (offer?.Status == AdmissionCrmSeatOfferStatus.Issued && offer.MarkOpened(Clock.Now))
		{
			await _seatOfferRepository.UpdateAsync(offer, autoSave: true);
			await _timelineRepository.InsertAsync(new AdmissionCrmTimelineEvent(
				GuidGenerator.Create(), CurrentTenant.Id, lead.Id, lead.AcademicYearId,
				AdmissionCrmTimelineEventType.OfferOpened, "Seat offer opened",
				$"Offer {offer.OfferNumber} was opened through the applicant status link.",
				offer.Id, null, Clock.Now), autoSave: true);
		}
		if (registration != null && registration.OpenedAt == null)
		{
			registration.MarkOpened(Clock.Now);
			await _registrationApplicationRepository.UpdateAsync(registration, autoSave: true);
			await AddRegistrationTimelineAsync(
				lead,
				registration.Id,
				AdmissionCrmTimelineEventType.RegistrationOpened,
				"Registration opened",
				null);
		}
		var status = new AdmissionApplicantStatusDto
		{
			LeadId = lead.Id,
			DisplayName = BuildDisplayName(lead),
			Stage = lead.Stage,
			StageName = stageName,
			LastActivityTime = lead.LastActivityTime,
			NextStep = GetNextStep(lead.Stage),
			Checklist = BuildChecklist(lead, exam),
			ApplicantChecklist = applicantChecklist,
			Activity = await BuildApplicantActivityAsync(lead),
			Exam = exam is null ? null : ToApplicantStatusExamDto(exam),
			Registration = registration == null ? null : ToPublicRegistrationDto(registration),
			Documents = documents,
			Offer = offer == null ? null : ToApplicantStatusOfferDto(offer),
			FinancialStatus = financialStatus,
			AdmissionRoute = admissionRoute,
			Activation = activation
		};
		return LocalizeApplicantStatus(status, requestedLanguage ?? lead.PreferredCommunicationLanguage);
	}

	private static AdmissionApplicantStatusDto LocalizeApplicantStatus(AdmissionApplicantStatusDto status, string? requestedLanguage)
	{
		var language = NormalizeApplicantLanguage(requestedLanguage);
		status.Language = language;
		if (language == "en") return status;
		status.StageName = TranslateApplicantText(language, GetDefaultStageName(status.Stage));
		status.NextStep = TranslateApplicantText(language, status.NextStep);
		foreach (var item in status.Checklist.Concat(status.ApplicantChecklist))
		{
			item.Label = TranslateApplicantText(language, item.Label);
			item.Status = TranslateApplicantText(language, item.Status);
			if (item.Details != null) item.Details = TranslateApplicantText(language, item.Details);
		}
		foreach (var item in status.Activity)
		{
			item.Title = TranslateApplicantText(language, item.Title);
			if (item.Details != null) item.Details = TranslateApplicantText(language, item.Details);
		}
		return status;
	}

	private static string TranslateApplicantText(string language, string text) =>
		ApplicantTranslations.TryGetValue(text, out var translation)
			? language == "gu" ? translation.Gujarati : translation.Hindi
			: text;

	private static string GetDefaultStageName(AdmissionInquiryStage stage) => stage switch
	{
		AdmissionInquiryStage.NewInquiry => "New Inquiry", AdmissionInquiryStage.FollowUp => "Follow-up",
		AdmissionInquiryStage.ExamReady => "Exam Ready", AdmissionInquiryStage.ExamScheduled => "Exam Scheduled",
		AdmissionInquiryStage.ExamCompleted => "Exam Completed", AdmissionInquiryStage.Eligible => "Eligible",
		AdmissionInquiryStage.Waitlisted => "Waitlisted", AdmissionInquiryStage.Rejected => "Rejected",
		AdmissionInquiryStage.Stale => "Stale", AdmissionInquiryStage.Archived => "Archived", _ => stage.ToString()
	};

	private async Task<AdmissionApplicantRouteStatusDto?> GetApplicantRouteStatusOrDefaultAsync(Guid leadId)
	{
		var queryable = await _routeAssignmentRepository.GetQueryableAsync();
		var assignment = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId && x.IsPrimary));
		return assignment == null ? null : new AdmissionApplicantRouteStatusDto
		{
			RouteType = assignment.RouteType,
			EligibilityStatus = assignment.Status,
			AssignedAt = assignment.AssignedAt,
			EvaluatedAt = assignment.EvaluatedAt
		};
	}

	private async Task<AdmissionApplicantActivationStatusDto?> GetApplicantActivationStatusOrDefaultAsync(Guid leadId)
	{
		var queryable = await _activationRepository.GetQueryableAsync();
		var activation = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
		return activation == null ? null : new AdmissionApplicantActivationStatusDto
		{
			ActivationReference = activation.ActivationReference,
			Status = activation.Status,
			FirstDayReadyAt = activation.FirstDayReadyAt
		};
	}

	private async Task<AdmissionApplicantFinancialStatusDto?> GetApplicantFinancialStatusOrDefaultAsync(Guid leadId)
	{
		var queryable = await _quotationRepository.GetQueryableAsync();
		var quotation = await AsyncExecuter.FirstOrDefaultAsync(queryable
			.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId &&
				x.Status == AdmissionCrmQuotationStatus.Issued)
			.OrderByDescending(x => x.Version));
		if (quotation == null)
		{
			return null;
		}
		var components = JsonSerializer.Deserialize<IReadOnlyList<AdmissionCrmFeeComponentDto>>(
			quotation.ComponentsSnapshotJson, JsonOptions) ?? [];
		return new AdmissionApplicantFinancialStatusDto
		{
			QuotationId = quotation.Id,
			QuotationNumber = quotation.QuotationNumber,
			Currency = quotation.Currency,
			Components = components.Select(x => new AdmissionApplicantFeeComponentDto
			{
				Name = x.Name,
				Amount = x.Amount,
				IsRefundable = x.IsRefundable,
				ParentDescription = x.ParentDescription
			}).ToList(),
			GrossAmount = quotation.GrossAmount,
			ConcessionAmount = quotation.ConcessionAmount,
			NetAmount = quotation.NetAmount,
			ConfirmedAmount = quotation.ConfirmedAmount,
			OutstandingAmount = Math.Max(0, quotation.NetAmount - quotation.ConfirmedAmount),
			FinanceState = quotation.FinanceState,
			GateType = quotation.GateType,
			GateMilestone = quotation.GateMilestone,
			IsGateSatisfied = quotation.IsGateSatisfiedAt(Clock.Now),
			ValidUntil = quotation.ValidUntil,
			FinanceReference = quotation.FinanceReference
		};
	}

	private async Task<AdmissionCrmSeatOffer?> GetApplicantOfferOrDefaultAsync(Guid leadId)
	{
		var queryable = await _seatOfferRepository.GetQueryableAsync();
		var offers = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
		return offers
			.OrderByDescending(x => x.Version)
			.FirstOrDefault(x => x.Status is AdmissionCrmSeatOfferStatus.Issued or AdmissionCrmSeatOfferStatus.Opened or AdmissionCrmSeatOfferStatus.Accepted) ??
			offers.OrderByDescending(x => x.Version).FirstOrDefault();
	}

	private async Task EnsureApplicantOfferPaymentGateAsync(Guid leadId, DateTime at)
	{
		var queryable = await _quotationRepository.GetQueryableAsync();
		var quotation = await AsyncExecuter.FirstOrDefaultAsync(queryable
			.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId &&
				x.Status == AdmissionCrmQuotationStatus.Issued)
			.OrderByDescending(x => x.Version));
		if (quotation == null ||
			quotation.GateMilestone != AdmissionCrmPaymentGateMilestone.OfferAcceptance ||
			quotation.IsGateSatisfiedAt(at))
		{
			return;
		}

		throw new BusinessException("AdmissionCrm:PaymentGateNotSatisfied")
			.WithData("QuotationId", quotation.Id)
			.WithData("GateType", quotation.GateType)
			.WithData("FinanceState", quotation.FinanceState);
	}

	private async Task UpdatePromotedWaitlistAsync(
		AdmissionCrmSeatOffer offer,
		DateTime at,
		bool accepted,
		string? reason)
	{
		var queryable = await _waitlistRepository.GetQueryableAsync();
		var entry = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.OfferId == offer.Id &&
			x.Status == AdmissionCrmWaitlistStatus.Promoted));
		if (entry == null)
		{
			return;
		}
		if (accepted)
		{
			entry.MarkAccepted(at);
		}
		else
		{
			entry.ClosePromotedOffer(reason ?? "Applicant declined the offer.", at);
		}
		await _waitlistRepository.UpdateAsync(entry, autoSave: true);
	}

	private static AdmissionApplicantStatusOfferDto ToApplicantStatusOfferDto(AdmissionCrmSeatOffer offer) => new()
	{
		OfferId = offer.Id,
		OfferNumber = offer.OfferNumber,
		OfferType = offer.OfferType,
		Status = offer.Status,
		Terms = JsonSerializer.Deserialize<Dictionary<string, string?>>(offer.TermsSnapshotJson, JsonOptions) ?? new(),
		Language = offer.Language,
		ValidFrom = offer.ValidFrom,
		ValidUntil = offer.ValidUntil,
		RespondedAt = offer.RespondedAt,
		OpenedAt = offer.OpenedAt,
		ResponseReason = offer.ResponseReason
	};

	private async Task<AdmissionCrmRegistrationApplication?> GetRegistrationApplicationOrDefaultAsync(Guid leadId)
	{
		var queryable = await _registrationApplicationRepository.GetQueryableAsync();
		var applications = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
		return applications
			.OrderByDescending(x => x.IssuedAt)
			.FirstOrDefault(x => x.Status != AdmissionCrmRegistrationStatus.Revoked) ?? applications.OrderByDescending(x => x.IssuedAt).FirstOrDefault();
	}

	private async Task<AdmissionCrmRegistrationApplication> GetRegistrationApplicationAsync(Guid leadId)
	{
		return await GetRegistrationApplicationOrDefaultAsync(leadId)
		       ?? throw new BusinessException("AdmissionCrm:RegistrationApplicationNotFound");
	}

	private static AdmissionCrmRegistrationTemplateDto DeserializeRegistrationTemplate(string json)
	{
		return JsonSerializer.Deserialize<AdmissionCrmRegistrationTemplateDto>(json, JsonOptions)
		       ?? throw new BusinessException("AdmissionCrm:InvalidStoredRegistrationConfiguration");
	}

	private static AdmissionCrmRegistrationApplicationDto ToPublicRegistrationDto(AdmissionCrmRegistrationApplication application)
	{
		var snapshot = DeserializeRegistrationTemplate(application.TemplateSnapshotJson);
		return new AdmissionCrmRegistrationApplicationDto
		{
			Id = application.Id,
			AcademicYearId = application.AcademicYearId,
			LeadId = application.LeadId,
			RegistrationTemplateId = application.RegistrationTemplateId,
			RegistrationTemplateVersion = application.RegistrationTemplateVersion,
			PlaybookId = application.PlaybookId,
			FormNumber = application.FormNumber,
			Status = application.Status,
			Sections = snapshot.Sections,
			Prefill = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(application.PrefillJson, JsonOptions) ?? new Dictionary<string, JsonElement>(),
			Responses = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(application.ResponseJson, JsonOptions) ?? new Dictionary<string, JsonElement>(),
			CorrectionFieldKeys = JsonSerializer.Deserialize<List<string>>(application.CorrectionJson, JsonOptions) ?? [],
			CompletionPercentage = application.CompletionPercentage,
			IssuedAt = application.IssuedAt,
			LinkExpiresAt = application.LinkExpiresAt,
			OpenedAt = application.OpenedAt,
			LastSavedAt = application.LastSavedAt,
			SubmittedAt = application.SubmittedAt,
			VerifiedAt = application.VerifiedAt,
			FeeAmount = application.FeeAmount,
			FeeStatus = application.FeeStatus,
			PaymentReference = application.PaymentReference,
			LifecycleReason = application.LifecycleReason
		};
	}

	private async Task AddRegistrationTimelineAsync(
		AdmissionInquiryLead lead,
		Guid applicationId,
		AdmissionCrmTimelineEventType eventType,
		string title,
		string? details)
	{
		await _timelineRepository.InsertAsync(new AdmissionCrmTimelineEvent(
			GuidGenerator.Create(), CurrentTenant.Id, lead.Id, lead.AcademicYearId, eventType, title, details,
			applicationId, actorUserId: null, occurredAt: new DateTimeOffset(Clock.Now)), autoSave: true);
	}

	private async Task<List<AdmissionApplicantStatusChecklistItemDto>> BuildApplicantChecklistAsync(
		AdmissionInquiryLead lead)
	{
		var queryable = await _cycleRepository.GetQueryableAsync();
		var configuration = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == lead.AcademicYearId));
		if (configuration == null)
		{
			return [];
		}

		var progressQueryable = await _applicantChecklistProgressRepository.GetQueryableAsync();
		var progressItems = await AsyncExecuter.ToListAsync(progressQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.LeadId == lead.Id));
		var progressByKey = progressItems
			.GroupBy(x => x.ChecklistKey, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.StatusChangedAt).First(), StringComparer.OrdinalIgnoreCase);
		return AdmissionCrmApplicantChecklistSerializer
			.DeserializeItems(configuration.ApplicantChecklistJson)
			.OrderBy(item => item.DisplayOrder)
			.ThenBy(item => item.Label)
			.Select(item => new AdmissionApplicantStatusChecklistItemDto
			{
				Key = item.Key,
				Label = item.Label,
				Status = GetApplicantChecklistPublicStatus(item, progressByKey.GetValueOrDefault(item.Key)),
				StatusCode = GetApplicantChecklistPublicStatusCode(progressByKey.GetValueOrDefault(item.Key)),
				Details = item.Details,
				IsRequired = item.IsRequired,
				IsComplete = IsApplicantChecklistComplete(progressByKey.GetValueOrDefault(item.Key)?.Status),
				StatusChangedAt = progressByKey.GetValueOrDefault(item.Key)?.StatusChangedAt
			})
			.ToList();
	}

	private static string GetApplicantChecklistPublicStatus(
		AdmissionCrmApplicantChecklistItemDto item,
		AdmissionCrmApplicantChecklistProgress? progress)
	{
		return progress == null
			? item.IsRequired ? "Required" : "Optional"
			: GetApplicantChecklistProgressStatusLabel(progress.Status);
	}

	private static string GetApplicantChecklistPublicStatusCode(
		AdmissionCrmApplicantChecklistProgress? progress)
	{
		return progress?.Status.ToString() ?? AdmissionCrmApplicantChecklistProgressStatus.Pending.ToString();
	}

	private static string GetApplicantChecklistProgressStatusLabel(
		AdmissionCrmApplicantChecklistProgressStatus status)
	{
		return status switch
		{
			AdmissionCrmApplicantChecklistProgressStatus.Requested => "Requested",
			AdmissionCrmApplicantChecklistProgressStatus.Received => "Received",
			AdmissionCrmApplicantChecklistProgressStatus.Waived => "Waived",
			_ => "Pending"
		};
	}

	private static bool IsApplicantChecklistComplete(
		AdmissionCrmApplicantChecklistProgressStatus? status)
	{
		return status is AdmissionCrmApplicantChecklistProgressStatus.Received or
			AdmissionCrmApplicantChecklistProgressStatus.Waived;
	}

	private async Task<List<AdmissionApplicantStatusActivityDto>> BuildApplicantActivityAsync(
		AdmissionInquiryLead lead)
	{
		var timelineQueryable = await _timelineRepository.GetQueryableAsync();
		var timelineEvents = await AsyncExecuter.ToListAsync(timelineQueryable
			.Where(x =>
				x.TenantId == CurrentTenant.Id &&
				x.AcademicYearId == lead.AcademicYearId &&
				x.LeadId == lead.Id));
		var activity = timelineEvents
			.Where(IsApplicantVisibleTimelineEvent)
			.Select(ToApplicantTimelineActivity);

		var deliveryLogQueryable = await _deliveryLogRepository.GetQueryableAsync();
		var deliveryLogs = await AsyncExecuter.ToListAsync(deliveryLogQueryable
			.Where(x =>
				x.TenantId == CurrentTenant.Id &&
				x.AcademicYearId == lead.AcademicYearId &&
				x.LeadId == lead.Id));
		var messageActivity = deliveryLogs.Select(ToApplicantMessageActivity);

		return activity
			.Concat(messageActivity)
			.OrderByDescending(x => x.OccurredAt)
			.Take(10)
			.ToList();
	}

	private async Task<StatusOtpDeliveryResult> DispatchStatusOtpAsync(
		AdmissionInquiryLead lead,
		string otpCode,
		DateTime issuedAt)
	{
		var consentedChannels = await GetConsentedChannelsAsync(lead.Id);
		var candidates = BuildOtpDeliveryCandidates(lead, consentedChannels).ToList();
		if (candidates.Count == 0)
		{
			return new StatusOtpDeliveryResult(
				false,
				"OTP challenge created. No consented contact channel is available for delivery.");
		}

		var selected = candidates[0];
		var selectedProvider = await GetProviderOrDefaultAsync(lead.AcademicYearId, selected.Channel);
		foreach (var candidate in candidates)
		{
			var provider = await GetProviderOrDefaultAsync(lead.AcademicYearId, candidate.Channel);
			if (provider is { IsEnabled: true, Status: AdmissionCrmCommunicationProviderConfiguration.ConfiguredStatus })
			{
				selected = candidate;
				selectedProvider = provider;
				break;
			}
		}

		var normalizedRecipient = AdmissionCrmCommunicationOptOut.NormalizeContact(
			selected.Channel,
			selected.Recipient);
		var optOut = await GetActiveOptOutOrDefaultAsync(
			lead.AcademicYearId,
			selected.Channel,
			normalizedRecipient);
		var dispatch = await ResolveOtpDispatchResultAsync(
			selectedProvider,
			optOut,
			lead,
			selected,
			otpCode);
		var log = new AdmissionCrmMessageDeliveryLog(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			lead.AcademicYearId,
			lead.Id,
			templateId: null,
			selected.Channel,
			selected.Recipient,
			selectedProvider?.ProviderName,
			selected.Channel == AdmissionCrmMessageChannel.Email ? "Admission status OTP" : null,
			$"Your admission status OTP is ******. It expires in {OtpExpiryMinutes} minutes.",
			dispatch.Status,
			dispatch.StatusDetails,
			issuedAt);
		log.RecordProviderMessageId(dispatch.ProviderMessageId);

		await _deliveryLogRepository.InsertAsync(log, autoSave: false);
		await _timelineRepository.InsertAsync(
			new AdmissionCrmTimelineEvent(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				lead.Id,
				lead.AcademicYearId,
				AdmissionCrmTimelineEventType.MessageTemplateChanged,
				"Status OTP delivery recorded",
				$"{selected.Channel}: {dispatch.StatusDetails}",
				log.Id,
				actorUserId: null,
				occurredAt: new DateTimeOffset(issuedAt)),
			autoSave: false);

		return new StatusOtpDeliveryResult(
			selectedProvider is { IsEnabled: true, Status: AdmissionCrmCommunicationProviderConfiguration.ConfiguredStatus },
			dispatch.StatusDetails);
	}

	private async Task<HashSet<AdmissionCrmMessageChannel>> GetConsentedChannelsAsync(Guid leadId)
	{
		var queryable = await _consentRepository.GetQueryableAsync();
		var consent = await AsyncExecuter.FirstOrDefaultAsync(queryable
			.Where(x =>
				x.TenantId == CurrentTenant.Id &&
				x.LeadId == leadId)
			.OrderByDescending(x => x.AcceptedAt));
		if (consent is null)
		{
			return Enum.GetValues<AdmissionCrmMessageChannel>().ToHashSet();
		}

		var preferences = JsonSerializer.Deserialize<List<string>>(
			consent.CommunicationPreferencesJson,
			JsonOptions) ?? [];
		var channels = new HashSet<AdmissionCrmMessageChannel>();
		foreach (var preference in preferences)
		{
			if (preference.Contains("sms", StringComparison.OrdinalIgnoreCase))
			{
				channels.Add(AdmissionCrmMessageChannel.Sms);
			}

			if (preference.Contains("whatsapp", StringComparison.OrdinalIgnoreCase))
			{
				channels.Add(AdmissionCrmMessageChannel.WhatsApp);
			}

			if (preference.Contains("email", StringComparison.OrdinalIgnoreCase))
			{
				channels.Add(AdmissionCrmMessageChannel.Email);
			}
		}

		return channels;
	}

	private async Task<AdmissionCrmCommunicationProviderConfiguration?> GetProviderOrDefaultAsync(
		Guid academicYearId,
		AdmissionCrmMessageChannel channel)
	{
		var queryable = await _providerRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			x.Channel == channel));
	}

	private async Task<AdmissionCrmMessageDeliveryLog> GetDeliveryLogByProviderMessageIdAsync(
		Guid academicYearId,
		AdmissionCrmMessageChannel channel,
		string providerMessageId)
	{
		var queryable = await _deliveryLogRepository.GetQueryableAsync();
		var matches = await AsyncExecuter.ToListAsync(queryable
			.Where(x =>
				x.TenantId == CurrentTenant.Id &&
				x.AcademicYearId == academicYearId &&
				x.Channel == channel &&
				x.ProviderMessageId == providerMessageId)
			.OrderByDescending(x => x.CreationTime)
			.Take(2));

		if (matches.Count == 0)
		{
			throw new BusinessException("AdmissionCrm:MessageDeliveryLogNotFound")
				.WithData("AcademicYearId", academicYearId)
				.WithData("Channel", channel)
				.WithData("ProviderMessageId", providerMessageId);
		}

		if (matches.Count > 1)
		{
			throw new BusinessException("AdmissionCrm:DeliveryCallbackProviderMessageIdAmbiguous")
				.WithData("AcademicYearId", academicYearId)
				.WithData("Channel", channel)
				.WithData("ProviderMessageId", providerMessageId);
		}

		return matches[0];
	}

	private async Task<AdmissionCrmMessageDeliveryLogDto> ApplyResolvedDeliveryCallbackAsync(
		AdmissionCrmMessageDeliveryLog log,
		ResolvedDeliveryCallback callback)
	{
		var status = NormalizeCallbackStatus(callback.Status);
		var details = BuildCallbackStatusDetails(
			status,
			callback.StatusDetails,
			callback.ProviderMessageId);
		var now = Clock.Now;
		var statusApplied = log.ApplyProviderCallback(status, details, callback.ProviderMessageId, now);
		await _deliveryLogRepository.UpdateAsync(log, autoSave: false);
		await AddDeliveryCallbackTimelineEventAsync(log, status, details, statusApplied, now);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return ToDeliveryLogDto(log);
	}

	private static void EnsureDeliveryCallbackSecret(
		AdmissionCrmCommunicationProviderConfiguration? provider,
		ResolvedDeliveryCallback callback,
		AdmissionCrmMessageChannel channel,
		Guid? deliveryLogId = null)
	{
		var configuredSecret = GetCallbackSecret(provider?.SettingsJson);
		if (string.IsNullOrWhiteSpace(configuredSecret))
		{
			throw new BusinessException("AdmissionCrm:DeliveryCallbackSecretNotConfigured")
				.WithData("Channel", channel);
		}

		if (SecretsEqual(configuredSecret, callback.CallbackSecret))
		{
			return;
		}

		var exception = new BusinessException("AdmissionCrm:DeliveryCallbackSecretInvalid")
			.WithData("Channel", channel);
		if (deliveryLogId.HasValue)
		{
			exception.WithData("DeliveryLogId", deliveryLogId.Value);
		}

		throw exception;
	}

	private async Task AddDeliveryCallbackTimelineEventAsync(
		AdmissionCrmMessageDeliveryLog log,
		string callbackStatus,
		string details,
		bool statusApplied,
		DateTime occurredAt)
	{
		if (!log.LeadId.HasValue)
		{
			return;
		}

		await _timelineRepository.InsertAsync(
			new AdmissionCrmTimelineEvent(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				log.LeadId.Value,
				log.AcademicYearId,
				AdmissionCrmTimelineEventType.MessageTemplateChanged,
				statusApplied
					? "Message delivery callback received"
					: "Message delivery callback ignored",
				statusApplied
					? $"{log.Channel}: {details}"
					: $"{log.Channel}: Ignored stale {callbackStatus} callback because current delivery status is {log.Status}. {details}",
				log.Id,
				null,
				new DateTimeOffset(occurredAt)),
			autoSave: false);
	}

	private async Task<AdmissionCrmCommunicationOptOut?> GetActiveOptOutOrDefaultAsync(
		Guid academicYearId,
		AdmissionCrmMessageChannel channel,
		string normalizedContact)
	{
		var queryable = await _optOutRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			x.Channel == channel &&
			x.NormalizedContactValue == normalizedContact &&
			x.IsActive));
	}

	private async Task<AdmissionCrmCommunicationDispatchResult> ResolveOtpDispatchResultAsync(
		AdmissionCrmCommunicationProviderConfiguration? provider,
		AdmissionCrmCommunicationOptOut? optOut,
		AdmissionInquiryLead lead,
		StatusOtpDeliveryCandidate candidate,
		string otpCode)
	{
		if (optOut is not null)
		{
			return new AdmissionCrmCommunicationDispatchResult
			{
				Status = AdmissionCrmMessageDeliveryLog.BlockedByOptOutStatus,
				StatusDetails = "OTP delivery blocked because the recipient opted out."
			};
		}

		if (provider is not { IsEnabled: true, Status: AdmissionCrmCommunicationProviderConfiguration.ConfiguredStatus })
		{
			return new AdmissionCrmCommunicationDispatchResult
			{
				Status = AdmissionCrmMessageDeliveryLog.ProviderNotConfiguredStatus,
				StatusDetails = "OTP challenge created but not sent because no enabled provider is configured for the consented channel."
			};
		}

		return await _dispatcher.DispatchAsync(
			provider,
			new AdmissionCrmCommunicationDispatchRequest
			{
				AcademicYearId = lead.AcademicYearId,
				LeadId = lead.Id,
				TemplateId = null,
				Channel = candidate.Channel,
				Recipient = candidate.Recipient,
				Subject = candidate.Channel == AdmissionCrmMessageChannel.Email
					? "Admission status OTP"
					: null,
				Body = $"Your admission status OTP is {otpCode}. It expires in {OtpExpiryMinutes} minutes.",
				Trigger = "StatusOtp"
			});
	}

	private async Task<AdmissionCrmEntranceExamRecord?> GetExamRecordOrDefaultAsync(Guid leadId)
	{
		var queryable = await _examRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable
			.Where(x =>
				x.TenantId == CurrentTenant.Id &&
				x.LeadId == leadId)
			.OrderByDescending(x => x.ScheduledAt)
			.ThenByDescending(x => x.CreationTime));
	}

	private async Task<string> GetStageNameAsync(Guid academicYearId, AdmissionInquiryStage stage)
	{
		var queryable = await _stageRepository.GetQueryableAsync();
		var configuredStage = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			x.SystemStage == stage));

		return configuredStage?.StageName ?? stage.ToString();
	}

	private static PublicAdmissionInquiryFormDto ToPublicFormDto(Guid tenantId, AdmissionInquiryFormTemplate template, string? requestedLanguage)
	{
		var language = NormalizeApplicantLanguage(requestedLanguage);
		var localizations = JsonSerializer.Deserialize<Dictionary<string, AdmissionCrmFormTemplateLocalizationDto>>(template.LocalizationsJson, JsonOptions) ?? [];
		localizations.TryGetValue(language, out var localization);
		var fields = DeserializeFields(template.FieldsJson).Select(field => LocalizeField(field, language)).ToList();
		return new PublicAdmissionInquiryFormDto
		{
			TenantId = tenantId,
			TemplateId = template.Id,
			AcademicYearId = template.AcademicYearId,
			TemplateName = localization?.TemplateName ?? template.TemplateName,
			Description = localization?.Description ?? template.Description,
			Channel = template.Channel,
			ConsentText = localization?.ConsentText ?? template.ConsentText ?? string.Empty,
			PrivacyNoticeUrl = template.PrivacyNoticeUrl ?? string.Empty,
			ConsentVersion = template.ConcurrencyStamp,
			Fields = fields
		};
	}

	private static AdmissionCrmFormFieldDto LocalizeField(AdmissionCrmFormFieldDto field, string language)
	{
		field.Label = field.LocalizedLabels.GetValueOrDefault(language, field.Label);
		field.HelperText = field.LocalizedHelperTexts.GetValueOrDefault(language, field.HelperText ?? string.Empty);
		field.Placeholder = field.LocalizedPlaceholders.GetValueOrDefault(language, field.Placeholder ?? string.Empty);
		field.OptionLabels = field.LocalizedOptions.TryGetValue(language, out var optionLabels) &&
			optionLabels.Count == field.Options.Count
			? optionLabels
			: field.Options;
		return field;
	}

	private static string NormalizeApplicantLanguage(string? language) => language?.Trim().ToLowerInvariant() switch
	{
		"gu" or "gu-in" => "gu",
		"hi" or "hi-in" => "hi",
		_ => "en"
	};

	private static IReadOnlyList<AdmissionCrmFormFieldDto> DeserializeFields(string fieldsJson)
	{
		return JsonSerializer.Deserialize<List<AdmissionCrmFormFieldDto>>(fieldsJson, JsonOptions) ?? [];
	}

	private static Dictionary<string, string?> MapSystemFields(
		IReadOnlyList<AdmissionCrmFormFieldDto> fields,
		IReadOnlyDictionary<string, JsonElement> values)
	{
		var mapped = MapPublicTrackingFields(values);
		foreach (var field in fields.Where(x => !string.IsNullOrWhiteSpace(x.SystemField)))
		{
			if (!IsFieldVisible(field, values))
			{
				continue;
			}

			var value = GetSubmittedOrDefaultFieldValue(field, values);
			if (!string.IsNullOrWhiteSpace(value))
			{
				mapped[field.SystemField!] = value;
			}
		}

		return mapped;
	}

	private static bool IsFieldVisible(
		AdmissionCrmFormFieldDto field,
		IReadOnlyDictionary<string, JsonElement> values)
	{
		if (string.IsNullOrWhiteSpace(field.VisibleWhenFieldKey))
		{
			return true;
		}

		if (!values.TryGetValue(field.VisibleWhenFieldKey.Trim(), out var controllingValue))
		{
			return false;
		}

		var actualValue = ExtractString(controllingValue);
		return string.IsNullOrWhiteSpace(field.VisibleWhenValue)
			? !string.IsNullOrWhiteSpace(actualValue)
			: string.Equals(
				actualValue?.Trim(),
				field.VisibleWhenValue.Trim(),
				StringComparison.OrdinalIgnoreCase);
	}

	private static string? GetSubmittedOrDefaultFieldValue(
		AdmissionCrmFormFieldDto field,
		IReadOnlyDictionary<string, JsonElement> values)
	{
		if (values.TryGetValue(field.Key, out var submittedValue))
		{
			var extracted = ExtractString(submittedValue);
			if (!string.IsNullOrWhiteSpace(extracted))
			{
				return extracted;
			}
		}

		return string.IsNullOrWhiteSpace(field.DefaultValue)
			? null
			: field.DefaultValue.Trim();
	}

	private static Dictionary<string, string?> MapPublicTrackingFields(
		IReadOnlyDictionary<string, JsonElement> values)
	{
		var mapped = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
		var campaignName = GetFirstTrackingValue(
			values,
			"CampaignName",
			"campaignName",
			"campaign",
			"utm_campaign",
			"utmCampaign");
		if (!string.IsNullOrWhiteSpace(campaignName))
		{
			mapped["CampaignName"] = campaignName;
		}

		var ownerUserId = GetFirstTrackingValue(
			values,
			"OwnerUserId",
			"ownerUserId",
			"staffUserId",
			"sharedByUserId",
			"referrerUserId");
		if (!string.IsNullOrWhiteSpace(ownerUserId))
		{
			mapped["OwnerUserId"] = ownerUserId;
		}

		return mapped;
	}

	private static Guid? ResolvePublicTrackedOwnerUserId(
		AdmissionInquiryChannel channel,
		IReadOnlyDictionary<string, string?> mapped,
		AdmissionCrmChannelSettingDto channelSetting,
		AdmissionCrmLeadRoutingSettingsDto routingSettings)
	{
		if (channel != AdmissionInquiryChannel.StaffSharedLink)
		{
			return null;
		}

		var ownerUserId = ParseGuid(GetValue(mapped, "OwnerUserId"));
		if (!ownerUserId.HasValue || ownerUserId.Value == Guid.Empty)
		{
			return null;
		}

		return IsAllowedPublicStaffSharedOwner(ownerUserId.Value, channelSetting, routingSettings)
			? ownerUserId
			: null;
	}

	private static bool IsAllowedPublicStaffSharedOwner(
		Guid ownerUserId,
		AdmissionCrmChannelSettingDto channelSetting,
		AdmissionCrmLeadRoutingSettingsDto routingSettings)
	{
		return channelSetting.DefaultOwnerUserId == ownerUserId ||
		       (routingSettings.IsEnabled &&
		        (routingSettings.DefaultOwnerUserId == ownerUserId ||
		         routingSettings.OwnerPoolUserIds.Contains(ownerUserId) ||
		         routingSettings.Rules.Any(x => x.OwnerUserId == ownerUserId)));
	}

	private static string? GetFirstTrackingValue(
		IReadOnlyDictionary<string, JsonElement> values,
		params string[] keys)
	{
		foreach (var key in keys)
		{
			if (values.TryGetValue(key, out var value))
			{
				var normalized = ExtractString(value);
				if (!string.IsNullOrWhiteSpace(normalized))
				{
					return normalized;
				}
			}
		}

		return null;
	}

	private static string? ExtractString(JsonElement value)
	{
		return value.ValueKind switch
		{
			JsonValueKind.String => Normalize(value.GetString()),
			JsonValueKind.Number => value.ToString(),
			JsonValueKind.True => "true",
			JsonValueKind.False => "false",
			JsonValueKind.Array => Normalize(string.Join(", ", value.EnumerateArray().Select(ExtractString))),
			_ => null
		};
	}

	private static string? Normalize(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}

	private static string? GetValue(IReadOnlyDictionary<string, string?> values, string key)
	{
		return values.TryGetValue(key, out var value) ? value : null;
	}

	private static string GetRequiredValue(
		IReadOnlyDictionary<string, string?> values,
		string key,
		string errorCode)
	{
		var value = GetValue(values, key);
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new BusinessException(errorCode);
		}

		return value;
	}

	private static Guid? ParseGuid(string? value)
	{
		return Guid.TryParse(value, out var parsed) ? parsed : null;
	}

	private static DateTimeOffset? ParseDateTimeOffset(string? value)
	{
		return DateTimeOffset.TryParse(
			value,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal,
			out var parsed)
			? parsed
			: null;
	}

	private static string SerializeCommunicationPreferences(IReadOnlyList<string> preferences)
	{
		var normalized = preferences
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(x => x)
			.ToList();

		return JsonSerializer.Serialize(normalized, JsonOptions);
	}

	private static string CreateRawToken()
	{
		var bytes = RandomNumberGenerator.GetBytes(32);
		return Convert.ToBase64String(bytes)
			.TrimEnd('=')
			.Replace('+', '-')
			.Replace('/', '_');
	}

	private string EncryptRequired(string value)
	{
		return _stringEncryptionService.Encrypt(value)
		       ?? throw new BusinessException("AdmissionCrm:EncryptionFailed");
	}

	private static string CreateOtpCode()
	{
		return RandomNumberGenerator
			.GetInt32(0, 1_000_000)
			.ToString("D6", CultureInfo.InvariantCulture);
	}

	private static string HashSecret(string value)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
		return Convert.ToHexString(hash).ToLowerInvariant();
	}

	private static string CreateStatusPath(Guid tenantId, string token)
	{
		return $"/student/admissions/status/{tenantId:D}/{token}";
	}

	private static string MaskDestination(AdmissionInquiryLead lead)
	{
		if (!string.IsNullOrWhiteSpace(lead.PrimaryContactNo))
		{
			var digits = new string(lead.PrimaryContactNo.Where(char.IsDigit).ToArray());
			return digits.Length <= 4
				? "phone ending " + digits
				: "phone ending " + digits[^4..];
		}

		if (!string.IsNullOrWhiteSpace(lead.Email))
		{
			var parts = lead.Email.Split('@', 2);
			if (parts.Length == 2)
			{
				var prefix = parts[0].Length <= 2 ? parts[0] : parts[0][..2] + "***";
				return $"{prefix}@{parts[1]}";
			}
		}

		return "registered contact";
	}

	private static IEnumerable<StatusOtpDeliveryCandidate> BuildOtpDeliveryCandidates(
		AdmissionInquiryLead lead,
		IReadOnlySet<AdmissionCrmMessageChannel> consentedChannels)
	{
		if (!string.IsNullOrWhiteSpace(lead.PrimaryContactNo))
		{
			if (consentedChannels.Contains(AdmissionCrmMessageChannel.Sms))
			{
				yield return new StatusOtpDeliveryCandidate(
					AdmissionCrmMessageChannel.Sms,
					lead.PrimaryContactNo);
			}

			if (consentedChannels.Contains(AdmissionCrmMessageChannel.WhatsApp))
			{
				yield return new StatusOtpDeliveryCandidate(
					AdmissionCrmMessageChannel.WhatsApp,
					lead.PrimaryContactNo);
			}
		}

		if (!string.IsNullOrWhiteSpace(lead.Email) &&
		    consentedChannels.Contains(AdmissionCrmMessageChannel.Email))
		{
			yield return new StatusOtpDeliveryCandidate(
				AdmissionCrmMessageChannel.Email,
			lead.Email);
		}
	}

	private static bool IsLikelyDuplicate(
		AdmissionInquiryLead sourceLead,
		AdmissionInquiryLead candidateLead,
		DuplicateProfileProbe sourceProfile)
	{
		return
			(!string.IsNullOrWhiteSpace(sourceLead.NormalizedPrimaryContactNo) &&
			 candidateLead.NormalizedPrimaryContactNo == sourceLead.NormalizedPrimaryContactNo) ||
			(!string.IsNullOrWhiteSpace(sourceLead.NormalizedEmail) &&
			 candidateLead.NormalizedEmail == sourceLead.NormalizedEmail) ||
			HasProfileMatch(candidateLead, sourceProfile);
	}

	private static bool HasProfileMatch(AdmissionInquiryLead lead, DuplicateProfileProbe profile)
	{
		if (!profile.HasMinimumProfile())
		{
			return false;
		}

		if (NormalizeText(lead.FirstName) != profile.FirstName)
		{
			return false;
		}

		var leadLastName = NormalizeText(lead.LastName);
		if (leadLastName != null && profile.LastName != null && leadLastName != profile.LastName)
		{
			return false;
		}

		if (!HasAdmissionTargetMatch(lead, profile))
		{
			return false;
		}

		return HasParentGuardianMatch(lead, profile) || HasDateOfBirthMatch(lead, profile);
	}

	private static bool HasAdmissionTargetMatch(AdmissionInquiryLead lead, DuplicateProfileProbe profile)
	{
		return
			(profile.TargetStandardId.HasValue && lead.TargetStandardId == profile.TargetStandardId.Value) ||
			(profile.DesiredClassName != null &&
			 NormalizeText(lead.DesiredClassName) == profile.DesiredClassName);
	}

	private static bool HasParentGuardianMatch(AdmissionInquiryLead lead, DuplicateProfileProbe profile)
	{
		return profile.ParentGuardianName != null &&
		       NormalizeText(lead.ParentGuardianName) == profile.ParentGuardianName;
	}

	private static bool HasDateOfBirthMatch(AdmissionInquiryLead lead, DuplicateProfileProbe profile)
	{
		return lead.DateOfBirth.HasValue &&
		       profile.DateOfBirth.HasValue &&
		       lead.DateOfBirth.Value.Date == profile.DateOfBirth.Value.Date;
	}

	private static string? NormalizeText(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		return string.Join(
			" ",
			value
				.Trim()
				.ToLowerInvariant()
				.Split(' ', StringSplitOptions.RemoveEmptyEntries));
	}

	private static string BuildDisplayName(AdmissionInquiryLead lead)
	{
		var studentName = string.Join(
			" ",
			new[] { lead.FirstName, lead.MiddleName, lead.LastName }
				.Where(x => !string.IsNullOrWhiteSpace(x)));

		if (!string.IsNullOrWhiteSpace(studentName))
		{
			return studentName;
		}

		return lead.ParentGuardianName ?? "Applicant";
	}

	private sealed class DuplicateProfileProbe
	{
		private DuplicateProfileProbe(
			string? firstName,
			string? lastName,
			DateTimeOffset? dateOfBirth,
			string? parentGuardianName,
			Guid? targetStandardId,
			string? desiredClassName)
		{
			FirstName = firstName;
			LastName = lastName;
			DateOfBirth = dateOfBirth;
			ParentGuardianName = parentGuardianName;
			TargetStandardId = targetStandardId;
			DesiredClassName = desiredClassName;
		}

		public string? FirstName { get; }
		public string? LastName { get; }
		public DateTimeOffset? DateOfBirth { get; }
		public string? ParentGuardianName { get; }
		public Guid? TargetStandardId { get; }
		public string? DesiredClassName { get; }

		public static DuplicateProfileProbe FromLead(AdmissionInquiryLead lead)
		{
			return new DuplicateProfileProbe(
				NormalizeText(lead.FirstName),
				NormalizeText(lead.LastName),
				lead.DateOfBirth,
				NormalizeText(lead.ParentGuardianName),
				lead.TargetStandardId,
				NormalizeText(lead.DesiredClassName));
		}

		public bool HasMinimumProfile()
		{
			return
				FirstName != null &&
				(ParentGuardianName != null || DateOfBirth.HasValue) &&
				(TargetStandardId.HasValue || DesiredClassName != null);
		}
	}

	private static List<AdmissionApplicantStatusChecklistItemDto> BuildChecklist(
		AdmissionInquiryLead lead,
		AdmissionCrmEntranceExamRecord? exam)
	{
		return
		[
			new AdmissionApplicantStatusChecklistItemDto
			{
				Key = "inquiry-received",
				Label = "Inquiry received",
				Status = "Completed",
				Details = "Your inquiry has been recorded by the admissions team."
			},
			new AdmissionApplicantStatusChecklistItemDto
			{
				Key = "follow-up",
				Label = "Admissions follow-up",
				Status = HasReachedFollowUp(lead.Stage) ? "Completed" : "Current",
				Details = HasReachedFollowUp(lead.Stage)
					? "The admissions team has started processing the inquiry."
					: "The admissions team will review the inquiry and contact you."
			},
			new AdmissionApplicantStatusChecklistItemDto
			{
				Key = "entrance-exam-schedule",
				Label = "Entrance exam schedule",
				Status = exam is not null
					? "Completed"
					: lead.Stage == AdmissionInquiryStage.ExamReady
						? "Current"
						: "Pending",
				Details = exam is null
					? "Exam schedule details will appear here when available."
					: FormatExamSchedule(exam)
			},
			new AdmissionApplicantStatusChecklistItemDto
			{
				Key = "entrance-exam-result",
				Label = "Entrance exam result",
				Status = HasExamResult(exam)
					? "Completed"
					: lead.Stage == AdmissionInquiryStage.ExamCompleted
						? "Current"
						: "Pending",
				Details = HasExamResult(exam)
					? FormatExamResult(exam!)
					: "Result details will appear after the entrance exam is reviewed."
			},
			new AdmissionApplicantStatusChecklistItemDto
			{
				Key = "eligibility-decision",
				Label = "Eligibility decision",
				Status = HasEligibilityDecision(lead.Stage, exam) ? "Completed" : "Pending",
				Details = GetEligibilityDetails(lead.Stage, exam)
			}
		];
	}

	private static AdmissionApplicantStatusExamDto ToApplicantStatusExamDto(AdmissionCrmEntranceExamRecord exam)
	{
		return new AdmissionApplicantStatusExamDto
		{
			Scheduled = true,
			SlotName = exam.SlotNameSnapshot,
			ExamStartTime = exam.ExamStartTime,
			ExamEndTime = exam.ExamEndTime,
			RoomNo = exam.ExamRoomNo,
			AttendanceStatus = exam.AttendanceStatus,
			ExamStatus = exam.ExamStatus,
			TotalMarks = exam.TotalMarks,
			PassingMarks = exam.PassingMarks,
			ObtainedMarks = exam.ObtainedMarks,
			Percentage = exam.Percentage,
			MeritRank = exam.MeritRank,
			EligibilityDecision = exam.EligibilityDecision,
			EligibilityDecisionReason = exam.EligibilityDecisionReason
		};
	}

	private static bool IsApplicantVisibleTimelineEvent(AdmissionCrmTimelineEvent timelineEvent)
	{
		return timelineEvent.EventType is
			AdmissionCrmTimelineEventType.LeadCreated or
			AdmissionCrmTimelineEventType.PublicFormSubmitted or
			AdmissionCrmTimelineEventType.ExamScheduled or
			AdmissionCrmTimelineEventType.ExamAttendanceMarked or
			AdmissionCrmTimelineEventType.ExamResultRecorded or
			AdmissionCrmTimelineEventType.EligibilityDecided or
			AdmissionCrmTimelineEventType.ApplicantChecklistUpdated or
			AdmissionCrmTimelineEventType.OfferIssued or
			AdmissionCrmTimelineEventType.OfferAccepted or
			AdmissionCrmTimelineEventType.OfferDeclined or
			AdmissionCrmTimelineEventType.AdmissionRouteEvaluated or
			AdmissionCrmTimelineEventType.ActivationRequested or
			AdmissionCrmTimelineEventType.StudentActivated;
	}

	private static AdmissionApplicantStatusActivityDto ToApplicantTimelineActivity(
		AdmissionCrmTimelineEvent timelineEvent)
	{
		return timelineEvent.EventType switch
		{
			AdmissionCrmTimelineEventType.PublicFormSubmitted => new AdmissionApplicantStatusActivityDto
			{
				Type = "Inquiry",
				Title = "Inquiry submitted",
				Details = "Your admission inquiry was submitted successfully.",
				OccurredAt = timelineEvent.OccurredAt
			},
			AdmissionCrmTimelineEventType.LeadCreated => new AdmissionApplicantStatusActivityDto
			{
				Type = "Inquiry",
				Title = "Inquiry received",
				Details = "The admissions team has recorded your inquiry.",
				OccurredAt = timelineEvent.OccurredAt
			},
			AdmissionCrmTimelineEventType.ExamScheduled => new AdmissionApplicantStatusActivityDto
			{
				Type = "EntranceExam",
				Title = "Entrance exam scheduled",
				Details = "Entrance exam schedule details are available on this page.",
				OccurredAt = timelineEvent.OccurredAt
			},
			AdmissionCrmTimelineEventType.ExamAttendanceMarked => new AdmissionApplicantStatusActivityDto
			{
				Type = "EntranceExam",
				Title = "Entrance exam attendance updated",
				Details = "The school has recorded entrance exam attendance.",
				OccurredAt = timelineEvent.OccurredAt
			},
			AdmissionCrmTimelineEventType.ExamResultRecorded => new AdmissionApplicantStatusActivityDto
			{
				Type = "EntranceExam",
				Title = "Entrance exam result updated",
				Details = "Entrance exam result details are available on this page.",
				OccurredAt = timelineEvent.OccurredAt
			},
			AdmissionCrmTimelineEventType.ApplicantChecklistUpdated => new AdmissionApplicantStatusActivityDto
			{
				Type = "Checklist",
				Title = "Applicant checklist updated",
				Details = "The school has updated an applicant checklist item.",
				OccurredAt = timelineEvent.OccurredAt
			},
			AdmissionCrmTimelineEventType.OfferIssued => ApplicantActivity(
				"Offer", "Admission offer issued", "An admission offer is available for review.", timelineEvent.OccurredAt),
			AdmissionCrmTimelineEventType.OfferAccepted => ApplicantActivity(
				"Offer", "Admission offer accepted", "Your offer acceptance has been recorded.", timelineEvent.OccurredAt),
			AdmissionCrmTimelineEventType.OfferDeclined => ApplicantActivity(
				"Offer", "Admission offer declined", "Your offer response has been recorded.", timelineEvent.OccurredAt),
			AdmissionCrmTimelineEventType.AdmissionRouteEvaluated => ApplicantActivity(
				"AdmissionRoute", "Admission route reviewed", "The school has reviewed the applicable admission route.", timelineEvent.OccurredAt),
			AdmissionCrmTimelineEventType.ActivationRequested => ApplicantActivity(
				"Activation", "Student activation in progress", "Final academic placement and access setup are in progress.", timelineEvent.OccurredAt),
			AdmissionCrmTimelineEventType.StudentActivated => ApplicantActivity(
				"Activation", "Student ready to join", "Academic placement and required access setup are complete.", timelineEvent.OccurredAt),
			_ => new AdmissionApplicantStatusActivityDto
			{
				Type = "Eligibility",
				Title = "Eligibility decision updated",
				Details = "The admissions team has updated the eligibility decision.",
				OccurredAt = timelineEvent.OccurredAt
			}
		};
	}

	private static AdmissionApplicantStatusActivityDto ApplicantActivity(
		string type, string title, string details, DateTimeOffset occurredAt) => new()
	{
		Type = type,
		Title = title,
		Details = details,
		OccurredAt = occurredAt
	};

	private static AdmissionApplicantStatusActivityDto ToApplicantMessageActivity(
		AdmissionCrmMessageDeliveryLog log)
	{
		return new AdmissionApplicantStatusActivityDto
		{
			Type = "Message",
			Title = $"{log.Channel} message {GetApplicantMessageStatusLabel(log.Status)}",
			Details = GetApplicantMessageStatusDetails(log.Status),
			OccurredAt = ToDateTimeOffset(log.DeliveredAt ?? log.SentAt ?? log.LastAttemptAt ?? log.CreatedAt)
		};
	}

	private static string GetApplicantMessageStatusLabel(string status)
	{
		return status switch
		{
			AdmissionCrmMessageDeliveryLog.DeliveredStatus => "delivered",
			AdmissionCrmMessageDeliveryLog.SentStatus => "sent",
			AdmissionCrmMessageDeliveryLog.QueuedStatus => "queued",
			AdmissionCrmMessageDeliveryLog.FailedStatus => "failed",
			AdmissionCrmMessageDeliveryLog.BlockedByOptOutStatus => "not sent",
			AdmissionCrmMessageDeliveryLog.ProviderNotConfiguredStatus => "recorded",
			_ => "updated"
		};
	}

	private static string GetApplicantMessageStatusDetails(string status)
	{
		return status switch
		{
			AdmissionCrmMessageDeliveryLog.DeliveredStatus => "The school message was delivered.",
			AdmissionCrmMessageDeliveryLog.SentStatus => "The school message was sent.",
			AdmissionCrmMessageDeliveryLog.QueuedStatus => "The school message is queued for sending.",
			AdmissionCrmMessageDeliveryLog.FailedStatus => "The school message could not be delivered.",
			AdmissionCrmMessageDeliveryLog.BlockedByOptOutStatus => "The message was not sent because this contact is opted out.",
			AdmissionCrmMessageDeliveryLog.ProviderNotConfiguredStatus => "The school recorded a message update.",
			_ => "The school message status changed."
		};
	}

	private static DateTimeOffset ToDateTimeOffset(DateTime value)
	{
		return value.Kind switch
		{
			DateTimeKind.Utc => new DateTimeOffset(value),
			DateTimeKind.Local => new DateTimeOffset(value).ToUniversalTime(),
			_ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))
		};
	}

	private static bool HasReachedFollowUp(AdmissionInquiryStage stage)
	{
		return stage is AdmissionInquiryStage.FollowUp
			or AdmissionInquiryStage.ExamReady
			or AdmissionInquiryStage.ExamScheduled
			or AdmissionInquiryStage.ExamCompleted
			or AdmissionInquiryStage.Eligible
			or AdmissionInquiryStage.Waitlisted
			or AdmissionInquiryStage.Rejected;
	}

	private static bool HasExamResult(AdmissionCrmEntranceExamRecord? exam)
	{
		return exam is not null &&
		       (exam.ResultRecordedAt.HasValue ||
		        exam.AttendanceStatus == AdmissionCrmExamAttendanceStatus.Absent);
	}

	private static bool HasEligibilityDecision(
		AdmissionInquiryStage stage,
		AdmissionCrmEntranceExamRecord? exam)
	{
		return stage is AdmissionInquiryStage.Eligible
			       or AdmissionInquiryStage.Waitlisted
			       or AdmissionInquiryStage.Rejected ||
		       exam is { EligibilityDecision: not AdmissionCrmEligibilityDecision.Pending };
	}

	private static string FormatExamSchedule(AdmissionCrmEntranceExamRecord exam)
	{
		var slotName = string.IsNullOrWhiteSpace(exam.SlotNameSnapshot)
			? "Entrance exam"
			: exam.SlotNameSnapshot;
		return $"{slotName} on {exam.ExamStartTime:dd MMM yyyy, h:mm tt} in room {exam.ExamRoomNo}.";
	}

	private static string FormatExamResult(AdmissionCrmEntranceExamRecord exam)
	{
		if (exam.AttendanceStatus == AdmissionCrmExamAttendanceStatus.Absent)
		{
			return "The applicant was marked absent for the entrance exam.";
		}

		if (!exam.ObtainedMarks.HasValue || !exam.TotalMarks.HasValue)
		{
			return "The entrance exam result is being reviewed.";
		}

		var percentage = exam.Percentage.HasValue
			? $" ({exam.Percentage.Value:0.##}%)"
			: string.Empty;
		var rank = exam.MeritRank.HasValue
			? $" Merit rank: {exam.MeritRank.Value}."
			: string.Empty;
		return $"{exam.ExamStatus}: {exam.ObtainedMarks.Value:0.##}/{exam.TotalMarks.Value:0.##}{percentage}.{rank}";
	}

	private static string GetEligibilityDetails(
		AdmissionInquiryStage stage,
		AdmissionCrmEntranceExamRecord? exam)
	{
		if (exam is { EligibilityDecision: not AdmissionCrmEligibilityDecision.Pending })
		{
			return exam.EligibilityDecisionReason ??
			       $"Decision: {exam.EligibilityDecision}.";
		}

		return stage switch
		{
			AdmissionInquiryStage.Eligible => "The applicant is eligible for admission handoff.",
			AdmissionInquiryStage.Waitlisted => "The applicant is currently waitlisted.",
			AdmissionInquiryStage.Rejected => "The applicant is not eligible at this stage.",
			_ => "Eligibility will be shown after entrance exam review."
		};
	}

	private static string GetNextStep(AdmissionInquiryStage stage)
	{
		return stage switch
		{
			AdmissionInquiryStage.NewInquiry => "The admissions team will review the inquiry and follow up.",
			AdmissionInquiryStage.FollowUp => "The admissions team is following up on the inquiry.",
			AdmissionInquiryStage.ExamReady => "The inquiry is ready for entrance exam scheduling.",
			AdmissionInquiryStage.ExamScheduled => "The entrance exam has been scheduled.",
			AdmissionInquiryStage.ExamCompleted => "The entrance exam result is being reviewed.",
			AdmissionInquiryStage.Eligible => "The applicant is eligible for admission handoff.",
			AdmissionInquiryStage.Waitlisted => "The applicant is currently waitlisted.",
			AdmissionInquiryStage.Rejected => "The applicant is not eligible at this stage.",
			AdmissionInquiryStage.Stale => "The inquiry needs renewed follow-up.",
			AdmissionInquiryStage.Archived => "The inquiry is archived.",
			_ => "The admissions team will share the next step."
		};
	}

	private static string NormalizeCallbackStatus(string status)
	{
		return status.Trim().ToLowerInvariant() switch
		{
			"queued" => AdmissionCrmMessageDeliveryLog.QueuedStatus,
			"sent" => AdmissionCrmMessageDeliveryLog.SentStatus,
			"delivered" => AdmissionCrmMessageDeliveryLog.DeliveredStatus,
			"failed" => AdmissionCrmMessageDeliveryLog.FailedStatus,
			_ => throw new BusinessException("AdmissionCrm:DeliveryCallbackStatusInvalid")
				.WithData("Status", status)
		};
	}

	private static string BuildCallbackStatusDetails(
		string status,
		string? statusDetails,
		string? providerMessageId)
	{
		var details = string.IsNullOrWhiteSpace(statusDetails)
			? $"Provider callback marked delivery as {status}."
			: statusDetails.Trim();

		if (!string.IsNullOrWhiteSpace(providerMessageId))
		{
			details = $"{details} Provider message id: {providerMessageId.Trim()}.";
		}

		return details.Length <= AdmissionCrmConsts.MaxNotesLength
			? details
			: details[..AdmissionCrmConsts.MaxNotesLength];
	}

	private static ResolvedDeliveryCallback ResolveDeliveryCallback(
		string? settingsJson,
		ApplyAdmissionCrmDeliveryCallbackDto input)
	{
		var mappings = GetCallbackMappings(settingsJson);
		var status = NormalizeOptionalCallbackValue(input.Status) ??
		             ExtractPayloadString(input.Payload, mappings.StatusPath);
		if (string.IsNullOrWhiteSpace(status))
		{
			throw new BusinessException("AdmissionCrm:DeliveryCallbackStatusRequired");
		}

		var callbackSecret = NormalizeOptionalCallbackValue(input.CallbackSecret) ??
		                     ExtractPayloadString(input.Payload, mappings.CallbackSecretPath);
		if (string.IsNullOrWhiteSpace(callbackSecret))
		{
			throw new BusinessException("AdmissionCrm:DeliveryCallbackSecretRequired");
		}

		return new ResolvedDeliveryCallback(
			status,
			NormalizeOptionalCallbackValue(input.StatusDetails) ??
			ExtractPayloadString(input.Payload, mappings.StatusDetailsPath),
			NormalizeOptionalCallbackValue(input.ProviderMessageId) ??
			ExtractPayloadString(input.Payload, mappings.ProviderMessageIdPath),
			callbackSecret);
	}

	private static AdmissionCrmCallbackMappings GetCallbackMappings(string? settingsJson)
	{
		if (string.IsNullOrWhiteSpace(settingsJson))
		{
			return new AdmissionCrmCallbackMappings();
		}

		using var parsed = JsonDocument.Parse(settingsJson);
		if (!parsed.RootElement.TryGetProperty("callbackMappings", out var mappings) ||
		    mappings.ValueKind != JsonValueKind.Object)
		{
			return new AdmissionCrmCallbackMappings();
		}

		return new AdmissionCrmCallbackMappings(
			GetOptionalStringProperty(mappings, "statusPath"),
			GetOptionalStringProperty(mappings, "statusDetailsPath"),
			GetOptionalStringProperty(mappings, "providerMessageIdPath"),
			GetOptionalStringProperty(mappings, "callbackSecretPath"));
	}

	private static string? ExtractPayloadString(
		IReadOnlyDictionary<string, JsonElement> payload,
		string? path)
	{
		if (payload.Count == 0 || string.IsNullOrWhiteSpace(path))
		{
			return null;
		}

		var normalizedPath = path.Trim();
		if (normalizedPath.StartsWith("$.", StringComparison.Ordinal))
		{
			normalizedPath = normalizedPath[2..];
		}

		JsonElement current;
		var segments = normalizedPath
			.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		if (segments.Length == 0 || !TryGetPayloadRoot(payload, segments[0], out current))
		{
			return null;
		}

		foreach (var segment in segments.Skip(1))
		{
			if (!TryApplyPathSegment(current, segment, out current))
			{
				return null;
			}
		}

		return JsonElementToString(current);
	}

	private static bool TryGetPayloadRoot(
		IReadOnlyDictionary<string, JsonElement> payload,
		string segment,
		out JsonElement value)
	{
		if (payload.TryGetValue(segment, out value))
		{
			return true;
		}

		foreach (var item in payload)
		{
			if (string.Equals(item.Key, segment, StringComparison.OrdinalIgnoreCase))
			{
				value = item.Value;
				return true;
			}
		}

		value = default;
		return false;
	}

	private static bool TryApplyPathSegment(
		JsonElement current,
		string segment,
		out JsonElement next)
	{
		var propertyName = segment;
		int? index = null;
		var bracketIndex = segment.IndexOf('[', StringComparison.Ordinal);
		if (bracketIndex >= 0 && segment.EndsWith(']'))
		{
			propertyName = segment[..bracketIndex];
			if (int.TryParse(segment[(bracketIndex + 1)..^1], out var parsedIndex))
			{
				index = parsedIndex;
			}
		}

		next = current;
		if (!string.IsNullOrWhiteSpace(propertyName) &&
		    !TryGetJsonProperty(current, propertyName, out next))
		{
			return false;
		}

		if (!index.HasValue)
		{
			return true;
		}

		if (next.ValueKind != JsonValueKind.Array || index.Value < 0 || index.Value >= next.GetArrayLength())
		{
			next = default;
			return false;
		}

		next = next.EnumerateArray().ElementAt(index.Value);
		return true;
	}

	private static bool TryGetJsonProperty(
		JsonElement element,
		string propertyName,
		out JsonElement value)
	{
		if (element.ValueKind == JsonValueKind.Object &&
		    element.TryGetProperty(propertyName, out value))
		{
			return true;
		}

		if (element.ValueKind == JsonValueKind.Object)
		{
			foreach (var property in element.EnumerateObject())
			{
				if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
				{
					value = property.Value;
					return true;
				}
			}
		}

		value = default;
		return false;
	}

	private static string? JsonElementToString(JsonElement element)
	{
		return element.ValueKind switch
		{
			JsonValueKind.String => element.GetString(),
			JsonValueKind.Number => element.GetRawText(),
			JsonValueKind.True => "true",
			JsonValueKind.False => "false",
			JsonValueKind.Null => null,
			JsonValueKind.Undefined => null,
			_ => element.GetRawText()
		};
	}

	private static string? NormalizeOptionalCallbackValue(string? value)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: value.Trim();
	}

	private static string? NormalizeProviderMessageId(string? providerMessageId)
	{
		return string.IsNullOrWhiteSpace(providerMessageId)
			? null
			: Check.Length(
				providerMessageId.Trim(),
				nameof(providerMessageId),
				AdmissionCrmConsts.MaxProviderMessageIdLength);
	}

	private static string? GetOptionalStringProperty(JsonElement element, string propertyName)
	{
		return element.TryGetProperty(propertyName, out var value) &&
		       value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;
	}

	private static string? GetCallbackSecret(string? settingsJson)
	{
		if (string.IsNullOrWhiteSpace(settingsJson))
		{
			return null;
		}

		using var parsed = JsonDocument.Parse(settingsJson);
		return parsed.RootElement.TryGetProperty("callbackSecret", out var secret) &&
		       secret.ValueKind == JsonValueKind.String
			? secret.GetString()
			: null;
	}

	private static bool SecretsEqual(string expected, string provided)
	{
		if (string.IsNullOrWhiteSpace(provided))
		{
			return false;
		}

		var expectedBytes = Encoding.UTF8.GetBytes(expected.Trim());
		var providedBytes = Encoding.UTF8.GetBytes(provided.Trim());
		return expectedBytes.Length == providedBytes.Length &&
		       CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
	}

	private sealed record AdmissionCrmCallbackMappings(
		string? StatusPath = null,
		string? StatusDetailsPath = null,
		string? ProviderMessageIdPath = null,
		string? CallbackSecretPath = null);

	private sealed record ResolvedDeliveryCallback(
		string Status,
		string? StatusDetails,
		string? ProviderMessageId,
		string CallbackSecret);

	private static AdmissionCrmMessageDeliveryLogDto ToDeliveryLogDto(
		AdmissionCrmMessageDeliveryLog log)
	{
		return new AdmissionCrmMessageDeliveryLogDto
		{
			Id = log.Id,
			AcademicYearId = log.AcademicYearId,
			LeadId = log.LeadId,
			TemplateId = log.TemplateId,
			Channel = log.Channel,
			Recipient = log.Recipient,
			NormalizedRecipient = log.NormalizedRecipient,
			ProviderName = log.ProviderName,
			ProviderMessageId = log.ProviderMessageId,
			SubjectSnapshot = log.SubjectSnapshot,
			BodySnapshot = log.BodySnapshot,
			Status = log.Status,
			StatusDetails = log.StatusDetails,
			AttemptCount = log.AttemptCount,
			CreatedAt = log.CreatedAt,
			LastAttemptAt = log.LastAttemptAt,
			SentAt = log.SentAt,
			DeliveredAt = log.DeliveredAt
		};
	}

	private sealed record StatusOtpDeliveryCandidate(
		AdmissionCrmMessageChannel Channel,
		string Recipient);

	private sealed record StatusOtpDeliveryResult(
		bool DeliveryConfigured,
		string DeliveryStatus);
}
