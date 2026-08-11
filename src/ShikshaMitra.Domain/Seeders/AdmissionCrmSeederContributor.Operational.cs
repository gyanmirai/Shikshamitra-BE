using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Seeders;

public partial class AdmissionCrmSeederContributor
{
	private static DateTime SeededAt => DateTime.UtcNow.Date.AddHours(9);
	private Guid _seedRequesterId;
	private Guid _seedApproverId;

	private IRepository<TEntity, Guid> Repository<TEntity>() where TEntity : class, Volo.Abp.Domain.Entities.IEntity<Guid> =>
		_serviceProvider.GetRequiredService<IRepository<TEntity, Guid>>();

	private async Task<ExtendedConfiguration> RepairExtendedConfigurationAsync(
		Guid tenantId,
		Guid academicYearId,
		string academicYearName)
	{
		var playbookRepository = Repository<AdmissionCrmPlaybook>();
		var playbookId = StableId(tenantId, "playbook:default");
		var playbook = await playbookRepository.FindAsync(playbookId);
		if (playbook == null)
		{
			playbook = new AdmissionCrmPlaybook(
				playbookId, tenantId, academicYearId, $"DDVS admissions {academicYearName}", 1, true,
				AdmissionCrmRegistrationPath.RegistrationAfterExam,
				"{}",
				"[{\"code\":\"INQUIRY\",\"displayName\":\"Inquiry\",\"reportingMilestone\":0,\"order\":10,\"isRequired\":true,\"isParentVisible\":true,\"slaHours\":4,\"allowedNextStageCodes\":[\"EXAM_READY\"]},{\"code\":\"EXAM_READY\",\"displayName\":\"Exam ready\",\"reportingMilestone\":2,\"order\":20,\"isRequired\":true,\"isParentVisible\":true,\"slaHours\":24,\"allowedNextStageCodes\":[\"EXAM_SCHEDULED\"]},{\"code\":\"EXAM_SCHEDULED\",\"displayName\":\"Exam scheduled\",\"reportingMilestone\":3,\"order\":30,\"isRequired\":true,\"isParentVisible\":true,\"allowedNextStageCodes\":[\"EXAM_COMPLETED\"]},{\"code\":\"EXAM_COMPLETED\",\"displayName\":\"Exam completed\",\"reportingMilestone\":4,\"order\":40,\"isRequired\":true,\"isParentVisible\":true,\"allowedNextStageCodes\":[\"ELIGIBLE\"]},{\"code\":\"ELIGIBLE\",\"displayName\":\"Eligible\",\"reportingMilestone\":5,\"order\":50,\"isRequired\":true,\"isParentVisible\":true,\"allowedNextStageCodes\":[]}]",
				"[{\"code\":\"MAIN\",\"name\":\"Main admission round\",\"openingDate\":\"2026-01-01T00:00:00Z\",\"closingDate\":\"2027-03-31T00:00:00Z\",\"isActive\":true}]",
				"[{\"code\":\"GENERAL-PRIMARY\",\"roundCode\":\"MAIN\",\"campus\":\"Main Campus\",\"desiredClass\":\"Class 4 / Class 5\",\"board\":\"CBSE\",\"medium\":\"English\",\"plannedSeats\":40,\"approvedCapacity\":40,\"offeredSeats\":0,\"acceptedSeats\":0,\"waitlistedApplicants\":0,\"withdrawnApplicants\":0,\"remainingCapacity\":40,\"isOverAllocated\":false}]",
				"[{\"code\":\"GENERAL\",\"name\":\"General entrance exam\",\"roundCode\":\"MAIN\",\"campus\":\"Main Campus\",\"durationMinutes\":120,\"instructions\":\"Report 30 minutes before the exam.\",\"components\":[{\"code\":\"APTITUDE\",\"name\":\"Aptitude\",\"componentType\":0,\"maximumMarks\":100,\"minimumMarks\":40,\"weightPercentage\":100,\"isMandatory\":true,\"scorePrecision\":0}],\"resultRule\":{\"aggregatePassingPercentage\":40,\"requireMandatoryComponentPass\":true,\"meritCapacity\":30,\"waitlistCapacity\":10,\"tieBreakerCodes\":[\"APTITUDE\"]}}]",
				"[{\"code\":\"GENERAL-MORNING\",\"name\":\"General morning slot\",\"examDefinitionCode\":\"GENERAL\",\"campus\":\"Main Campus\",\"roomOrHall\":\"Hall A\",\"roomNo\":101,\"capacity\":40,\"reportingTime\":\"08:30:00\",\"startTime\":\"09:00:00\",\"endTime\":\"11:00:00\",\"daysOfWeek\":[1,3,5],\"blackoutDates\":[],\"isActive\":true}]",
				"[{\"code\":\"EXAM-READY-EARLIEST\",\"name\":\"Assign earliest available slot\",\"trigger\":\"ExamReady\",\"strategy\":2,\"priority\":10,\"slotTemplateCode\":\"GENERAL-MORNING\",\"isActive\":true}]",
				"Deterministic DDVS playbook for browser and API validation.");
			playbook.Publish(SeededAt);
			await playbookRepository.InsertAsync(playbook);
		}

		var registrationRepository = Repository<AdmissionCrmRegistrationTemplate>();
		var registrationTemplateId = StableId(tenantId, "registration-template:default");
		var registrationTemplate = await registrationRepository.FindAsync(registrationTemplateId);
		if (registrationTemplate == null)
		{
			registrationTemplate = new AdmissionCrmRegistrationTemplate(
				registrationTemplateId, tenantId, academicYearId, "DDVS registration form", 1, true, "{}",
				DefaultRegistrationSectionsJson(),
				14, true, false, 0, AdmissionInquiryStage.Eligible,
				"English, Gujarati, and Hindi labels are supplied through the applicant surface.");
			registrationTemplate.Publish(SeededAt);
			await registrationRepository.InsertAsync(registrationTemplate);
		}

		var checklistRepository = Repository<AdmissionCrmDocumentChecklistTemplate>();
		var checklistId = StableId(tenantId, "document-checklist:default");
		var checklist = await checklistRepository.FindAsync(checklistId);
		if (checklist == null)
		{
			checklist = new AdmissionCrmDocumentChecklistTemplate(
				checklistId, tenantId, academicYearId, "DDVS admission documents", 1, true, "{}",
				$"[{{\"key\":\"birthCertificate\",\"documentTypeId\":\"{StableId(tenantId, "document:BIRTH_CERT")}\",\"name\":\"Birth certificate\",\"requirementLevel\":0,\"blocksAdmissionReadiness\":true,\"requiresOriginal\":true,\"validityDateRequired\":false,\"maximumSizeMb\":5,\"acceptedExtensions\":[\"pdf\",\"jpg\",\"jpeg\",\"png\"],\"displayOrder\":10}},{{\"key\":\"addressProof\",\"documentTypeId\":\"{StableId(tenantId, "document:ADDRESS_PROOF")}\",\"name\":\"Address proof\",\"requirementLevel\":0,\"blocksAdmissionReadiness\":true,\"requiresOriginal\":true,\"validityDateRequired\":false,\"maximumSizeMb\":5,\"acceptedExtensions\":[\"pdf\",\"jpg\",\"jpeg\",\"png\"],\"displayOrder\":20}}]",
				1, "Deterministic admission-readiness checklist.");
			checklist.Publish(SeededAt);
			await checklistRepository.InsertAsync(checklist);
		}

		await RepairInquiryFormsAsync(tenantId, academicYearId);
		await RepairMessageTemplatesAsync(tenantId, academicYearId);

		var formSeriesRepository = Repository<AdmissionCrmFormNumberSeries>();
		var formSeriesId = StableId(tenantId, "form-number-series:default");
		var formSeries = await formSeriesRepository.FindAsync(formSeriesId);
		if (formSeries == null)
		{
			formSeries = new AdmissionCrmFormNumberSeries(
				formSeriesId, tenantId, academicYearId, "DDVS registration numbers", "DDVS-REG-", string.Empty,
				5, 1, "{}", true);
			await formSeriesRepository.InsertAsync(formSeries);
		}

		var seatPoolRepository = Repository<AdmissionCrmSeatPool>();
		var seatPoolId = StableId(tenantId, "seat-pool:general-1");
		var seatPool = await seatPoolRepository.FindAsync(seatPoolId);
		if (seatPool == null)
		{
			seatPool = new AdmissionCrmSeatPool(
				seatPoolId, tenantId, academicYearId, playbook.Id, "GENERAL-1", "Primary admission seats", "MAIN",
				"Main Campus", "Class 4 / Class 5", "CBSE", "English", "General", 32, 40);
			await seatPoolRepository.InsertAsync(seatPool);
		}
		else
		{
			seatPool.UpdateDefinition(
				playbook.Id,
				"Primary admission seats",
				"MAIN",
				"Main Campus",
				"Class 4 / Class 5",
				"CBSE",
				"English",
				"General",
				32,
				40,
				true);
			await seatPoolRepository.UpdateAsync(seatPool);
		}

		return new ExtendedConfiguration(playbook, registrationTemplate, checklist, formSeries, seatPool);
	}

