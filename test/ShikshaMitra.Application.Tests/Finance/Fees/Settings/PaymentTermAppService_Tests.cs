using System;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

public class PaymentTermAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IPaymentTermAppService _paymentTerms;
	private readonly IRepository<AcademicYear, Guid> _academicYears;
	private readonly IRepository<EducationBoard, Guid> _boards;

	public PaymentTermAppService_Tests()
	{
		_paymentTerms = GetRequiredService<IPaymentTermAppService>();
		_academicYears = GetRequiredService<IRepository<AcademicYear, Guid>>();
		_boards = GetRequiredService<IRepository<EducationBoard, Guid>>();
	}

	[Fact]
	public async Task Draft_Term_Cannot_Be_Default()
	{
		var (yearId, boardId, _) = await SeedAcademicContextAsync();
		var input = BuildInput(yearId, boardId, "Draft default", "DRAFT-DEFAULT");
		input.IsDefault = true;

		var error = await Should.ThrowAsync<BusinessException>(() => _paymentTerms.CreateAsync(input));

		error.Code.ShouldBe(PaymentTermErrorCodes.DefaultTermMustBeActive);
	}

	[Fact]
	public async Task Each_Board_Can_Have_Its_Own_Active_Default()
	{
		var (yearId, firstBoardId, secondBoardId) = await SeedAcademicContextAsync();
		var firstInput = BuildInput(yearId, firstBoardId, "Monthly", "MONTHLY");
		firstInput.Status = PaymentTermSaveStatus.Active;
		firstInput.IsDefault = true;
		var first = await _paymentTerms.CreateAsync(firstInput);

		var secondInput = BuildInput(yearId, secondBoardId, "Quarterly", "QUARTERLY");
		secondInput.Status = PaymentTermSaveStatus.Active;
		secondInput.IsDefault = true;
		var second = await _paymentTerms.CreateAsync(secondInput);

		(await _paymentTerms.GetAsync(first.Id)).IsDefault.ShouldBeTrue();
		(await _paymentTerms.GetAsync(second.Id)).IsDefault.ShouldBeTrue();
	}

	[Fact]
	public async Task New_Active_Default_Replaces_Previous_Default_On_The_Same_Board()
	{
		var (yearId, boardId, _) = await SeedAcademicContextAsync();
		var firstInput = BuildInput(yearId, boardId, "Monthly", "MONTHLY-SAME-BOARD");
		firstInput.Status = PaymentTermSaveStatus.Active;
		firstInput.IsDefault = true;
		var first = await _paymentTerms.CreateAsync(firstInput);

		var secondInput = BuildInput(yearId, boardId, "Quarterly", "QUARTERLY-SAME-BOARD");
		secondInput.Status = PaymentTermSaveStatus.Active;
		secondInput.IsDefault = true;
		var second = await _paymentTerms.CreateAsync(secondInput);

		(await _paymentTerms.GetAsync(first.Id)).IsDefault.ShouldBeFalse();
		(await _paymentTerms.GetAsync(second.Id)).IsDefault.ShouldBeTrue();
	}

	[Fact]
	public async Task Saving_Default_As_Draft_Clears_Default_Flag()
	{
		var (yearId, boardId, _) = await SeedAcademicContextAsync();
		var input = BuildInput(yearId, boardId, "Monthly", "MONTHLY-DRAFT");
		input.Status = PaymentTermSaveStatus.Active;
		input.IsDefault = true;
		var created = await _paymentTerms.CreateAsync(input);

		var updated = await _paymentTerms.UpdateDraftAsync(created.Id, new()
		{
			Name = input.Name,
			Code = input.Code,
			IsDefault = false,
			Status = PaymentTermSaveStatus.Draft,
			HolidayShiftingMethod = input.HolidayShiftingMethod,
			DemandGenerationRule = input.DemandGenerationRule,
			Installments = input.Installments
		});

		updated.Status.ShouldBe(PaymentTermLifecycleStatus.Draft);
		updated.IsDefault.ShouldBeFalse();
	}

	private async Task<(Guid YearId, Guid FirstBoardId, Guid SecondBoardId)> SeedAcademicContextAsync()
	{
		var yearId = Guid.NewGuid();
		var firstBoardId = Guid.NewGuid();
		var secondBoardId = Guid.NewGuid();
		var start = new DateTime(2026, 1, 1);
		var end = new DateTime(2027, 12, 31);
		await _academicYears.InsertAsync(new AcademicYear(yearId, null, $"Test {yearId:N}", start, end), autoSave: true);
		await _boards.InsertAsync(new EducationBoard(firstBoardId, null, EducationBoardType.CBSE, yearId, start, end), autoSave: true);
		await _boards.InsertAsync(new EducationBoard(secondBoardId, null, EducationBoardType.GSEB, yearId, start, end), autoSave: true);
		return (yearId, firstBoardId, secondBoardId);
	}

	private static CreatePaymentTermDto BuildInput(Guid yearId, Guid boardId, string name, string code) => new()
	{
		AcademicYearId = yearId,
		BoardId = boardId,
		Name = name,
		Code = code,
		Status = PaymentTermSaveStatus.Draft,
		HolidayShiftingMethod = HolidayShiftingMethod.None,
		DemandGenerationRule = DemandGenerationRule.OnInstallmentStartDate,
		Installments =
		[
			new()
			{
				Sequence = 1,
				Name = "Annual",
				CoverageStartDate = new DateTime(2026, 1, 1),
				CoverageEndDate = new DateTime(2027, 12, 31),
				DemandDate = new DateTime(2026, 1, 1),
				DueDate = new DateTime(2026, 1, 10),
				AllocationPercentage = 100m,
				DemandEnabled = true
			}
		]
	};
}
