using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.Security.UserScope;
using System.Security.Claims;
using Volo.Abp.Security.Claims;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

public class PaymentTermNameValidation_Tests
{
	[Fact]
	public void Names_Are_Trimmed_Before_Request_Length_Validation()
	{
		var create = new CreatePaymentTermDto { Name = " \t9th PT One \t" };
		var update = new UpdatePaymentTermDraftDto { Name = " \t9th PT One \t" };
		create.Name.ShouldBe("9th PT One");
		update.Name.ShouldBe("9th PT One");
		create.Name = "  ab  ";
		var errors = new List<ValidationResult>();
		Validator.TryValidateProperty(create.Name,
			new ValidationContext(create) { MemberName = nameof(create.Name) }, errors).ShouldBeFalse();
		create.Name = " \t ";
		Validator.TryValidateProperty(create.Name,
			new ValidationContext(create) { MemberName = nameof(create.Name) }, []).ShouldBeFalse();
		create.Name = $" \t{new string('N', 100)} \t";
		Validator.TryValidateProperty(create.Name,
			new ValidationContext(create) { MemberName = nameof(create.Name) }, []).ShouldBeTrue();
		update.Name = $" \t{new string('N', 100)} \t";
		Validator.TryValidateProperty(update.Name,
			new ValidationContext(update) { MemberName = nameof(update.Name) }, []).ShouldBeTrue();
	}
}

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

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Name_Uniqueness_Ignores_Whitespace_And_Case_Across_Boards(bool archived)
	{
		var (yearId, boardId, otherBoardId) = await SeedAcademicContextAsync();
		var original = await _paymentTerms.CreateAsync(BuildInput(yearId, boardId, "9th PT One", "PT-ONE"));
		if (archived) await _paymentTerms.ArchiveAsync(original.Id, new() { Reason = "Schedule retired" });
		var error = await Should.ThrowAsync<BusinessException>(() => _paymentTerms.CreateAsync(
			BuildInput(yearId, otherBoardId, " \t9TH \u00a0 PT ONE  ", "DIFFERENT-CODE")));
		error.Code.ShouldBe("Finance:PaymentTerm:000042");
		(await _paymentTerms.GetAsync(original.Id)).Name.ShouldBe("9th PT One");
	}

	[Fact]
	public async Task Update_Rejects_Duplicate_Name_But_Allows_Own_Trimmed_Name()
	{
		var (yearId, boardId, _) = await SeedAcademicContextAsync();
		await _paymentTerms.CreateAsync(BuildInput(yearId, boardId, "9th PT One", "PT-ONE"));
		var input = BuildInput(yearId, boardId, "9th PT Two", "PT-TWO");
		var other = await _paymentTerms.CreateAsync(input);
		var update = new UpdatePaymentTermDraftDto {
			Name = " 9th pt one\t", Code = input.Code, Status = PaymentTermSaveStatus.Draft,
			DemandGenerationRule = input.DemandGenerationRule, Installments = input.Installments
		};
		(await Should.ThrowAsync<BusinessException>(() => _paymentTerms.UpdateDraftAsync(other.Id, update)))
			.Code.ShouldBe("Finance:PaymentTerm:000042");
		update.Name = " 9th PT Two\t";
		(await _paymentTerms.UpdateDraftAsync(other.Id, update)).Name.ShouldBe("9th PT Two");
	}

	[Fact]
	public async Task Same_Name_Is_Allowed_In_Another_Year_And_Tenant_And_Is_Trimmed()
	{
		var (yearId, boardId, _) = await SeedAcademicContextAsync();
		await _paymentTerms.CreateAsync(BuildInput(yearId, boardId, "9th PT One", "PT-ONE"));
		var (nextYearId, nextBoardId, _) = await SeedAcademicContextAsync();
		(await _paymentTerms.CreateAsync(BuildInput(nextYearId, nextBoardId, " 9th PT One\t", "PT-ONE")))
			.Name.ShouldBe("9th PT One");
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var (tenantYearId, tenantBoardId, _) = await SeedAcademicContextAsync(tenantId);
		(await _paymentTerms.CreateAsync(BuildInput(tenantYearId, tenantBoardId, "9th PT One", "PT-ONE")))
			.Name.ShouldBe("9th PT One");
	}

	[Fact]
	public async Task Clone_Skips_A_Duplicate_Name_Even_With_A_Different_Code()
	{
		var (sourceYearId, sourceBoardId, _) = await SeedAcademicContextAsync();
		var source = await _paymentTerms.CreateAsync(BuildInput(sourceYearId, sourceBoardId, "9th PT One", "SOURCE-CODE"));
		var (targetYearId, targetBoardId, _) = await SeedAcademicContextAsync();
		await _paymentTerms.CreateAsync(BuildInput(targetYearId, targetBoardId, "9TH PT ONE", "TARGET-CODE"));
		var clone = await _paymentTerms.CloneAsync(new() {
			SourceAcademicYearId = sourceYearId, TargetAcademicYearId = targetYearId,
			PaymentTermIds = [source.Id]
		});
		clone.ClonedTermIds.ShouldBeEmpty();
		clone.ConflictedCodes.ShouldContain("SOURCE-CODE");
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
	public async Task Installment_Ranges_RoundTrip_After_Create_And_Edit()
	{
		var (yearId, boardId, _) = await SeedAcademicContextAsync();
		var input = BuildInput(yearId, boardId, "Range round trip", "RANGE-ROUND-TRIP");
		input.Installments[0].CoverageStartDate = new DateTime(2026, 6, 1);
		input.Installments[0].CoverageEndDate = new DateTime(2026, 12, 31);
		input.Installments[0].DemandDate = new DateTime(2026, 6, 1);
		input.Installments[0].DueDate = new DateTime(2026, 6, 15);
		input.Installments[0].AllocationPercentage = 50m;
		input.Installments.Add(new()
		{
			Sequence = 2,
			Name = "Second",
			CoverageStartDate = new DateTime(2027, 1, 1),
			CoverageEndDate = new DateTime(2027, 5, 31),
			DemandDate = new DateTime(2027, 1, 1),
			DueDate = new DateTime(2027, 1, 15),
			AllocationPercentage = 50m,
			DemandEnabled = true
		});
		var created = await _paymentTerms.CreateAsync(input);
		var reopened = await _paymentTerms.GetAsync(created.Id);
		var saved = reopened.Versions.Single(version => version.Status == PaymentTermVersionStatus.Draft)
			.Installments.OrderBy(installment => installment.Sequence).ToList();
		saved.Count.ShouldBe(2);
		saved[0].CoverageStartDate.ShouldBe(new DateTime(2026, 6, 1));
		saved[0].CoverageEndDate.ShouldBe(new DateTime(2026, 12, 31));
		saved[1].CoverageStartDate.ShouldBe(new DateTime(2027, 1, 1));
		saved[1].CoverageEndDate.ShouldBe(new DateTime(2027, 5, 31));

		input.Installments[0].CoverageStartDate = new DateTime(2026, 6, 2);
		input.Installments[0].DemandDate = new DateTime(2026, 6, 2);
		input.Installments[1].CoverageEndDate = new DateTime(2027, 5, 30);
		await _paymentTerms.UpdateDraftAsync(created.Id, new()
		{
			Name = input.Name,
			Code = input.Code,
			Status = PaymentTermSaveStatus.Draft,
			HolidayShiftingMethod = input.HolidayShiftingMethod,
			DemandGenerationRule = input.DemandGenerationRule,
			Installments = input.Installments
		});
		reopened = await _paymentTerms.GetAsync(created.Id);
		saved = reopened.Versions.Single(version => version.Status == PaymentTermVersionStatus.Draft)
			.Installments.OrderBy(installment => installment.Sequence).ToList();
		saved[0].CoverageStartDate.ShouldBe(new DateTime(2026, 6, 2));
		saved[0].CoverageEndDate.ShouldBe(new DateTime(2026, 12, 31));
		saved[1].CoverageStartDate.ShouldBe(new DateTime(2027, 1, 1));
		saved[1].CoverageEndDate.ShouldBe(new DateTime(2027, 5, 30));
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

	[Fact]
	public async Task Quarterly_Template_Previews_A_Complete_Valid_Schedule()
	{
		var (_, boardId, _) = await SeedAcademicContextAsync();

		var preview = await _paymentTerms.PreviewTemplateAsync(new PreviewPaymentTermTemplateDto
		{
			BoardId = boardId,
			TemplateKind = PaymentTermTemplateKind.Quarterly,
			DueDateRule = PaymentTermTemplateDueDateRule.CoverageEnd,
			DemandGenerationRule = DemandGenerationRule.OnInstallmentStartDate
		});

		preview.InstallmentCount.ShouldBe(4);
		preview.Installments.Sum(item => item.AllocationPercentage).ShouldBe(100m);
		preview.Installments[0].CoverageStartDate.ShouldBe(preview.BoardStartDate);
		preview.Installments[0].CoverageEndDate.ShouldBe(new DateTime(2026, 6, 30));
		preview.Installments[^1].CoverageEndDate.ShouldBe(preview.BoardEndDate);
		for (var index = 1; index < preview.Installments.Count; index++)
		{
			preview.Installments[index].CoverageStartDate.ShouldBe(
				preview.Installments[index - 1].CoverageEndDate.AddDays(1));
		}
	}

	[Fact]
	public async Task Custom_Template_Uses_Requested_Count_And_Stable_Remainder()
	{
		var (_, boardId, _) = await SeedAcademicContextAsync();

		var preview = await _paymentTerms.PreviewTemplateAsync(new PreviewPaymentTermTemplateDto
		{
			BoardId = boardId,
			TemplateKind = PaymentTermTemplateKind.Custom,
			CustomInstallmentCount = 3,
			DueDateRule = PaymentTermTemplateDueDateRule.DaysAfterCoverageStart,
			DueDaysAfterCoverageStart = 10,
			DemandGenerationRule = DemandGenerationRule.OnInstallmentStartDate
		});

		preview.Installments.Select(item => item.AllocationPercentage)
			.ShouldBe([33.33m, 33.33m, 33.34m]);
		preview.Installments.ShouldAllBe(item => item.DemandDate == item.CoverageStartDate);
	}

	[Fact]
	public async Task Template_Offset_Uses_The_Smallest_Gap_And_Includes_Invoice_Dates()
	{
		var (_, boardId, _) = await SeedAcademicContextAsync();
		var input = new PreviewPaymentTermTemplateDto
		{
			BoardId = boardId,
			TemplateKind = PaymentTermTemplateKind.Monthly,
			DueDateRule = PaymentTermTemplateDueDateRule.CoverageEnd,
			DemandGenerationRule = DemandGenerationRule.XDaysBeforeDue,
			DemandOffsetDays = 27
		};
		var preview = await _paymentTerms.PreviewTemplateAsync(input);
		preview.Installments.ShouldAllBe(item => item.DemandDate == item.DueDate.AddDays(-27));
		preview.Installments.ShouldAllBe(item => item.DemandDate >= item.CoverageStartDate && item.DemandDate <= item.DueDate);
		input.DemandOffsetDays = 28;
		var error = await Should.ThrowAsync<BusinessException>(() => _paymentTerms.PreviewTemplateAsync(input));
		error.Code.ShouldBe(PaymentTermErrorCodes.DemandOffsetDaysExceedsMaximum);
		error.Data["Maximum"].ShouldBe(27);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(3)]
	public async Task Specific_Date_Template_Requires_Exactly_One_Date_Per_Installment(int dateCount)
	{
		var (_, boardId, _) = await SeedAcademicContextAsync();
		var error = await Should.ThrowAsync<BusinessException>(() => _paymentTerms.PreviewTemplateAsync(new()
		{
			BoardId = boardId,
			TemplateKind = PaymentTermTemplateKind.HalfYearly,
			DemandGenerationRule = DemandGenerationRule.SpecificDate,
			SpecificDemandDates = Enumerable.Repeat(new DateTime(2026, 4, 1), dateCount).ToList()
		}));
		error.Code.ShouldBe(PaymentTermErrorCodes.InvalidTemplateConfiguration);
	}

	[Fact]
	public async Task Specific_Date_Template_Uses_Explicit_Dates_And_Rejects_Out_Of_Range()
	{
		var (_, boardId, _) = await SeedAcademicContextAsync();
		var input = new PreviewPaymentTermTemplateDto
		{
			BoardId = boardId,
			TemplateKind = PaymentTermTemplateKind.HalfYearly,
			DemandGenerationRule = DemandGenerationRule.SpecificDate,
			SpecificDemandDates = [new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)]
		};
		var preview = await _paymentTerms.PreviewTemplateAsync(input);
		preview.Installments.Select(item => item.DemandDate!.Value).ShouldBe(input.SpecificDemandDates);
		input.SpecificDemandDates[1] = new DateTime(2026, 12, 31);
		(await Should.ThrowAsync<BusinessException>(() => _paymentTerms.PreviewTemplateAsync(input)))
			.Code.ShouldBe(PaymentTermErrorCodes.DemandDateBeforeCoverageStart);
		input.SpecificDemandDates[1] = new DateTime(2028, 1, 1);
		(await Should.ThrowAsync<BusinessException>(() => _paymentTerms.PreviewTemplateAsync(input)))
			.Code.ShouldBe(PaymentTermErrorCodes.DemandDateAfterDueDate);
	}

	[Fact]
	public async Task Create_Rejects_Invoice_Before_Installment_Start()
	{
		var (yearId, boardId, _) = await SeedAcademicContextAsync();
		var input = BuildInput(yearId, boardId, "Invalid invoice", "INVALID-INVOICE");
		input.DemandGenerationRule = DemandGenerationRule.SpecificDate;
		input.Installments[0].CoverageStartDate = new DateTime(2026, 6, 1);
		input.Installments[0].DemandDate = new DateTime(2026, 2, 26);
		input.Installments[0].DueDate = new DateTime(2026, 6, 5);

		var error = await Should.ThrowAsync<BusinessException>(() => _paymentTerms.CreateAsync(input));
		error.Code.ShouldBe(PaymentTermErrorCodes.DemandDateBeforeCoverageStart);
	}

	[Fact]
	public async Task Update_Rejects_Excessive_Offset()
	{
		var (yearId, boardId, _) = await SeedAcademicContextAsync();
		var input = BuildInput(yearId, boardId, "Valid schedule", "VALID-SCHEDULE");
		var created = await _paymentTerms.CreateAsync(input);
		var error = await Should.ThrowAsync<BusinessException>(() => _paymentTerms.UpdateDraftAsync(created.Id, new()
		{
			Name = input.Name,
			Code = input.Code,
			Status = PaymentTermSaveStatus.Draft,
			DemandGenerationRule = DemandGenerationRule.XDaysBeforeDue,
			DemandOffsetDays = 99,
			Installments = input.Installments
		}));

		error.Code.ShouldBe(PaymentTermErrorCodes.DemandOffsetDaysExceedsMaximum);
	}

	[Fact]
	public async Task Default_Impact_Reports_Current_Published_Version_And_Audits_Replacement()
	{
		var (yearId, boardId, _) = await SeedAcademicContextAsync();
		var firstInput = BuildInput(yearId, boardId, "Half-Yearly", "HALF-YEARLY-IMPACT");
		firstInput.Status = PaymentTermSaveStatus.Active;
		firstInput.IsDefault = true;
		var first = await _paymentTerms.CreateAsync(firstInput);
		var preview = await _paymentTerms.GetDefaultImpactAsync(yearId, boardId);
		preview.CurrentDefaultId.ShouldBe(first.Id);
		preview.CurrentDefaultName.ShouldBe("Half-Yearly");
		preview.CurrentDefaultVersionNumber.ShouldBe(1);
		preview.ReplacesExistingDefault.ShouldBeTrue();
		preview.CurrentDefaultUsage!.FeeHeadCount.ShouldBe(0);
		(await _paymentTerms.GetDefaultImpactAsync(yearId, boardId, first.Id)).ReplacesExistingDefault.ShouldBeFalse();
		var secondInput = BuildInput(yearId, boardId, "Monthly", "MONTHLY-IMPACT");
		secondInput.Status = PaymentTermSaveStatus.Active;
		secondInput.IsDefault = true;
		var second = await _paymentTerms.CreateAsync(secondInput);
		var events = await GetRequiredService<IRepository<FinanceAuditEvent, Guid>>()
			.GetListAsync(e => e.EntityId == second.Id && e.EntityType == FinanceAuditEntityTypes.PaymentTerm);
		events.Count.ShouldBe(1);
		events[0].SubModuleCode.ShouldBe(FinanceAuditSubModuleCodes.PaymentTerms);
		events[0].ContextJson.ShouldNotBeNull().ShouldContain("Half-Yearly");
		events[0].ContextJson.ShouldNotBeNull().ShouldContain("Monthly");
		(await _paymentTerms.GetAsync(first.Id)).Versions.Single().Id.ShouldBe(first.Versions.Single().Id);
	}

	[Fact]
	public async Task Usage_Counts_Distinct_Heads_And_Versions_And_Default_Replacement_Preserves_Assignments()
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var userId = Guid.NewGuid();
		await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new IdentityUser(userId, "term-reviewer", "term-reviewer@example.test", tenantId), autoSave: true);
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: userId), autoSave: true);
		using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.UserName, "term-reviewer"), new Claim(AbpClaimTypes.TenantId, tenantId.ToString()) }, "Test")));
		var (yearId, boardId, _) = await SeedAcademicContextAsync(tenantId);
		var mediumId = Guid.NewGuid(); var stageId = Guid.NewGuid(); var standardId = Guid.NewGuid();
		await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(new EducationMedium(mediumId, tenantId, "English", boardId), autoSave: true);
		await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(new EducationMediumStage(stageId, tenantId, "Primary", null, mediumId), autoSave: true);
		await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(new Standard(standardId, tenantId, "Class 3", 8, stageId, null), autoSave: true);
		var metadata = new FeeStructureMetadata(Guid.NewGuid(), tenantId, yearId, boardId, mediumId, null, "Term usage");
		await GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(metadata, autoSave: true);
		var category = new FeeCategory(Guid.NewGuid(), tenantId, yearId, "Usage category", null);
		await GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(category, autoSave: true);
		var incomeId = await FeeHeadAccountingTestData.CreateIncomeCategoryAsync(GetRequiredService<IRepository<AccountHead, Guid>>(), GetRequiredService<IRepository<TransactionCategory, Guid>>(), yearId, tenantId, "Term usage", new DateTimeOffset(DateTime.UtcNow));
		var head = new FeeHead(Guid.NewGuid(), tenantId, yearId, category.Id, "School Fees", FeeHeadModule.Finance, true, null, incomeId);
		await GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(head, autoSave: true);
		var input = BuildInput(yearId, boardId, "Monthly", "MONTHLY-PINNED"); input.Status = PaymentTermSaveStatus.Active; input.IsDefault = true;
		var term = await _paymentTerms.CreateAsync(input);
		var version1 = term.Versions.Single().Id;
		var first = new FeeStructure(Guid.NewGuid(), tenantId, metadata.Id, standardId, "FS 1");
		var firstDetail = new FeeStructureDetail(Guid.NewGuid(), tenantId, first.Id, head.Id, 1000m, false, false, null, null);
		firstDetail.AttachPaymentTerm(term.Id, version1); first.FeeStructureDetails.Add(firstDetail); first.Lock();
		var structures = GetRequiredService<IRepository<FeeStructure, Guid>>();
		await structures.InsertAsync(first, autoSave: true);
		var revised = await _paymentTerms.UpdateDraftAsync(term.Id, new UpdatePaymentTermDraftDto { Name = input.Name, Code = input.Code, IsDefault = true, Status = PaymentTermSaveStatus.Active, HolidayShiftingMethod = input.HolidayShiftingMethod, DemandGenerationRule = input.DemandGenerationRule, Installments = input.Installments });
		var version2 = revised.Versions.Single(v => v.VersionNumber == 2).Id;
		var second = new FeeStructure(Guid.NewGuid(), tenantId, metadata.Id, standardId, "FS 2");
		var secondDetail = new FeeStructureDetail(Guid.NewGuid(), tenantId, second.Id, head.Id, 1000m, false, false, null, null);
		secondDetail.AttachPaymentTerm(term.Id, version2); second.FeeStructureDetails.Add(secondDetail); second.Lock();
		await structures.InsertAsync(second, autoSave: true);
		var usage = await _paymentTerms.GetUsageAsync(term.Id);
		usage.FeeHeadCount.ShouldBe(1); usage.FeeStructureCount.ShouldBe(2); usage.ClassCount.ShouldBe(1); usage.Assignments.Count.ShouldBe(2);
		(await _paymentTerms.GetUsageAsync(term.Id, version1)).FeeStructureCount.ShouldBe(1);
		var replacement = BuildInput(yearId, boardId, "Quarterly", "QUARTERLY-PINNED"); replacement.Status = PaymentTermSaveStatus.Active; replacement.IsDefault = true;
		await _paymentTerms.CreateAsync(replacement);
		(await structures.GetAsync(first.Id)).FeeStructureDetails.Single().PaymentTermVersionId.ShouldBe(version1);
		(await structures.GetAsync(second.Id)).FeeStructureDetails.Single().PaymentTermVersionId.ShouldBe(version2);
		(await _paymentTerms.GetUsageAsync(term.Id)).FeeStructureCount.ShouldBe(2);
	}

	[Fact]
	public async Task Usage_Rejects_A_Version_From_Another_Term()
	{
		var (yearId, boardId, _) = await SeedAcademicContextAsync();
		var term = await _paymentTerms.CreateAsync(BuildInput(yearId, boardId, "Usage", "USAGE-VERSION"));
		var error = await Should.ThrowAsync<BusinessException>(() => _paymentTerms.GetUsageAsync(term.Id, Guid.NewGuid()));
		error.Code.ShouldBe(PaymentTermErrorCodes.NotFound);
		var usage = await _paymentTerms.GetUsageAsync(term.Id, term.Versions.Single().Id);
		usage.VersionId.ShouldBe(term.Versions.Single().Id);
		usage.Assignments.ShouldBeEmpty();
	}

	private async Task<(Guid YearId, Guid FirstBoardId, Guid SecondBoardId)> SeedAcademicContextAsync(Guid? tenantId = null)
	{
		var yearId = Guid.NewGuid();
		var firstBoardId = Guid.NewGuid();
		var secondBoardId = Guid.NewGuid();
		var start = new DateTime(2026, 1, 1);
		var end = new DateTime(2027, 12, 31);
		await _academicYears.InsertAsync(new AcademicYear(yearId, tenantId, $"Test {yearId:N}", start, end), autoSave: true);
		await _boards.InsertAsync(new EducationBoard(firstBoardId, tenantId, EducationBoardType.CBSE, yearId, start, end), autoSave: true);
		await _boards.InsertAsync(new EducationBoard(secondBoardId, tenantId, EducationBoardType.GSEB, yearId, start, end), autoSave: true);
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