	private async Task RepairInquiryFormsAsync(Guid tenantId, Guid academicYearId)
	{
		var repository = Repository<AdmissionInquiryFormTemplate>();
		var fields = "[{\"key\":\"studentFirstName\",\"label\":\"Student first name\",\"fieldType\":0,\"systemField\":\"FirstName\",\"required\":true,\"displayOrder\":10,\"options\":[]},{\"key\":\"parentPhone\",\"label\":\"Parent mobile number\",\"fieldType\":2,\"systemField\":\"PrimaryContactNo\",\"required\":true,\"displayOrder\":20,\"options\":[]},{\"key\":\"admissionIntent\",\"label\":\"Admission intent\",\"fieldType\":1,\"systemField\":\"AdmissionIntent\",\"required\":true,\"displayOrder\":30,\"options\":[]},{\"key\":\"desiredClass\",\"label\":\"Desired class\",\"fieldType\":0,\"systemField\":\"DesiredClassName\",\"required\":true,\"displayOrder\":40,\"options\":[]}]";
		var localizations = "{\"en\":{\"templateName\":\"Admission inquiry\",\"consentText\":\"I agree that the school may contact me about admission.\"},\"gu\":{\"templateName\":\"Pravesh inquiry\",\"consentText\":\"Hu shala ne pravesh ange sampark karva sammati apu chhu.\"},\"hi\":{\"templateName\":\"Pravesh inquiry\",\"consentText\":\"Main school ko admission ke sambandh mein sampark karne ki sahmati deta/deti hoon.\"}}";
		foreach (var spec in new[]
		         {
			         (Key: "website", Name: "Public website inquiry", Channel: AdmissionInquiryChannel.SchoolWebsiteForm),
			         (Key: "quick-call", Name: "Quick call inquiry", Channel: AdmissionInquiryChannel.QuickCallEntry),
			         (Key: "campaign", Name: "Campaign inquiry", Channel: AdmissionInquiryChannel.CampaignLink)
		         })
		{
			var id = StableId(tenantId, $"inquiry-form:{spec.Key}");
			if (await repository.AnyAsync(item => item.Id == id)) continue;
			await repository.InsertAsync(new AdmissionInquiryFormTemplate(
				id, tenantId, academicYearId, spec.Name, "Deterministic multilingual DDVS inquiry form.", spec.Channel,
				true, "I agree that the school may contact me about this admission inquiry.",
				"https://ddvs.example/privacy", fields, localizations));
		}
	}

