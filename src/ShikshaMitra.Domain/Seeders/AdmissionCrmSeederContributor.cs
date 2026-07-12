using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class AdmissionCrmSeederContributor : BaseSeederContributor
{
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly IRepository<AdmissionCrmCycleConfiguration, Guid> _cycleRepository;
	private readonly IRepository<AdmissionCrmPipelineStage, Guid> _stageRepository;
	private readonly IRepository<AdmissionCrmInquirySource, Guid> _sourceRepository;
	private readonly IRepository<AdmissionCrmExamSlot, Guid> _slotRepository;
	private readonly IRepository<AdmissionCrmDocumentType, Guid> _documentTypeRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IConfiguration _configuration;
	private readonly ILogger<AdmissionCrmSeederContributor> _logger;

	public AdmissionCrmSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork currentUnitOfWork,
		IAcademicYearRepository academicYearRepository,
		IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
		IRepository<AdmissionCrmPipelineStage, Guid> stageRepository,
		IRepository<AdmissionCrmInquirySource, Guid> sourceRepository,
		IRepository<AdmissionCrmExamSlot, Guid> slotRepository,
		IRepository<AdmissionCrmDocumentType, Guid> documentTypeRepository,
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IConfiguration configuration,
		ILogger<AdmissionCrmSeederContributor> logger)
		: base(currentTenant, guidGenerator, currentUnitOfWork)
	{
		_academicYearRepository = academicYearRepository;
		_cycleRepository = cycleRepository;
		_stageRepository = stageRepository;
		_sourceRepository = sourceRepository;
		_slotRepository = slotRepository;
		_documentTypeRepository = documentTypeRepository;
		_leadRepository = leadRepository;
		_configuration = configuration;
		_logger = logger;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		if (!CurrentTenant.Id.HasValue)
		{
			return;
		}

		var currentYear = DateTime.Now.Year.ToString();
		var academicYear = await _academicYearRepository.FirstOrDefaultAsync(year => year.Name.StartsWith(currentYear)) ??
			await _academicYearRepository.FirstOrDefaultAsync();
		if (academicYear == null)
		{
			_logger.LogWarning("[AdmissionCrmSeeder] No academic year exists for tenant {TenantId}.", CurrentTenant.Id);
			return;
		}

		var tenantId = CurrentTenant.Id.Value;
		await RepairCycleAsync(tenantId, academicYear.Id, academicYear.Name);
		await RepairStagesAsync(tenantId, academicYear.Id);
		var sources = await RepairSourcesAsync(tenantId, academicYear.Id);
		await RepairSlotsAsync(tenantId, academicYear.Id, academicYear.Name);
		await RepairDocumentTypesAsync(tenantId, academicYear.Id);

		var profile = ShikshaMitraSeederProfile.Normalize(
			context.Properties.TryGetValue(ShikshaMitraSeederProfile.ContextPropertyName, out var value)
				? value?.ToString()
				: _configuration["ShikshaMitra:Seeders:Profile"]);
		if (profile != ShikshaMitraSeederProfile.Minimal)
		{
			await RepairScenarioLeadsAsync(tenantId, academicYear.Id, sources);
		}

		await CurrentUnitOfWork.SaveChangesAsync();
		_logger.LogInformation(
			"[AdmissionCrmSeeder] Repaired CRM configuration for tenant {TenantId}, academic year {AcademicYearId}, profile {Profile}.",
			tenantId,
			academicYear.Id,
			profile);
	}

	private async Task RepairCycleAsync(Guid tenantId, Guid academicYearId, string academicYearName)
	{
		var cycle = await _cycleRepository.FirstOrDefaultAsync(item => item.AcademicYearId == academicYearId);
		cycle ??= new AdmissionCrmCycleConfiguration(StableId(tenantId, "cycle"), tenantId, academicYearId, $"Admissions {academicYearName}");
		cycle.Update(
			$"Admissions {academicYearName}",
			true,
			false,
			30,
			60,
			24,
			7,
			40,
			AdmissionCrmMeritRankingMode.MarksThenInquiryDate,
			AdmissionCrmExamReadinessField.Contact | AdmissionCrmExamReadinessField.AdmissionIntent | AdmissionCrmExamReadinessField.DesiredClass,
			"{\"sourceScores\":{},\"channelScores\":{\"SchoolWebsiteForm\":15,\"WalkInReception\":12,\"QuickCallEntry\":8}}",
			"{\"strategy\":\"RoundRobin\",\"rules\":[]}",
			"[]",
			"[{\"key\":\"contactVerified\",\"label\":\"Contact verified\",\"required\":true},{\"key\":\"classConfirmed\",\"label\":\"Class confirmed\",\"required\":true}]",
			"[]",
			"{\"timeZoneId\":\"Asia/Kolkata\",\"workingDays\":[1,2,3,4,5,6],\"workingDayStartHour\":8,\"workingDayEndHour\":17,\"holidays\":[],\"escalationAfterHours\":24}",
			"Deterministic DDVS admissions CRM configuration.");
		if (await _cycleRepository.AnyAsync(item => item.Id == cycle.Id))
		{
			await _cycleRepository.UpdateAsync(cycle);
		}
		else
		{
			await _cycleRepository.InsertAsync(cycle);
		}
	}

	private async Task RepairStagesAsync(Guid tenantId, Guid academicYearId)
	{
		var existing = await _stageRepository.GetListAsync(item => item.AcademicYearId == academicYearId);
		var colors = new[] { "#1565C0", "#00695C", "#2E7D32", "#6A1B9A", "#4527A0", "#00838F", "#EF6C00", "#C62828", "#5D4037", "#455A64" };
		foreach (var stageValue in Enum.GetValues<AdmissionInquiryStage>())
		{
			var stage = existing.FirstOrDefault(item => item.SystemStage == stageValue) ??
				new AdmissionCrmPipelineStage(
					StableId(tenantId, $"stage:{stageValue}"), tenantId, academicYearId, stageValue,
					StageLabel(stageValue), (int)stageValue, colors[(int)stageValue], StageSla(stageValue), "Admissions team");
			stage.Update(StageLabel(stageValue), (int)stageValue, true, colors[(int)stageValue], StageSla(stageValue), "Admissions team");
			if (existing.Any(item => item.Id == stage.Id)) await _stageRepository.UpdateAsync(stage);
			else await _stageRepository.InsertAsync(stage);
		}
	}

	private async Task<Dictionary<string, AdmissionCrmInquirySource>> RepairSourcesAsync(Guid tenantId, Guid academicYearId)
	{
		var existing = await _sourceRepository.GetListAsync(item => item.AcademicYearId == academicYearId);
		var specifications = new[]
		{
			new SourceSpec("WEBSITE", "School website", AdmissionInquiryChannel.SchoolWebsiteForm, 10),
			new SourceSpec("WALK_IN", "Walk-in reception", AdmissionInquiryChannel.WalkInReception, 20),
			new SourceSpec("PHONE", "Quick call", AdmissionInquiryChannel.QuickCallEntry, 30),
			new SourceSpec("WHATSAPP", "WhatsApp inquiry", AdmissionInquiryChannel.WhatsAppInquiry, 40),
			new SourceSpec("STAFF_LINK", "Staff-shared link", AdmissionInquiryChannel.StaffSharedLink, 50),
			new SourceSpec("QR", "QR code", AdmissionInquiryChannel.QrCodeScan, 60),
			new SourceSpec("CAMPAIGN", "Campaign", AdmissionInquiryChannel.CampaignLink, 70),
			new SourceSpec("BACK_OFFICE", "Back-office entry", AdmissionInquiryChannel.ManualBackOffice, 80)
		};
		var result = new Dictionary<string, AdmissionCrmInquirySource>(StringComparer.OrdinalIgnoreCase);
		foreach (var spec in specifications)
		{
			var source = existing.FirstOrDefault(item => item.Code == spec.Code) ??
				new AdmissionCrmInquirySource(
					StableId(tenantId, $"source:{spec.Code}"), tenantId, academicYearId, spec.Code, spec.Name,
					"Seeded CRM inquiry source.", [spec.Channel], spec.Order, true);
			source.Update(spec.Code, spec.Name, "Seeded CRM inquiry source.", [spec.Channel], spec.Order, true);
			source.SetActive(true, null);
			if (existing.Any(item => item.Id == source.Id)) await _sourceRepository.UpdateAsync(source);
			else await _sourceRepository.InsertAsync(source);
			result[spec.Code] = source;
		}
		return result;
	}

	private async Task RepairSlotsAsync(Guid tenantId, Guid academicYearId, string academicYearName)
	{
		var existing = await _slotRepository.GetListAsync(item => item.AcademicYearId == academicYearId);
		var nextMonday = SeedExamMonday(academicYearName);
		var specs = new[]
		{
			new SlotSpec("Morning A", nextMonday.AddHours(9), nextMonday.AddHours(11), "Main Campus", "Hall A", 40, 101),
			new SlotSpec("Afternoon A", nextMonday.AddHours(13), nextMonday.AddHours(15), "Main Campus", "Hall A", 40, 101),
			new SlotSpec("Morning B", nextMonday.AddDays(2).AddHours(9), nextMonday.AddDays(2).AddHours(11), "Main Campus", "Hall B", 30, 102)
		};
		foreach (var spec in specs)
		{
			var slot = existing.FirstOrDefault(item => item.SlotName == spec.Name) ??
				new AdmissionCrmExamSlot(
					StableId(tenantId, $"slot:{spec.Name}"), tenantId, academicYearId, null, "GENERAL", "OFFLINE",
					spec.Name, spec.Campus, spec.Hall, spec.Start, spec.End, spec.Capacity, spec.RoomNo);
			slot.Update(spec.Name, spec.Campus, spec.Hall, spec.Start, spec.End, spec.Capacity, spec.RoomNo);
			slot.SetActive(true);
			if (existing.Any(item => item.Id == slot.Id)) await _slotRepository.UpdateAsync(slot);
			else await _slotRepository.InsertAsync(slot);
		}
	}

	private static DateTimeOffset SeedExamMonday(string academicYearName)
	{
		var indiaOffset = TimeSpan.FromHours(5.5);
		var firstYear = academicYearName
			.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			.Select(value => int.TryParse(value, out var year) ? year : 0)
			.FirstOrDefault(year => year >= 2000);
		var anchor = new DateTimeOffset(
			firstYear > 0 ? firstYear : 2026,
			4,
			1,
			9,
			0,
			0,
			indiaOffset);
		var daysUntilMonday = ((int)DayOfWeek.Monday - (int)anchor.DayOfWeek + 7) % 7;
		var examMonday = anchor.AddDays(daysUntilMonday + 7);
		var minimumStart = DateTimeOffset.UtcNow.ToOffset(indiaOffset).AddDays(7);
		while (examMonday <= minimumStart)
		{
			examMonday = examMonday.AddDays(7);
		}

		return examMonday;
	}

	private async Task RepairDocumentTypesAsync(Guid tenantId, Guid academicYearId)
	{
		var existing = await _documentTypeRepository.GetListAsync(item => item.AcademicYearId == academicYearId);
		var specs = new[]
		{
			new DocumentSpec("BIRTH_CERT", "Birth certificate", true, false, 10),
			new DocumentSpec("AADHAAR", "Aadhaar", false, false, 20),
			new DocumentSpec("PHOTO", "Applicant photograph", false, false, 30),
			new DocumentSpec("ADDRESS_PROOF", "Address proof", true, false, 40),
			new DocumentSpec("TRANSFER_CERT", "Transfer certificate", true, false, 50),
			new DocumentSpec("REPORT_CARD", "Previous report card", false, false, 60),
			new DocumentSpec("CATEGORY_CERT", "Category certificate", true, true, 70),
			new DocumentSpec("RTE_ALLOTMENT", "RTE allotment letter", true, false, 80)
		};
		foreach (var spec in specs)
		{
			var documentType = existing.FirstOrDefault(item => item.Code == spec.Code) ??
				new AdmissionCrmDocumentType(
					StableId(tenantId, $"document:{spec.Code}"), tenantId, academicYearId, spec.Code, spec.Name, 5,
					["pdf", "jpg", "jpeg", "png"], spec.RequiresOriginal, spec.ValidityRequired, spec.Order);
			documentType.Update(spec.Code, spec.Name, 5, ["pdf", "jpg", "jpeg", "png"], spec.RequiresOriginal, spec.ValidityRequired, spec.Order);
			documentType.SetActive(true);
			if (existing.Any(item => item.Id == documentType.Id)) await _documentTypeRepository.UpdateAsync(documentType);
			else await _documentTypeRepository.InsertAsync(documentType);
		}
	}

	private async Task RepairScenarioLeadsAsync(
		Guid tenantId,
		Guid academicYearId,
		IReadOnlyDictionary<string, AdmissionCrmInquirySource> sources)
	{
		var scenarios = new[]
		{
			new LeadSpec("new-inquiry", "Aarav", "Patel", AdmissionInquiryStage.NewInquiry, "WEBSITE", AdmissionInquiryChannel.SchoolWebsiteForm),
			new LeadSpec("duplicate-warning", "Aarav", "Patel", AdmissionInquiryStage.FollowUp, "PHONE", AdmissionInquiryChannel.QuickCallEntry),
			new LeadSpec("stale", "Diya", "Shah", AdmissionInquiryStage.Stale, "WHATSAPP", AdmissionInquiryChannel.WhatsAppInquiry),
			new LeadSpec("archived", "Kabir", "Mehta", AdmissionInquiryStage.Archived, "BACK_OFFICE", AdmissionInquiryChannel.ManualBackOffice),
			new LeadSpec("exam-ready", "Meera", "Joshi", AdmissionInquiryStage.ExamReady, "WALK_IN", AdmissionInquiryChannel.WalkInReception),
			new LeadSpec("exam-scheduled", "Vivaan", "Desai", AdmissionInquiryStage.ExamScheduled, "WEBSITE", AdmissionInquiryChannel.SchoolWebsiteForm),
			new LeadSpec("no-show", "Anaya", "Trivedi", AdmissionInquiryStage.ExamScheduled, "STAFF_LINK", AdmissionInquiryChannel.StaffSharedLink),
			new LeadSpec("passed", "Aditya", "Bhatt", AdmissionInquiryStage.ExamCompleted, "QR", AdmissionInquiryChannel.QrCodeScan),
			new LeadSpec("failed", "Ishita", "Prajapati", AdmissionInquiryStage.Rejected, "CAMPAIGN", AdmissionInquiryChannel.CampaignLink),
			new LeadSpec("registration-correction", "Riya", "Modi", AdmissionInquiryStage.Eligible, "WEBSITE", AdmissionInquiryChannel.SchoolWebsiteForm),
			new LeadSpec("document-rejection", "Arjun", "Raval", AdmissionInquiryStage.Eligible, "WALK_IN", AdmissionInquiryChannel.WalkInReception),
			new LeadSpec("admission-ready", "Saanvi", "Dave", AdmissionInquiryStage.Eligible, "STAFF_LINK", AdmissionInquiryChannel.StaffSharedLink),
			new LeadSpec("offer-pending", "Reyansh", "Thakkar", AdmissionInquiryStage.Eligible, "WEBSITE", AdmissionInquiryChannel.SchoolWebsiteForm),
			new LeadSpec("offer-accepted", "Aadhya", "Parikh", AdmissionInquiryStage.Eligible, "WALK_IN", AdmissionInquiryChannel.WalkInReception),
			new LeadSpec("waitlisted", "Krishna", "Vyas", AdmissionInquiryStage.Waitlisted, "CAMPAIGN", AdmissionInquiryChannel.CampaignLink),
			new LeadSpec("rte-allotment", "Fatima", "Shaikh", AdmissionInquiryStage.Eligible, "BACK_OFFICE", AdmissionInquiryChannel.ManualBackOffice),
			new LeadSpec("payment-pending", "Dev", "Solanki", AdmissionInquiryStage.Eligible, "WEBSITE", AdmissionInquiryChannel.SchoolWebsiteForm),
			new LeadSpec("activation-pending", "Mahi", "Chauhan", AdmissionInquiryStage.Eligible, "STAFF_LINK", AdmissionInquiryChannel.StaffSharedLink),
			new LeadSpec("activation-repair", "Neil", "Gandhi", AdmissionInquiryStage.Eligible, "WALK_IN", AdmissionInquiryChannel.WalkInReception),
			new LeadSpec("first-day-ready", "Tara", "Pandya", AdmissionInquiryStage.Eligible, "WEBSITE", AdmissionInquiryChannel.SchoolWebsiteForm)
		};
		var existingIds = (await _leadRepository.GetListAsync(item => item.AcademicYearId == academicYearId))
			.Select(item => item.Id)
			.ToHashSet();
		for (var index = 0; index < scenarios.Length; index++)
		{
			var spec = scenarios[index];
			var id = StableId(tenantId, $"lead:{spec.Key}");
			if (existingIds.Contains(id)) continue;
			var source = sources[spec.SourceCode];
			var lead = new AdmissionInquiryLead(
				id, tenantId, academicYearId, source.Id, source.DisplayName, spec.Channel,
				spec.Key.Contains("campaign", StringComparison.OrdinalIgnoreCase) ? "DDVS Open House" : null,
				"Main Campus", index % 3 == 0 ? "gu" : index % 3 == 1 ? "hi" : "en",
				spec.FirstName, null, spec.LastName, DateTimeOffset.Now.AddYears(-6 - index % 8),
				$"Parent of {spec.FirstName}", $"900000{index:0000}", $"admission.crm.{index:00}@ddvs.example",
				"Admission for the upcoming academic cycle", null, $"Class {(index % 10) + 1}",
				$"DDVS scenario: {spec.Key}.", DateTimeOffset.Now.AddDays(-(index + 1)));
			if (spec.Stage == AdmissionInquiryStage.Stale) lead.MarkStale();
			else if (spec.Stage == AdmissionInquiryStage.Archived) lead.Archive("Seeded archived scenario.", DateTime.UtcNow.AddDays(-5));
			else if (spec.Stage != AdmissionInquiryStage.NewInquiry) lead.ChangeStage(spec.Stage);
			await _leadRepository.InsertAsync(lead);
		}
	}

	private static Guid StableId(Guid tenantId, string key)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId:N}:admission-crm:{key}"));
		return new Guid(hash.AsSpan(0, 16));
	}

	private static string StageLabel(AdmissionInquiryStage stage) => stage switch
	{
		AdmissionInquiryStage.NewInquiry => "New Inquiry",
		AdmissionInquiryStage.FollowUp => "Follow-up",
		AdmissionInquiryStage.ExamReady => "Exam Ready",
		AdmissionInquiryStage.ExamScheduled => "Exam Scheduled",
		AdmissionInquiryStage.ExamCompleted => "Exam Completed",
		_ => stage.ToString()
	};

	private static int? StageSla(AdmissionInquiryStage stage) => stage switch
	{
		AdmissionInquiryStage.NewInquiry => 4,
		AdmissionInquiryStage.FollowUp => 24,
		AdmissionInquiryStage.ExamReady => 24,
		AdmissionInquiryStage.ExamScheduled => 48,
		_ => null
	};

	private sealed record SourceSpec(string Code, string Name, AdmissionInquiryChannel Channel, int Order);
	private sealed record SlotSpec(string Name, DateTimeOffset Start, DateTimeOffset End, string Campus, string Hall, int Capacity, int RoomNo);
	private sealed record DocumentSpec(string Code, string Name, bool RequiresOriginal, bool ValidityRequired, int Order);
	private sealed record LeadSpec(string Key, string FirstName, string LastName, AdmissionInquiryStage Stage, string SourceCode, AdmissionInquiryChannel Channel);
}
