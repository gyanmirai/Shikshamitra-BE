using Microsoft.Extensions.Logging;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Adds reusable demo payment schedules for every seeded academic board.
/// Existing terms are matched by academic year and code, making the seeder safe to rerun.
/// </summary>
public class PaymentTermDemoSeederContributor : BaseSeederContributor
{
	public const string StepName = "PaymentTermDemoSeeder";
	public const string StepVersion = "PaymentTermDemoSeeder:v1";
	public const int StepOrder = 152;

	private static readonly PaymentTermTemplate[] Templates =
	[
		new("Monthly", "MONTHLY", "Twelve equal monthly installments.", 12),
		new("Quarterly", "QUARTERLY", "Four equal quarterly installments.", 4),
		new("Half-Yearly", "HALF-YEARLY", "Two equal half-yearly installments.", 2)
	];

	private readonly IRepository<PaymentTerm, Guid> _paymentTermRepository;
	private readonly IEducationBoardRepository _boardRepository;
	private readonly ILogger<PaymentTermDemoSeederContributor> _logger;

	public PaymentTermDemoSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IRepository<PaymentTerm, Guid> paymentTermRepository,
		IEducationBoardRepository boardRepository,
		ILogger<PaymentTermDemoSeederContributor> logger)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_paymentTermRepository = paymentTermRepository;
		_boardRepository = boardRepository;
		_logger = logger;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		var boards = await _boardRepository.GetListAsync();
		var existingTerms = await _paymentTermRepository.GetListAsync();
		var existingKeys = existingTerms
			.Select(term => (term.AcademicYearId, term.Code))
			.ToHashSet();
		var boardsWithDefault = existingTerms
			.Where(term => term.IsDefault && term.Status != PaymentTermStatus.Archived)
			.Select(term => (term.AcademicYearId, term.BoardId))
			.ToHashSet();
		var termsToInsert = new List<PaymentTerm>();

		foreach (var board in boards.OrderBy(item => item.StartDate).ThenBy(item => item.Type))
		{
			foreach (var template in Templates)
			{
				var code = BuildCode(board.Type.ToString(), template.Code);
				if (existingKeys.Contains((board.AcademicYearId, code)))
				{
					continue;
				}

				var boardKey = (board.AcademicYearId, board.Id);
				var isDefault = template.Code == "MONTHLY" && !boardsWithDefault.Contains(boardKey);
				var term = BuildTerm(board, template, code, isDefault);
				termsToInsert.Add(term);
				existingKeys.Add((board.AcademicYearId, code));
				if (isDefault)
				{
					boardsWithDefault.Add(boardKey);
				}
			}
		}

		if (termsToInsert.Count > 0)
		{
			await _paymentTermRepository.InsertManyAsync(termsToInsert, autoSave: true);
		}

		_logger.LogInformation(
			"[PaymentTermDemoSeeder] Ensured demo payment terms. Boards={BoardCount}, Inserted={InsertedCount}",
			boards.Count,
			termsToInsert.Count);
	}

	private PaymentTerm BuildTerm(
		EducationBoard board,
		PaymentTermTemplate template,
		string code,
		bool isDefault)
	{
		var termId = GuidGenerator.Create();
		var versionId = GuidGenerator.Create();
		var term = new PaymentTerm(
			termId,
			CurrentTenant.Id,
			board.AcademicYearId,
			board.Id,
			template.Name,
			code,
			template.Description,
			isDefault,
			HolidayShiftingMethod.NextWorkingDay,
			DemandGenerationRule.OnInstallmentStartDate,
			demandOffsetDays: null,
			InitialDemandStatus.Published);
		var version = new PaymentTermVersion(
			versionId,
			CurrentTenant.Id,
			termId,
			versionNumber: 1,
			PaymentTermVersionStatus.Draft,
			HolidayShiftingMethod.NextWorkingDay,
			DemandGenerationRule.OnInstallmentStartDate,
			demandOffsetDays: null,
			effectiveFrom: null,
			effectiveTo: null,
			changeReason: "Initial demo schedule");

		version.SetInstallments(BuildInstallments(versionId, board, template));
		version.Publish(board.StartDate, Guid.Empty);
		term.AddVersion(version);
		return term;
	}

	private List<PaymentTermInstallment> BuildInstallments(
		Guid versionId,
		EducationBoard board,
		PaymentTermTemplate template)
	{
		var installments = new List<PaymentTermInstallment>(template.InstallmentCount);
		var monthsPerInstallment = 12 / template.InstallmentCount;
		var baseAllocation = decimal.Round(100m / template.InstallmentCount, 4, MidpointRounding.AwayFromZero);
		var allocated = 0m;

		for (var index = 0; index < template.InstallmentCount; index++)
		{
			var coverageStart = board.StartDate.Date.AddMonths(index * monthsPerInstallment);
			var coverageEnd = index == template.InstallmentCount - 1
				? board.EndDate.Date
				: coverageStart.AddMonths(monthsPerInstallment).AddDays(-1);
			var allocation = index == template.InstallmentCount - 1
				? 100m - allocated
				: baseAllocation;
			allocated += allocation;

			installments.Add(new PaymentTermInstallment(
				GuidGenerator.Create(),
				versionId,
				index + 1,
				BuildInstallmentName(template, index + 1, coverageStart),
				coverageStart,
				coverageEnd,
				coverageStart,
				coverageStart.AddDays(9) <= coverageEnd ? coverageStart.AddDays(9) : coverageEnd,
				allocation,
				demandEnabled: true,
				reminderEnabled: true,
				reminderDate: coverageStart.AddDays(6) <= coverageEnd ? coverageStart.AddDays(6) : null));
		}

		return installments;
	}

	private static string BuildInstallmentName(PaymentTermTemplate template, int sequence, DateTime startDate) =>
		template.InstallmentCount == 12
			? startDate.ToString("MMM yyyy")
			: $"{template.Name} {sequence}";

	private static string BuildCode(string boardName, string templateCode)
	{
		var code = $"DEMO-{boardName}-{templateCode}".ToUpperInvariant();
		return code.Length <= 24 ? code : code[..24];
	}

	private sealed record PaymentTermTemplate(
		string Name,
		string Code,
		string Description,
		int InstallmentCount);
}