	private async Task RepairMessageTemplatesAsync(Guid tenantId, Guid academicYearId)
	{
		var repository = Repository<AdmissionCrmMessageTemplate>();
		foreach (var channel in Enum.GetValues<AdmissionCrmMessageChannel>())
		{
			var id = StableId(tenantId, $"message-template:exam-reminder:{channel}");
			if (await repository.AnyAsync(item => item.Id == id)) continue;
			var template = new AdmissionCrmMessageTemplate(
				id, tenantId, academicYearId, $"Entrance exam reminder - {channel}", channel, "Entrance exam reminder",
				channel == AdmissionCrmMessageChannel.Email ? "Your entrance exam schedule" : null,
				"Hello {{applicantName}}, your entrance exam is scheduled for {{examDate}} at {{examTime}}.",
				"[\"applicantName\",\"examDate\",\"examTime\"]");
			template.UpdateStatus(AdmissionCrmMessageTemplateStatus.Approved, true);
			await repository.InsertAsync(template);
		}

		var registrationInvitationId = StableId(tenantId, "message-template:registration-invitation:email");
		if (!await repository.AnyAsync(item => item.Id == registrationInvitationId))
		{
			var registrationInvitation = new AdmissionCrmMessageTemplate(
				registrationInvitationId,
				tenantId,
				academicYearId,
				"Registration invitation - Email",
				AdmissionCrmMessageChannel.Email,
				"Registration invitation",
				"Complete your admission registration",
				"Hello {{applicantName}}, your registration form is ready. Complete it securely at {{registrationLink}}.",
				"[\"applicantName\",\"registrationLink\"]");
			registrationInvitation.UpdateStatus(AdmissionCrmMessageTemplateStatus.Approved, true);
			await repository.InsertAsync(registrationInvitation);
		}
	}

	private async Task RepairOperationalScenariosAsync(
		Guid tenantId,
		Guid academicYearId,
		ExtendedConfiguration configuration,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads,
		IReadOnlyList<AdmissionCrmExamSlot> slots,
		IReadOnlyDictionary<string, Guid> ownerUserIds,
		IReadOnlyDictionary<string, Standard> standards)
	{
		_seedRequesterId = ownerUserIds["admissions-reception@ddvs.com"];
		_seedApproverId = ownerUserIds["admissions-approver@ddvs.com"];
		_logger.LogInformation(
			"[AdmissionCrmSeeder] Operational prerequisites loaded. Leads={LeadCount}, Slots={SlotCount}.",
			leads.Count,
			slots.Count);
		if (slots.Count == 0)
		{
			_logger.LogWarning("[AdmissionCrmSeeder] Exam scenarios skipped because no active exam slots were visible.");
		}
		else
		{
			var examRepository = Repository<AdmissionCrmEntranceExamRecord>();
			foreach (var spec in ExamScenarioSpecs())
			{
				var leadId = StableId(tenantId, $"lead:{spec.Key}");
				if (!leads.ContainsKey(leadId)) continue;
				var examId = StableId(tenantId, $"exam:{spec.Key}");
				if (await examRepository.AnyAsync(item => item.Id == examId)) continue;
				var exam = new AdmissionCrmEntranceExamRecord(
					examId, tenantId, academicYearId, leadId, slots[spec.SlotIndex % slots.Count], false, null, SeededAt.AddDays(spec.SlotIndex));
				if (spec.Absent)
				{
					exam.MarkAttendance(AdmissionCrmExamAttendanceStatus.Absent, SeededAt.AddDays(spec.SlotIndex + 1));
				}
				else if (spec.Marks.HasValue)
				{
					exam.MarkAttendance(AdmissionCrmExamAttendanceStatus.Present, SeededAt.AddDays(spec.SlotIndex + 1));
					exam.RecordResult(100, 40, spec.Marks.Value, spec.MeritRank, SeededAt.AddDays(spec.SlotIndex + 1));
					if (spec.Decision == AdmissionCrmEligibilityDecision.Waitlisted)
					{
						exam.SetEligibilityDecision(AdmissionCrmEligibilityDecision.Waitlisted, "Applicant placed on the seeded waitlist.", false, null, spec.MeritRank, SeededAt.AddDays(spec.SlotIndex + 1));
					}
				}
				await examRepository.InsertAsync(exam);
				await EnsureTimelineAsync(tenantId, academicYearId, leadId, $"exam:{spec.Key}", AdmissionCrmTimelineEventType.ExamScheduled,
					"Entrance exam scheduled", $"Seeded exam scenario: {spec.Key}.", exam.Id);
			}
		}

		await RepairRegistrationScenariosAsync(tenantId, academicYearId, configuration, leads);
		await RepairOfferAndWaitlistScenariosAsync(tenantId, academicYearId, configuration, leads);
		await RepairRouteScenariosAsync(tenantId, academicYearId, leads);
		await RepairActivationScenariosAsync(tenantId, academicYearId, leads, standards);
	}

