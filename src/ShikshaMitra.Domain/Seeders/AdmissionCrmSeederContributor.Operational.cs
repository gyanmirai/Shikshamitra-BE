using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Seeders;

public partial class AdmissionCrmSeederContributor
{
	private static readonly DateTime SeededAt = new(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc);
	private static readonly Guid SeedRequesterId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
	private static readonly Guid SeedApproverId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");

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
				"[{\"code\":\"GENERAL-1\",\"roundCode\":\"MAIN\",\"campus\":\"Main Campus\",\"desiredClass\":\"Class 1\",\"board\":\"GSEB\",\"medium\":\"English\",\"plannedSeats\":40,\"approvedCapacity\":40,\"offeredSeats\":0,\"acceptedSeats\":0,\"waitlistedApplicants\":0,\"withdrawnApplicants\":0,\"remainingCapacity\":40,\"isOverAllocated\":false}]",
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
				"[{\"key\":\"student\",\"title\":\"Student details\",\"displayOrder\":10,\"parentVisible\":true,\"fields\":[{\"key\":\"firstName\",\"label\":\"First name\",\"fieldType\":0,\"systemField\":\"FirstName\",\"required\":true,\"parentEditable\":true,\"displayOrder\":10,\"options\":[]},{\"key\":\"dateOfBirth\",\"label\":\"Date of birth\",\"fieldType\":4,\"systemField\":\"DateOfBirth\",\"required\":true,\"parentEditable\":true,\"displayOrder\":20,\"options\":[]}]},{\"key\":\"family\",\"title\":\"Family and communication\",\"displayOrder\":20,\"parentVisible\":true,\"fields\":[{\"key\":\"parentName\",\"label\":\"Parent or guardian\",\"fieldType\":0,\"systemField\":\"ParentGuardianName\",\"required\":true,\"parentEditable\":true,\"displayOrder\":10,\"options\":[]},{\"key\":\"phone\",\"label\":\"Mobile number\",\"fieldType\":2,\"systemField\":\"PrimaryContactNo\",\"required\":true,\"parentEditable\":true,\"displayOrder\":20,\"options\":[]}]}]",
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
				seatPoolId, tenantId, academicYearId, playbook.Id, "GENERAL-1", "Class 1 general seats", "MAIN",
				"Main Campus", "Class 1", "GSEB", "English", "General", 32, 40);
			await seatPoolRepository.InsertAsync(seatPool);
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
	}

	private async Task RepairOperationalScenariosAsync(
		Guid tenantId,
		Guid academicYearId,
		ExtendedConfiguration configuration,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads,
		IReadOnlyList<AdmissionCrmExamSlot> slots)
	{
		_logger.LogInformation(
			"[AdmissionCrmSeeder] Operational prerequisites loaded. Leads={LeadCount}, Slots={SlotCount}.",
			leads.Count,
			slots.Count);
		if (slots.Count == 0)
		{
			_logger.LogWarning("[AdmissionCrmSeeder] Operational scenarios skipped because no active exam slots were visible.");
			return;
		}

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

		await RepairRegistrationScenariosAsync(tenantId, academicYearId, configuration, leads);
		await RepairOfferAndWaitlistScenariosAsync(tenantId, academicYearId, configuration, leads);
		await RepairActivationScenariosAsync(tenantId, academicYearId, configuration, leads);
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
		var keys = new[] { "registration-correction", "document-rejection", "admission-ready", "offer-pending", "offer-accepted", "waitlisted", "rte-allotment", "payment-pending", "activation-pending", "activation-repair", "first-day-ready" };
		for (var index = 0; index < keys.Length; index++)
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
				application.Save("{\"firstName\":\"Seeded applicant\",\"parentName\":\"Seeded parent\"}", 75, SeededAt.AddHours(1));
				application.Submit("{\"firstName\":\"Seeded applicant\",\"parentName\":\"Seeded parent\"}", SeededAt.AddHours(2), false);
				if (key == "registration-correction")
				{
					application.ReturnForCorrection("[{\"fieldKey\":\"dateOfBirth\",\"message\":\"Upload a matching birth certificate.\"}]", "Date of birth evidence does not match.");
				}
				else
				{
					application.Verify(SeededAt.AddHours(3));
					if (key != "document-rejection") application.MarkAdmissionReady();
				}
				await applicationRepository.InsertAsync(application);
			}

			if (key is "registration-correction") continue;
			var assignmentId = StableId(tenantId, $"document-assignment:{key}");
			var assignment = await assignmentRepository.FindAsync(assignmentId);
			if (assignment == null)
			{
				assignment = new AdmissionCrmDocumentChecklistAssignment(
					assignmentId, tenantId, academicYearId, leadId, application.Id, configuration.Checklist.Id, 1,
					"{\"requirements\":[\"birthCertificate\",\"addressProof\"]}", SeededAt.AddHours(3));
				if (key != "document-rejection")
				{
					assignment.ConfirmReadiness(SeedApproverId, 1, false, null, SeededAt.AddHours(5));
				}
				await assignmentRepository.InsertAsync(assignment);
			}

			var documentId = StableId(tenantId, $"applicant-document:{key}:birth");
			if (!await documentRepository.AnyAsync(item => item.Id == documentId))
			{
				var document = new AdmissionCrmApplicantDocument(
					documentId, tenantId, academicYearId, leadId, assignment.Id, "birthCertificate", 1,
					$"seed/admissions/{key}/birth-certificate.pdf", "birth-certificate.pdf", "application/pdf", 1024,
					new string('a', 64), null, SeededAt.AddHours(4));
				if (key == "document-rejection")
					document.Reject(SeedApproverId, "The uploaded copy is unreadable.", SeededAt.AddHours(5));
				else
					document.Verify(SeedApproverId, SeededAt.AddHours(5));
				await documentRepository.InsertAsync(document);
			}
		}

		var minimumNextNumber = keys.LongLength + 1;
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
		foreach (var key in new[] { "offer-pending", "offer-accepted", "payment-pending" })
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
			offer.SubmitForApproval(SeedRequesterId, SeededAt.AddHours(1));
			if (key != "offer-pending")
			{
				offer.Approve(SeedApproverId, SeededAt.AddHours(2));
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
		ExtendedConfiguration configuration,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var board = await Repository<EducationBoard>().FirstOrDefaultAsync();
		if (board == null) return;
		var medium = await Repository<EducationMedium>().FirstOrDefaultAsync(item => item.EducationBoardId == board.Id);
		if (medium == null) return;
		var academicStage = await Repository<EducationMediumStage>().FirstOrDefaultAsync(item => item.EducationMediumId == medium.Id);
		if (academicStage == null) return;
		var standard = await Repository<Standard>().FirstOrDefaultAsync(item => item.EducationMediumStageId == academicStage.Id);
		if (standard == null) return;
		var division = await Repository<Division>().FirstOrDefaultAsync(item => item.StandardId == standard.Id);
		if (division == null) return;

		var numberSeriesRepository = Repository<AdmissionCrmNumberSeries>();
		var numberSeriesId = StableId(tenantId, "number-series:gr");
		var numberSeries = await numberSeriesRepository.FindAsync(numberSeriesId);
		if (numberSeries == null)
		{
			numberSeries = new AdmissionCrmNumberSeries(
				numberSeriesId, tenantId, academicYearId, "DDVS-GR", "DDVS GR number", AdmissionCrmIdentifierType.GrNumber,
				board.Id, medium.Id, standard.Id, "DDVS-GR-", string.Empty, 5, 1, 99999, SeededAt, SeededAt.AddYears(2));
			numberSeries.Submit(SeedRequesterId);
			numberSeries.Publish(SeedApproverId);
			await numberSeriesRepository.InsertAsync(numberSeries);
		}

		var activationRepository = Repository<AdmissionCrmActivation>();
		var student = (await _serviceProvider.GetRequiredService<IStudentRepository>().GetListAsync())
			.FirstOrDefault(candidate => leads.Values.All(lead => lead.StudentId != candidate.Id));
		foreach (var key in new[] { "activation-pending", "activation-repair", "first-day-ready" })
		{
			var leadId = StableId(tenantId, $"lead:{key}");
			if (!leads.ContainsKey(leadId)) continue;
			var activationId = StableId(tenantId, $"activation:{key}");
			if (await activationRepository.AnyAsync(item => item.Id == activationId)) continue;
			var activation = new AdmissionCrmActivation(
				activationId, tenantId, academicYearId, leadId, $"DDVS-ACT-{key.ToUpperInvariant()}", board.Id, medium.Id,
				academicStage.Id, standard.Id, division.Id, numberSeries.Id, null, ShikshaMitra.Students.GenderEnum.Male,
				"{\"registrationReady\":true,\"documentsReady\":true,\"paymentGate\":\"Manual\"}", "[]",
				true, true, SeedRequesterId, SeededAt);
			if (key != "activation-pending")
			{
				activation.Approve(SeedApproverId, SeededAt.AddHours(1));
				activation.StartAttempt();
				if (key == "activation-repair")
					activation.RequireRepair("StudentAccount", "Seeded account provisioning retry scenario.");
				else if (student != null)
				{
					activation.LinkStudent(student.Id);
					activation.UpdateSteps("[{\"code\":\"StudentLinked\",\"status\":\"Completed\"},{\"code\":\"AccountsProvisioned\",\"status\":\"Completed\"}]");
					activation.Complete(SeededAt.AddHours(2));
					leads[leadId].LinkStudent(student.Id);
					await _leadRepository.UpdateAsync(leads[leadId]);
				}
			}
			await activationRepository.InsertAsync(activation);
		}
	}

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
			SeedRequesterId, new DateTimeOffset(SeededAt)));
	}

	private static IReadOnlyList<ExamScenario> ExamScenarioSpecs() =>
	[
		new("exam-scheduled", 0, false, null, null, AdmissionCrmEligibilityDecision.Pending),
		new("no-show", 1, true, null, null, AdmissionCrmEligibilityDecision.Rejected),
		new("passed", 2, false, 82, 2, AdmissionCrmEligibilityDecision.Eligible),
		new("failed", 0, false, 35, null, AdmissionCrmEligibilityDecision.Rejected),
		new("registration-correction", 1, false, 76, 4, AdmissionCrmEligibilityDecision.Eligible),
		new("document-rejection", 2, false, 74, 5, AdmissionCrmEligibilityDecision.Eligible),
		new("admission-ready", 0, false, 88, 1, AdmissionCrmEligibilityDecision.Eligible),
		new("offer-pending", 1, false, 72, 7, AdmissionCrmEligibilityDecision.Eligible),
		new("offer-accepted", 2, false, 84, 3, AdmissionCrmEligibilityDecision.Eligible),
		new("waitlisted", 0, false, 68, 12, AdmissionCrmEligibilityDecision.Waitlisted),
		new("rte-allotment", 1, false, 79, 6, AdmissionCrmEligibilityDecision.Eligible),
		new("payment-pending", 2, false, 81, 5, AdmissionCrmEligibilityDecision.Eligible),
		new("activation-pending", 0, false, 86, 2, AdmissionCrmEligibilityDecision.Eligible),
		new("activation-repair", 1, false, 83, 3, AdmissionCrmEligibilityDecision.Eligible),
		new("first-day-ready", 2, false, 91, 1, AdmissionCrmEligibilityDecision.Eligible)
	];

	private sealed record ExtendedConfiguration(
		AdmissionCrmPlaybook Playbook,
		AdmissionCrmRegistrationTemplate RegistrationTemplate,
		AdmissionCrmDocumentChecklistTemplate Checklist,
		AdmissionCrmFormNumberSeries FormSeries,
		AdmissionCrmSeatPool SeatPool);

	private sealed record ExamScenario(
		string Key,
		int SlotIndex,
		bool Absent,
		double? Marks,
		int? MeritRank,
		AdmissionCrmEligibilityDecision Decision);
}
