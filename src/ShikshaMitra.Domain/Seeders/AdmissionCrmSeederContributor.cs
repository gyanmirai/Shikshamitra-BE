using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public partial class AdmissionCrmSeederContributor : BaseSeederContributor
{
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly IRepository<AdmissionCrmCycleConfiguration, Guid> _cycleRepository;
	private readonly IRepository<AdmissionCrmPipelineStage, Guid> _stageRepository;
	private readonly IRepository<AdmissionCrmInquirySource, Guid> _sourceRepository;
	private readonly IRepository<AdmissionCrmExamSlot, Guid> _slotRepository;
	private readonly IRepository<AdmissionCrmDocumentType, Guid> _documentTypeRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<AdmissionCrmSeederContributor> _logger;
	private static readonly string[] RequiredDemoOwnerEmails =
	[
		"admissions-reception@ddvs.com",
		"admissions-counselor@ddvs.com",
		"admissions-approver@ddvs.com"
	];

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
		IServiceProvider serviceProvider,
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
		_serviceProvider = serviceProvider;
		_logger = logger;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		if (!CurrentTenant.Id.HasValue)
		{
			return;
		}

		var academicYear = await GetTargetAcademicYearAsync();
		if (academicYear == null)
		{
			_logger.LogWarning("[AdmissionCrmSeeder] No academic year exists for tenant {TenantId}.", CurrentTenant.Id);
			return;
		}

		var tenantId = CurrentTenant.Id.Value;
		var ownerUserIds = await GetConfiguredOwnerUserIdsAsync();
		await RepairCycleAsync(tenantId, academicYear.Id, academicYear.Name, ownerUserIds);
		await RepairStagesAsync(tenantId, academicYear.Id);
		await RepairSourcesAsync(tenantId, academicYear.Id);
		await RepairSlotsAsync(tenantId, academicYear.Id, academicYear.Name);
		await RepairDocumentTypesAsync(tenantId, academicYear.Id);
		await RepairExtendedConfigurationAsync(tenantId, academicYear.Id, academicYear.Name);
		await CurrentUnitOfWork.SaveChangesAsync();
		_logger.LogInformation(
			"[AdmissionCrmSeeder] Repaired CRM configuration for tenant {TenantId}, academic year {AcademicYearId}.",
			tenantId,
			academicYear.Id);
	}

	internal async Task SeedDemoScenariosAsync()
	{
		if (!CurrentTenant.Id.HasValue)
		{
			return;
		}

		var tenantId = CurrentTenant.Id.Value;
		var academicYear = await GetDemoScenarioAcademicYearAsync(tenantId);
		if (academicYear == null)
		{
			_logger.LogWarning("[AdmissionCrmDemoSeeder] No academic year exists for tenant {TenantId}.", CurrentTenant.Id);
			return;
		}

		var ownerUserIds = await GetConfiguredOwnerUserIdsAsync();
		var missingOwnerEmails = RequiredDemoOwnerEmails.Where(email => !ownerUserIds.ContainsKey(email)).ToList();
		if (missingOwnerEmails.Count > 0)
		{
			_logger.LogWarning(
				"[AdmissionCrmDemoSeeder] Demo scenarios skipped for tenant {TenantId}; active employee users are missing for {MissingOwnerEmails}.",
				tenantId,
				string.Join(", ", missingOwnerEmails));
			return;
		}

		await RepairCycleAsync(tenantId, academicYear.Id, academicYear.Name, ownerUserIds);
		await RepairStagesAsync(tenantId, academicYear.Id);
		await RepairDocumentTypesAsync(tenantId, academicYear.Id);
		var standards = await ResolveDemoStandardsAsync(academicYear.Id);
		await ResetLegacyDemoScenariosAsync(tenantId);
		await CurrentUnitOfWork.SaveChangesAsync();

		var sources = await RepairSourcesAsync(tenantId, academicYear.Id);
		var slots = await RepairSlotsAsync(tenantId, academicYear.Id, academicYear.Name);
		var configuration = await RepairExtendedConfigurationAsync(tenantId, academicYear.Id, academicYear.Name);
		var scenarioLeads = await RepairScenarioLeadsAsync(
			tenantId,
			academicYear.Id,
			sources,
			ownerUserIds,
			standards);
		await CurrentUnitOfWork.SaveChangesAsync();

		await RepairFollowUpTasksAsync(tenantId, academicYear.Id, scenarioLeads, ownerUserIds);
		await RepairOperationalScenariosAsync(
			tenantId,
			academicYear.Id,
			configuration,
			scenarioLeads,
			slots,
			ownerUserIds,
			standards);
		await CurrentUnitOfWork.SaveChangesAsync();

		_logger.LogInformation(
			"[AdmissionCrmDemoSeeder] Seeded {LeadCount} focused Enrollment scenarios for tenant {TenantId}, academic year {AcademicYearId}.",
			scenarioLeads.Count,
			tenantId,
			academicYear.Id);
	}

	private async Task<AcademicYear?> GetTargetAcademicYearAsync()
	{
		var currentYear = DateTime.Now.Year.ToString();
		return await _academicYearRepository.FirstOrDefaultAsync(year => year.Name.StartsWith(currentYear)) ??
			await _academicYearRepository.FirstOrDefaultAsync();
	}

	private async Task<AcademicYear?> GetDemoScenarioAcademicYearAsync(Guid tenantId)
	{
		var deterministicLeadIds = LegacyDemoLeadKeys()
			.Select(key => StableId(tenantId, $"lead:{key}"))
			.ToHashSet();
		var existingDemoLeads = await _leadRepository.GetListAsync(item => deterministicLeadIds.Contains(item.Id));
		var existingAcademicYearId = existingDemoLeads
			.GroupBy(item => item.AcademicYearId)
			.OrderByDescending(group => group.Count())
			.Select(group => (Guid?)group.Key)
			.FirstOrDefault();

		if (existingAcademicYearId.HasValue)
		{
			var existingAcademicYear = await _academicYearRepository.FindAsync(existingAcademicYearId.Value);
			if (existingAcademicYear != null)
			{
				return existingAcademicYear;
			}
		}

		return await GetTargetAcademicYearAsync();
	}

	private async Task<IReadOnlyDictionary<string, Standard>> ResolveDemoStandardsAsync(Guid academicYearId)
	{
		var board = await Repository<EducationBoard>().FirstOrDefaultAsync(item =>
			item.AcademicYearId == academicYearId && item.Type == EducationBoardType.CBSE)
			?? throw new InvalidOperationException("The demo Enrollment dataset requires a CBSE board for the selected academic year.");
		var medium = await Repository<EducationMedium>().FirstOrDefaultAsync(item =>
			item.EducationBoardId == board.Id && item.Name == "English")
			?? throw new InvalidOperationException("The demo Enrollment dataset requires the CBSE English medium.");
		var stage = await Repository<EducationMediumStage>().FirstOrDefaultAsync(item =>
			item.EducationMediumId == medium.Id && item.Name == "Primary")
			?? throw new InvalidOperationException("The demo Enrollment dataset requires the CBSE English Primary stage.");
		var standards = await Repository<Standard>().GetListAsync(item =>
			item.EducationMediumStageId == stage.Id && (item.Name == "Class 4" || item.Name == "Class 5"));

		if (standards.Count != 2)
		{
			throw new InvalidOperationException("The demo Enrollment dataset requires Class 4 and Class 5 under CBSE English Primary.");
		}

		return standards.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
	}

	private async Task RepairCycleAsync(
		Guid tenantId,
		Guid academicYearId,
		string academicYearName,
		IReadOnlyDictionary<string, Guid> ownerUserIds)
	{
		var cycle = await _cycleRepository.FirstOrDefaultAsync(item => item.AcademicYearId == academicYearId);
		cycle ??= new AdmissionCrmCycleConfiguration(StableId(tenantId, "cycle"), tenantId, academicYearId, $"Admissions {academicYearName}");
		var ownerPool = new[] { "admissions-reception@ddvs.com", "admissions-counselor@ddvs.com" }
			.Where(ownerUserIds.ContainsKey)
			.Select(email => ownerUserIds[email])
			.Distinct()
			.ToList();
		var defaultOwnerUserId = ownerUserIds.GetValueOrDefault("admissions-counselor@ddvs.com");
		var routingSettingsJson = JsonSerializer.Serialize(new
		{
			isEnabled = ownerPool.Count > 0,
			defaultOwnerUserId = defaultOwnerUserId == Guid.Empty ? (Guid?)null : defaultOwnerUserId,
			routingStrategy = AdmissionCrmLeadRoutingStrategy.RoundRobin,
			ownerPoolUserIds = ownerPool,
			rules = Array.Empty<object>()
		}, new JsonSerializerOptions(JsonSerializerDefaults.Web));
		cycle.Update(
			$"Admissions {academicYearName}",
			true,
			false,
			true,
			30,
			60,
			24,
			7,
			40,
			AdmissionCrmMeritRankingMode.MarksThenInquiryDate,
			AdmissionCrmExamReadinessField.Contact | AdmissionCrmExamReadinessField.AdmissionIntent | AdmissionCrmExamReadinessField.DesiredClass,
			"{\"sourceScores\":{},\"channelScores\":{\"SchoolWebsiteForm\":15,\"WalkInReception\":12,\"QuickCallEntry\":8}}",
			routingSettingsJson,
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

	private async Task<IReadOnlyDictionary<string, Guid>> GetConfiguredOwnerUserIdsAsync()
	{
		var employeeRepository = _serviceProvider.GetRequiredService<IEmployeeRepository>();
		var employees = await employeeRepository.GetListAsync(employee =>
			employee.UserId.HasValue &&
			(!employee.EmployeeStatusType.HasValue || employee.EmployeeStatusType == EmployeeStatusType.Active));

		return employees
			.Where(employee => employee.UserId.HasValue && !string.IsNullOrWhiteSpace(employee.Email))
			.GroupBy(employee => employee.Email!, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(
				group => group.Key,
				group => group.First().UserId!.Value,
				StringComparer.OrdinalIgnoreCase);
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

	private async Task<IReadOnlyList<AdmissionCrmExamSlot>> RepairSlotsAsync(
		Guid tenantId,
		Guid academicYearId,
		string academicYearName)
	{
		var existing = await _slotRepository.GetListAsync(item => item.AcademicYearId == academicYearId);
		var repaired = new List<AdmissionCrmExamSlot>();
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
			repaired.Add(slot);
		}
		return repaired;
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

	private async Task<IReadOnlyDictionary<Guid, AdmissionInquiryLead>> RepairScenarioLeadsAsync(
		Guid tenantId,
		Guid academicYearId,
		IReadOnlyDictionary<string, AdmissionCrmInquirySource> sources,
		IReadOnlyDictionary<string, Guid> ownerUserIds,
		IReadOnlyDictionary<string, Standard> standards)
	{
		var scenarios = DemoLeadSpecs();
		var repaired = (await _leadRepository.GetListAsync(item => item.AcademicYearId == academicYearId))
			.ToDictionary(item => item.Id);
		for (var index = 0; index < scenarios.Count; index++)
		{
			var spec = scenarios[index];
			var id = StableId(tenantId, $"lead:{spec.Key}");
			if (repaired.ContainsKey(id)) continue;
			var source = sources[spec.SourceCode];
			var standard = standards[spec.StandardName];
			var lead = new AdmissionInquiryLead(
				id, tenantId, academicYearId, source.Id, source.DisplayName, spec.Channel,
				spec.Channel == AdmissionInquiryChannel.CampaignLink ? "DDVS Open House" : null,
				"Main Campus", index % 3 == 0 ? "gu" : index % 3 == 1 ? "hi" : "en",
				spec.FirstName, null, spec.LastName, DateTimeOffset.UtcNow.AddYears(spec.StandardName == "Class 4" ? -9 : -10),
				$"Parent of {spec.FirstName}", $"900000{index:0000}", $"admission.crm.{index:00}@ddvs.example",
				"Admission for the upcoming academic cycle", standard.Id, standard.Name,
				$"Focused DDVS scenario: {spec.Key}.", DateTimeOffset.UtcNow.AddDays(-(index + 1)), spec.RouteType);
			lead.AssignOwner(ownerUserIds[spec.OwnerEmail]);
			lead.ChangePriority(spec.Priority);
			if (spec.Stage == AdmissionInquiryStage.Stale) lead.MarkStale();
			else if (spec.Stage == AdmissionInquiryStage.Archived) lead.Archive("Seeded archived scenario.", DateTime.UtcNow.AddDays(-5));
			else if (spec.Stage != AdmissionInquiryStage.NewInquiry) lead.ChangeStage(spec.Stage);
			await _leadRepository.InsertAsync(lead);
			repaired[id] = lead;
		}
		return repaired;
	}

	internal static IReadOnlyList<LeadSpec> DemoLeadSpecs() =>
	[
		new("new-inquiry", "Aarav", "Patel", "Class 4", AdmissionInquiryStage.NewInquiry, "WEBSITE", AdmissionInquiryChannel.SchoolWebsiteForm, "admissions-reception@ddvs.com", AdmissionCrmRouteType.Fresh, AdmissionCrmLeadPriority.Warm),
		new("follow-up", "Diya", "Shah", "Class 5", AdmissionInquiryStage.FollowUp, "PHONE", AdmissionInquiryChannel.QuickCallEntry, "admissions-counselor@ddvs.com", AdmissionCrmRouteType.Fresh, AdmissionCrmLeadPriority.Warm),
		new("exam-scheduled", "Meera", "Joshi", "Class 4", AdmissionInquiryStage.ExamScheduled, "WALK_IN", AdmissionInquiryChannel.WalkInReception, "admissions-counselor@ddvs.com", AdmissionCrmRouteType.Fresh, AdmissionCrmLeadPriority.Warm),
		new("no-show", "Anaya", "Trivedi", "Class 5", AdmissionInquiryStage.ExamScheduled, "STAFF_LINK", AdmissionInquiryChannel.StaffSharedLink, "admissions-counselor@ddvs.com", AdmissionCrmRouteType.Fresh, AdmissionCrmLeadPriority.Hot),
		new("passed", "Aditya", "Bhatt", "Class 4", AdmissionInquiryStage.ExamCompleted, "QR", AdmissionInquiryChannel.QrCodeScan, "admissions-reception@ddvs.com", AdmissionCrmRouteType.Fresh, AdmissionCrmLeadPriority.Warm),
		new("registration-correction", "Riya", "Modi", "Class 5", AdmissionInquiryStage.Eligible, "WEBSITE", AdmissionInquiryChannel.SchoolWebsiteForm, "admissions-reception@ddvs.com", AdmissionCrmRouteType.Direct, AdmissionCrmLeadPriority.Warm),
		new("rte-allotment", "Fatima", "Shaikh", "Class 4", AdmissionInquiryStage.Eligible, "BACK_OFFICE", AdmissionInquiryChannel.ManualBackOffice, "admissions-reception@ddvs.com", AdmissionCrmRouteType.RteEws, AdmissionCrmLeadPriority.Warm),
		new("offer-pending", "Reyansh", "Thakkar", "Class 5", AdmissionInquiryStage.Eligible, "WEBSITE", AdmissionInquiryChannel.SchoolWebsiteForm, "admissions-approver@ddvs.com", AdmissionCrmRouteType.Direct, AdmissionCrmLeadPriority.Hot),
		new("waitlisted", "Krishna", "Vyas", "Class 4", AdmissionInquiryStage.Waitlisted, "CAMPAIGN", AdmissionInquiryChannel.CampaignLink, "admissions-counselor@ddvs.com", AdmissionCrmRouteType.Fresh, AdmissionCrmLeadPriority.Cold),
		new("activation-repair", "Neil", "Gandhi", "Class 5", AdmissionInquiryStage.Eligible, "WALK_IN", AdmissionInquiryChannel.WalkInReception, "admissions-approver@ddvs.com", AdmissionCrmRouteType.Direct, AdmissionCrmLeadPriority.Hot)
	];

	internal static Guid StableId(Guid tenantId, string key)
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
	internal sealed record LeadSpec(
		string Key,
		string FirstName,
		string LastName,
		string StandardName,
		AdmissionInquiryStage Stage,
		string SourceCode,
		AdmissionInquiryChannel Channel,
		string OwnerEmail,
		AdmissionCrmRouteType RouteType,
		AdmissionCrmLeadPriority Priority);
}