	private async Task RepairRegistrationScenariosAsync(
		Guid tenantId,
		Guid academicYearId,
		ExtendedConfiguration configuration,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var applicationRepository = Repository<AdmissionCrmRegistrationApplication>();
		var assignmentRepository = Repository<AdmissionCrmDocumentChecklistAssignment>();
		var documentRepository = Repository<AdmissionCrmApplicantDocument>();
		var keys = DemoRegistrationKeys();
		for (var index = 0; index < keys.Count; index++)
		{
			var key = keys[index];
			var leadId = StableId(tenantId, $"lead:{key}");
			if (!leads.ContainsKey(leadId)) continue;
			var applicationId = StableId(tenantId, $"registration:{key}");
			var application = await applicationRepository.FindAsync(applicationId);
			if (application == null)
			{
				application = new AdmissionCrmRegistrationApplication(
					applicationId, tenantId, academicYearId, leadId, configuration.RegistrationTemplate.Id, 1,
					configuration.Playbook.Id, $"DDVS-REG-{index + 1:00000}", "{\"template\":\"DDVS registration form\"}",
					$"{{\"scenario\":\"{key}\"}}", SeededAt, SeededAt.AddDays(30), 0);
				var completionPercentage = key == "passed" ? 60 : 100;
				application.Save("{\"firstName\":\"Seeded applicant\",\"parentName\":\"Seeded parent\"}", completionPercentage, SeededAt.AddHours(1));
				if (key != "passed")
				{
					application.Submit("{\"firstName\":\"Seeded applicant\",\"parentName\":\"Seeded parent\"}", SeededAt.AddHours(2), key is "rte-allotment" or "activation-repair");
					if (key == "registration-correction")
					{
						application.ReturnForCorrection("[{\"fieldKey\":\"dateOfBirth\",\"message\":\"Upload a matching birth certificate.\"}]", "Date of birth evidence does not match.");
					}
					else
					{
						application.Verify(SeededAt.AddHours(3));
						application.MarkAdmissionReady();
					}
				}
				await applicationRepository.InsertAsync(application);
			}

			if (key is "passed" or "registration-correction") continue;
			var assignmentId = StableId(tenantId, $"document-assignment:{key}");
			var assignment = await assignmentRepository.FindAsync(assignmentId);
			if (assignment == null)
			{
				assignment = new AdmissionCrmDocumentChecklistAssignment(
					assignmentId, tenantId, academicYearId, leadId, application.Id, configuration.Checklist.Id, 1,
					"{\"requirements\":[\"birthCertificate\",\"addressProof\"]}", SeededAt.AddHours(3));
				assignment.ConfirmReadiness(_seedApproverId, 1, false, null, SeededAt.AddHours(5));
				await assignmentRepository.InsertAsync(assignment);
			}

			var documentId = StableId(tenantId, $"applicant-document:{key}:birth");
			if (!await documentRepository.AnyAsync(item => item.Id == documentId))
			{
				var document = new AdmissionCrmApplicantDocument(
					documentId, tenantId, academicYearId, leadId, assignment.Id, "birthCertificate", 1,
					$"seed/admissions/{key}/birth-certificate.pdf", "birth-certificate.pdf", "application/pdf", 1024,
					new string('a', 64), null, SeededAt.AddHours(4));
				document.Verify(_seedApproverId, SeededAt.AddHours(5));
				await documentRepository.InsertAsync(document);
			}
		}

		var minimumNextNumber = keys.Count + 1L;
		if (configuration.FormSeries.NextNumber < minimumNextNumber)
		{
			configuration.FormSeries.Update(
				configuration.FormSeries.Name,
				configuration.FormSeries.Prefix,
				configuration.FormSeries.Suffix,
				configuration.FormSeries.Padding,
				minimumNextNumber,
				configuration.FormSeries.ApplicabilityJson,
				configuration.FormSeries.IsActive);
			await Repository<AdmissionCrmFormNumberSeries>().UpdateAsync(configuration.FormSeries);
		}
	}

	private async Task RepairOfferAndWaitlistScenariosAsync(
		Guid tenantId,
		Guid academicYearId,
		ExtendedConfiguration configuration,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var offerRepository = Repository<AdmissionCrmSeatOffer>();
		var reservationRepository = Repository<AdmissionCrmSeatReservation>();
		var waitlistRepository = Repository<AdmissionCrmWaitlistEntry>();
		foreach (var key in DemoOfferKeys())
		{
			var leadId = StableId(tenantId, $"lead:{key}");
			if (!leads.ContainsKey(leadId)) continue;
			var offerId = StableId(tenantId, $"offer:{key}");
			if (await offerRepository.AnyAsync(item => item.Id == offerId)) continue;
			var registrationId = StableId(tenantId, $"registration:{key}");
			var offer = new AdmissionCrmSeatOffer(
				offerId, tenantId, academicYearId, leadId, configuration.SeatPool.Id, registrationId,
				configuration.Playbook.Id, null, $"DDVS-OFFER-{key.ToUpperInvariant()}", 1,
				AdmissionCrmSeatOfferType.Provisional, "{\"paymentGate\":\"ReferenceRecorded\"}", "en",
				SeededAt, SeededAt.AddDays(14));
			offer.SubmitForApproval(_seedRequesterId, SeededAt.AddHours(1));
			if (key != "offer-pending")
			{
				offer.Approve(_seedApproverId, SeededAt.AddHours(2));
				var reservationId = StableId(tenantId, $"reservation:{key}");
				configuration.SeatPool.ReserveSeat(false, null);
				var reservation = new AdmissionCrmSeatReservation(
					reservationId, tenantId, academicYearId, leadId, configuration.SeatPool.Id, offer.Id,
					SeededAt.AddHours(2), SeededAt.AddDays(14), false, null);
				offer.Issue(reservation.Id, SeededAt.AddHours(3));
				offer.Accept("ApplicantStatusLink", SeededAt.AddHours(4));
				reservation.Commit(SeededAt.AddHours(4));
				configuration.SeatPool.AcceptReservedSeat();
				await reservationRepository.InsertAsync(reservation);
			}
			await offerRepository.InsertAsync(offer);
		}

		var waitlistLeadId = StableId(tenantId, "lead:waitlisted");
		var waitlistId = StableId(tenantId, "waitlist:waitlisted");
		if (leads.ContainsKey(waitlistLeadId) && !await waitlistRepository.AnyAsync(item => item.Id == waitlistId))
		{
			configuration.SeatPool.AddWaitlistedApplicant();
			await waitlistRepository.InsertAsync(new AdmissionCrmWaitlistEntry(
				waitlistId, tenantId, academicYearId, waitlistLeadId, configuration.SeatPool.Id, 82, 1, SeededAt));
		}
		await Repository<AdmissionCrmSeatPool>().UpdateAsync(configuration.SeatPool);
	}

	private async Task RepairActivationScenariosAsync(
		Guid tenantId,
		Guid academicYearId,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads,
		IReadOnlyDictionary<string, Standard> standards)
	{
		var standard = standards["Class 5"];
		var academicStage = await Repository<EducationMediumStage>().FindAsync(standard.EducationMediumStageId);
		if (academicStage == null) return;
		var medium = await Repository<EducationMedium>().FindAsync(academicStage.EducationMediumId);
		if (medium == null) return;
		var board = await Repository<EducationBoard>().FindAsync(medium.EducationBoardId);
		if (board == null) return;
		var division = await Repository<Division>().FirstOrDefaultAsync(item => item.StandardId == standard.Id);
		if (division == null) return;

		var numberSeriesRepository = Repository<AdmissionCrmNumberSeries>();
		var numberSeriesId = StableId(tenantId, "number-series:gr:class-5-demo-v2");
		var numberSeries = await numberSeriesRepository.FindAsync(numberSeriesId);
		if (numberSeries == null)
		{
			numberSeries = new AdmissionCrmNumberSeries(
				numberSeriesId, tenantId, academicYearId, "DDVS-GR", "DDVS GR number", AdmissionCrmIdentifierType.GrNumber,
				board.Id, medium.Id, standard.Id, "DDVS-GR-", string.Empty, 5, 1, 99999, SeededAt, SeededAt.AddYears(2));
			numberSeries.Submit(_seedRequesterId);
			numberSeries.Publish(_seedApproverId);
			await numberSeriesRepository.InsertAsync(numberSeries);
		}

		var activationRepository = Repository<AdmissionCrmActivation>();
		foreach (var key in DemoActivationKeys())
		{
			var leadId = StableId(tenantId, $"lead:{key}");
			if (!leads.ContainsKey(leadId)) continue;
			var activationId = StableId(tenantId, $"activation:{key}");
			if (await activationRepository.AnyAsync(item => item.Id == activationId)) continue;
			var activation = new AdmissionCrmActivation(
				activationId, tenantId, academicYearId, leadId, $"DDVS-ACT-{key.ToUpperInvariant()}", board.Id, medium.Id,
				academicStage.Id, standard.Id, division.Id, numberSeries.Id, null, ShikshaMitra.Students.GenderEnum.Male,
				"{\"registrationReady\":true,\"documentsReady\":true,\"paymentGate\":\"Manual\"}", "[]",
				true, true, _seedRequesterId, SeededAt);
			activation.Approve(_seedApproverId, SeededAt.AddHours(1));
			activation.StartAttempt();
			activation.RequireRepair("StudentAccount", "Student account provisioning needs a retry.");
			await activationRepository.InsertAsync(activation);
		}
	}

	private async Task RepairFollowUpTasksAsync(
		Guid tenantId,
		Guid academicYearId,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads,
		IReadOnlyDictionary<string, Guid> ownerUserIds)
	{
		var repository = Repository<AdmissionCrmFollowUpTask>();
		var today = DateTime.UtcNow.Date;
		foreach (var spec in FollowUpTaskSpecs())
		{
			var leadId = StableId(tenantId, $"lead:{spec.LeadKey}");
			if (!leads.ContainsKey(leadId)) continue;
			var taskId = StableId(tenantId, $"task:{spec.LeadKey}");
			if (await repository.AnyAsync(item => item.Id == taskId)) continue;

			await repository.InsertAsync(new AdmissionCrmFollowUpTask(
				taskId,
				tenantId,
				leadId,
				academicYearId,
				spec.Title,
				today.AddDays(spec.DueDayOffset).AddHours(17),
				ownerUserIds[spec.AssigneeEmail],
				spec.Notes));
		}
	}

	private async Task RepairRouteScenariosAsync(
		Guid tenantId,
		Guid academicYearId,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var leadId = StableId(tenantId, "lead:rte-allotment");
		if (!leads.ContainsKey(leadId)) return;

		var definitionRepository = Repository<AdmissionCrmRouteDefinition>();
		var definitionId = StableId(tenantId, "route-definition:rte-ews");
		var definition = await definitionRepository.FindAsync(definitionId);
		if (definition == null)
		{
			definition = new AdmissionCrmRouteDefinition(
				definitionId,
				tenantId,
				academicYearId,
				"RTE-EWS",
				"RTE / EWS admission",
				AdmissionCrmRouteType.RteEws,
				1,
				"{\"requiresGovernmentAllotment\":true}",
				SeededAt,
				SeededAt.AddYears(1));
			definition.Submit(_seedRequesterId);
			definition.Publish(_seedApproverId);
			await definitionRepository.InsertAsync(definition);
		}

		var assignmentRepository = Repository<AdmissionCrmRouteAssignment>();
		var assignmentId = StableId(tenantId, "route-assignment:rte-allotment");
		if (!await assignmentRepository.AnyAsync(item => item.Id == assignmentId))
		{
			var assignment = new AdmissionCrmRouteAssignment(
				assignmentId,
				tenantId,
				academicYearId,
				leadId,
				definition.Id,
				definition.Version,
				AdmissionCrmRouteType.RteEws,
				"{\"allotmentReference\":\"DDVS-RTE-DEMO\"}",
				"[]",
				null,
				"Government allotment requires staff reconciliation.",
				_seedRequesterId,
				SeededAt);
			assignment.Evaluate(
				AdmissionCrmRouteEligibilityStatus.Eligible,
				"Government allotment verified for the demo admission.",
				_seedApproverId,
				SeededAt.AddHours(1));
			await assignmentRepository.InsertAsync(assignment);
		}
	}

	private async Task ResetLegacyDemoScenariosAsync(Guid tenantId)
	{
		var leadIds = LegacyDemoLeadKeys()
			.Select(key => StableId(tenantId, $"lead:{key}"))
			.ToHashSet();
		await ReconcileLegacySeatPoolCountersAsync(tenantId, leadIds);

		await HardDeleteByLeadIdsAsync<AdmissionApplicantStatusAccess>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmActivation>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmAdmissionDecision>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmAdmissionQuotation>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmApplicantChecklistProgress>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmApplicantDocument>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmApplicantIdentityProfile>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmConcessionRequest>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmDocumentChecklistAssignment>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmEntranceExamRecord>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmFollowUpTask>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmGuardianRelationship>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmIdentifierAllocation>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmIdentityConsent>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmPolicyEvaluation>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmPolicyException>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmPrivacyRequest>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmRegistrationApplication>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmRouteAppeal>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmRouteAssignment>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmSeatOffer>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmSeatReservation>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmServiceHandoff>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmSlaHold>(item => leadIds.Contains(item.LeadId));
		await DeleteByLeadIdsAsync<AdmissionCrmTimelineEvent>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionCrmWaitlistEntry>(item => leadIds.Contains(item.LeadId));
		await DeleteByLeadIdsAsync<AdmissionCrmWaitlistHistory>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionInquiryConsentRecord>(item => leadIds.Contains(item.LeadId));
		await HardDeleteByLeadIdsAsync<AdmissionInquiryFormSubmission>(item => leadIds.Contains(item.LeadId));

		var leads = await _leadRepository.GetListAsync(item => leadIds.Contains(item.Id));
		foreach (var lead in leads)
		{
			await _leadRepository.HardDeleteAsync(lead, autoSave: false);
		}

		var oldNumberSeriesId = StableId(tenantId, "number-series:gr");
		if (!await Repository<AdmissionCrmIdentifierAllocation>().AnyAsync(item => item.NumberSeriesId == oldNumberSeriesId))
		{
			var oldNumberSeries = await Repository<AdmissionCrmNumberSeries>().FindAsync(oldNumberSeriesId);
			if (oldNumberSeries != null)
			{
				await Repository<AdmissionCrmNumberSeries>().HardDeleteAsync(oldNumberSeries, autoSave: false);
			}
		}

	}

	private async Task ReconcileLegacySeatPoolCountersAsync(Guid tenantId, IReadOnlySet<Guid> leadIds)
	{
		var seatPoolRepository = Repository<AdmissionCrmSeatPool>();
		var seatPool = await seatPoolRepository.FindAsync(StableId(tenantId, "seat-pool:general-1"));
		if (seatPool == null) return;

		var reservations = await Repository<AdmissionCrmSeatReservation>().GetListAsync(item =>
			item.SeatPoolId == seatPool.Id && leadIds.Contains(item.LeadId));
		foreach (var reservation in reservations)
		{
			if (reservation.Status == AdmissionCrmSeatReservationStatus.Active && seatPool.ReservedSeats > 0)
			{
				seatPool.ReleaseSeat(wasAccepted: false, cancelled: false);
			}
			else if (reservation.Status == AdmissionCrmSeatReservationStatus.Committed && seatPool.AcceptedSeats > 0)
			{
				seatPool.ReleaseSeat(wasAccepted: true, cancelled: false);
			}
		}

		var activeWaitlistCount = (await Repository<AdmissionCrmWaitlistEntry>().GetListAsync(item =>
			item.SeatPoolId == seatPool.Id && leadIds.Contains(item.LeadId)))
			.Count(item => item.Status == AdmissionCrmWaitlistStatus.Active);
		for (var index = 0; index < Math.Min(activeWaitlistCount, seatPool.WaitlistedApplicants); index++)
		{
			seatPool.RemoveWaitlistedApplicant();
		}

		await seatPoolRepository.UpdateAsync(seatPool, autoSave: false);
	}

	private async Task HardDeleteByLeadIdsAsync<TEntity>(
		Expression<Func<TEntity, bool>> predicate)
		where TEntity : class, IEntity<Guid>, ISoftDelete
	{
		var repository = Repository<TEntity>();
		var entities = await repository.GetListAsync(predicate);
		foreach (var entity in entities)
		{
			await repository.HardDeleteAsync(entity, autoSave: false);
		}
	}

	private async Task DeleteByLeadIdsAsync<TEntity>(Expression<Func<TEntity, bool>> predicate)
		where TEntity : class, IEntity<Guid>
	{
		var repository = Repository<TEntity>();
		var entities = await repository.GetListAsync(predicate);
		foreach (var entity in entities)
		{
			await repository.DeleteAsync(entity, autoSave: false);
		}
	}

	internal static string DefaultRegistrationSectionsJson()
	{
		static object Field(string key, string label, AdmissionCrmFormFieldType type, int order,
			bool required = false, string[]? options = null) => new
		{
			key,
			label,
			fieldType = type,
			systemField = key,
			required,
			parentEditable = true,
			displayOrder = order,
			options = options ?? Array.Empty<string>()
		};

		static object Section(string key, string title, int order, params object[] fields) => new
		{
			key,
			title,
			displayOrder = order,
			parentVisible = true,
			fields
		};

		var sections = new[]
		{
			Section("personal", "Personal and applicant details", 10,
				Field("firstName", "First name", AdmissionCrmFormFieldType.Text, 10, true),
				Field("middleName", "Middle name", AdmissionCrmFormFieldType.Text, 20),
				Field("lastName", "Last name", AdmissionCrmFormFieldType.Text, 30, true),
				Field("dateOfBirth", "Date of birth", AdmissionCrmFormFieldType.Date, 40, true),
				Field("gender", "Gender", AdmissionCrmFormFieldType.Select, 50, true, ["Male", "Female"]),
				Field("nationality", "Nationality", AdmissionCrmFormFieldType.Text, 60),
				Field("birthPlace", "Place of birth", AdmissionCrmFormFieldType.Text, 70),
				Field("motherTongue", "Mother tongue", AdmissionCrmFormFieldType.Text, 80),
				Field("aadharNo", "Aadhaar number", AdmissionCrmFormFieldType.Text, 90),
				Field("apparId", "APAAR ID", AdmissionCrmFormFieldType.Text, 100),
				Field("penNo", "PEN", AdmissionCrmFormFieldType.Text, 110)),
			Section("academic", "Academic and admission details", 20,
				Field("desiredClassName", "Class applied for", AdmissionCrmFormFieldType.Text, 10, true),
				Field("campus", "Campus", AdmissionCrmFormFieldType.Text, 20),
				Field("board", "Education board", AdmissionCrmFormFieldType.Text, 30),
				Field("medium", "Medium", AdmissionCrmFormFieldType.Text, 40),
				Field("admissionRound", "Admission round", AdmissionCrmFormFieldType.Text, 50),
				Field("applicantCategory", "Applicant category", AdmissionCrmFormFieldType.Text, 60),
				Field("admissionRoute", "Admission route", AdmissionCrmFormFieldType.Text, 70)),
			Section("family", "Family and guardians", 30,
				Field("parentGuardianName", "Parent or guardian name", AdmissionCrmFormFieldType.Text, 10, true),
				Field("guardianContactNo", "Parent or guardian mobile", AdmissionCrmFormFieldType.Phone, 20, true),
				Field("guardianEmail", "Parent or guardian email", AdmissionCrmFormFieldType.Email, 30),
				Field("guardianOccupation", "Occupation", AdmissionCrmFormFieldType.Text, 40),
				Field("guardianQualification", "Qualification", AdmissionCrmFormFieldType.Text, 50),
				Field("guardianAadharNo", "Parent or guardian Aadhaar", AdmissionCrmFormFieldType.Text, 60)),
			Section("contact", "Contact and addresses", 40,
				Field("primaryContactNo", "Primary mobile", AdmissionCrmFormFieldType.Phone, 10, true),
				Field("secondaryContactNo", "Secondary mobile", AdmissionCrmFormFieldType.Phone, 20),
				Field("email", "Applicant email", AdmissionCrmFormFieldType.Email, 30),
				Field("localAddress1", "Current address", AdmissionCrmFormFieldType.Text, 40, true),
				Field("localAddress2", "Current address line 2", AdmissionCrmFormFieldType.Text, 50),
				Field("localCity", "Current city", AdmissionCrmFormFieldType.Text, 60, true),
				Field("localState", "Current state", AdmissionCrmFormFieldType.Text, 70, true),
				Field("localCountry", "Current country", AdmissionCrmFormFieldType.Text, 80, true),
				Field("localPincode", "Current postal code", AdmissionCrmFormFieldType.Text, 90, true),
				Field("permanentSameAsLocal", "Permanent address is the same", AdmissionCrmFormFieldType.Checkbox, 100),
				Field("permanentAddress1", "Permanent address", AdmissionCrmFormFieldType.Text, 110),
				Field("permanentCity", "Permanent city", AdmissionCrmFormFieldType.Text, 120),
				Field("permanentState", "Permanent state", AdmissionCrmFormFieldType.Text, 130),
				Field("permanentCountry", "Permanent country", AdmissionCrmFormFieldType.Text, 140),
				Field("permanentPincode", "Permanent postal code", AdmissionCrmFormFieldType.Text, 150)),
			Section("compliance", "Additional and compliance information", 50,
				Field("category", "Category", AdmissionCrmFormFieldType.Text, 10),
				Field("religion", "Religion", AdmissionCrmFormFieldType.Text, 20),
				Field("familyCast", "Community or caste", AdmissionCrmFormFieldType.Text, 30),
				Field("secondLanguage", "Second language", AdmissionCrmFormFieldType.Text, 40),
				Field("haveDisability", "Disability accommodation required", AdmissionCrmFormFieldType.Checkbox, 50),
				Field("disabilityDetails", "Disability or accommodation details", AdmissionCrmFormFieldType.TextArea, 60),
				Field("emergencyContactName", "Emergency contact name", AdmissionCrmFormFieldType.Text, 70, true),
				Field("emergencyContactNo", "Emergency contact mobile", AdmissionCrmFormFieldType.Phone, 80, true),
				Field("emergencyContactRelation", "Emergency contact relationship", AdmissionCrmFormFieldType.Text, 90, true)),
			Section("previousSchool", "Previous school", 60,
				Field("previousSchoolName", "School name", AdmissionCrmFormFieldType.Text, 10),
				Field("previousSchoolAddress", "School address", AdmissionCrmFormFieldType.TextArea, 20),
				Field("previousSchoolBoard", "Board", AdmissionCrmFormFieldType.Text, 30),
				Field("previousSchoolMedium", "Medium", AdmissionCrmFormFieldType.Text, 40),
				Field("lastClassAttended", "Last class attended", AdmissionCrmFormFieldType.Text, 50),
				Field("previousSchoolLeavingDate", "Date of leaving", AdmissionCrmFormFieldType.Date, 60),
				Field("previousSchoolPercentage", "Result or percentage", AdmissionCrmFormFieldType.Number, 70),
				Field("previousSchoolAcademicYear", "Academic year", AdmissionCrmFormFieldType.Text, 80),
				Field("previousSchoolLeavingReason", "Reason for leaving", AdmissionCrmFormFieldType.TextArea, 90)),
			Section("medical", "Medical information", 70,
				Field("bloodGroup", "Blood group", AdmissionCrmFormFieldType.Text, 10),
				Field("heightCm", "Height (cm)", AdmissionCrmFormFieldType.Number, 20),
				Field("weightKg", "Weight (kg)", AdmissionCrmFormFieldType.Number, 30),
				Field("allergies", "Allergies", AdmissionCrmFormFieldType.TextArea, 40),
				Field("chronicConditions", "Chronic conditions", AdmissionCrmFormFieldType.TextArea, 50),
				Field("currentMedications", "Current medications", AdmissionCrmFormFieldType.TextArea, 60)),
			Section("bank", "Bank and payment identity", 80,
				Field("bankAccountHolderName", "Account holder", AdmissionCrmFormFieldType.Text, 10),
				Field("bankName", "Bank name", AdmissionCrmFormFieldType.Text, 20),
				Field("bankAccountNo", "Account number", AdmissionCrmFormFieldType.Text, 30),
				Field("bankIfscCode", "IFSC code", AdmissionCrmFormFieldType.Text, 40),
				Field("bankBranchName", "Branch", AdmissionCrmFormFieldType.Text, 50),
				Field("bankAccountType", "Account type", AdmissionCrmFormFieldType.Text, 60),
				Field("declarationAccepted", "I confirm that the information is accurate", AdmissionCrmFormFieldType.Checkbox, 70, true))
		};

		return JsonSerializer.Serialize(sections, new JsonSerializerOptions(JsonSerializerDefaults.Web));
	}

	internal static IReadOnlyList<string> LegacyDemoLeadKeys() =>
	[
		"new-inquiry", "follow-up", "duplicate-warning", "stale", "archived", "exam-ready", "exam-scheduled",
		"no-show", "passed", "failed", "registration-correction", "document-rejection", "admission-ready",
		"offer-pending", "offer-accepted", "waitlisted", "rte-allotment", "payment-pending", "activation-pending",
		"activation-repair", "first-day-ready"
	];

	internal static IReadOnlyList<FollowUpTaskSpec> FollowUpTaskSpecs() =>
	[
		new("new-inquiry", "Confirm class and contact", "admissions-reception@ddvs.com", 0, "Confirm the target class and the best parent contact number."),
		new("follow-up", "Call parent about admission intent", "admissions-counselor@ddvs.com", 2, "Confirm the family's admission intent and preferred next step."),
		new("no-show", "Reschedule missed entrance exam", "admissions-counselor@ddvs.com", -1, "Offer a new exam slot or record a reasoned rejection decision."),
		new("registration-correction", "Review corrected registration", "admissions-reception@ddvs.com", 0, "Review the date-of-birth correction and matching evidence."),
		new("offer-pending", "Review offer approval", "admissions-approver@ddvs.com", 1, "Approve or return the prepared seat offer."),
		new("activation-repair", "Repair student account activation", "admissions-approver@ddvs.com", 2, "Retry the failed student-account provisioning step.")
	];

	internal static IReadOnlyList<string> DemoRegistrationKeys() =>
	[
		"passed", "registration-correction", "rte-allotment", "offer-pending", "activation-repair"
	];

	internal static IReadOnlyList<string> DemoOfferKeys() =>
	[
		"offer-pending", "rte-allotment", "activation-repair"
	];

	internal static IReadOnlyList<string> DemoActivationKeys() =>
	[
		"activation-repair"
	];

	private async Task EnsureTimelineAsync(
		Guid tenantId,
		Guid academicYearId,
		Guid leadId,
		string key,
		AdmissionCrmTimelineEventType eventType,
		string title,
		string details,
		Guid? relatedEntityId)
	{
		var repository = Repository<AdmissionCrmTimelineEvent>();
		var eventId = StableId(tenantId, $"timeline:{key}");
		if (await repository.AnyAsync(item => item.Id == eventId)) return;
		await repository.InsertAsync(new AdmissionCrmTimelineEvent(
			eventId, tenantId, leadId, academicYearId, eventType, title, details, relatedEntityId,
			_seedRequesterId, new DateTimeOffset(SeededAt)));
	}

	internal static IReadOnlyList<ExamScenario> ExamScenarioSpecs() =>
	[
		new("exam-scheduled", 0, false, null, null, AdmissionCrmEligibilityDecision.Pending),
		new("no-show", 1, true, null, null, AdmissionCrmEligibilityDecision.Rejected),
		new("passed", 2, false, 82, 2, AdmissionCrmEligibilityDecision.Eligible),
		new("waitlisted", 0, false, 68, 12, AdmissionCrmEligibilityDecision.Waitlisted)
	];

	private sealed record ExtendedConfiguration(
		AdmissionCrmPlaybook Playbook,
		AdmissionCrmRegistrationTemplate RegistrationTemplate,
		AdmissionCrmDocumentChecklistTemplate Checklist,
		AdmissionCrmFormNumberSeries FormSeries,
		AdmissionCrmSeatPool SeatPool);

	internal sealed record ExamScenario(
		string Key,
		int SlotIndex,
		bool Absent,
		double? Marks,
		int? MeritRank,
		AdmissionCrmEligibilityDecision Decision);

	internal sealed record FollowUpTaskSpec(
		string LeadKey,
		string Title,
		string AssigneeEmail,
		int DueDayOffset,
		string Notes);
}
