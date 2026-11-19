using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
	[Fact]
	public async Task Proration_Default_Clone_Creates_Independent_Drafts_And_Skips_Occupied_Names()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, _, head) = await SeedStandardAsync();
		var service = GetRequiredService<IProrationPolicyAppService>();
		var input = ProrationCloneInput(year, head);
		var templateHead = await _feeHeadRepo.GetAsync(head);
		for (var number = 2; number <= 5; number++)
		{
			var extraHead = new FeeHead(Guid.NewGuid(), TestTenantId, year, templateHead.FeeCategoryId,
				$"Clone fee {number}", templateHead.Module, true, null, templateHead.IncomeCategoryId);
			await _feeHeadRepo.InsertAsync(extraHead, autoSave: true);
			input.FeeHeadRules.Add(new()
			{
				FeeHeadId = extraHead.Id, RuleType = "MonthlyCutoff", CutoffDay = 13,
				AfterCutoffAction = "NoCharge", ApplyOnAdmission = true, ApplyOnEarlyWithdrawal = true,
				EarlyWithdrawalRuleType = "Daily"
			});
		}
		var source = await service.CreateAsync(input);
		var before = JsonSerializer.Serialize(await service.GetAsync(source.Id));
		var first = await service.CloneAsync(source.Id, new() { TargetAcademicYearId = year });
		await service.ArchiveAsync(first.Id);
		var second = await service.CloneAsync(source.Id, new() { TargetAcademicYearId = year });

		first.PolicyName.ShouldBe(source.PolicyName + " Copy");
		second.PolicyName.ShouldBe(source.PolicyName + " Copy 2");
		foreach (var clone in new[] { first, second })
		{
			clone.Id.ShouldNotBe(source.Id);
			clone.PolicyCode.ShouldNotBe(source.PolicyCode);
			clone.Status.ShouldBe("Draft");
			clone.IsDefault.ShouldBeFalse();
			clone.Version.ShouldBe(1);
			clone.AcademicYearId.ShouldBe(year);
			clone.Description.ShouldBe(source.Description);
			JsonSerializer.Serialize(clone.FeeHeadRules).ShouldBe(JsonSerializer.Serialize(source.FeeHeadRules));
			var versions = await service.GetVersionsAsync(clone.Id, new());
			versions.Items.Last().ChangeAction.ShouldBe("created");
			var repository = GetRequiredService<IRepository<ProrationPolicy, Guid>>();
			var sourceEntity = await repository.GetAsync(source.Id);
			var cloneEntity = await repository.GetAsync(clone.Id);
			cloneEntity.Rules.Select(x => x.Id).Intersect(sourceEntity.Rules.Select(x => x.Id)).ShouldBeEmpty();
			cloneEntity.Rules.ShouldAllBe(x => x.ProrationPolicyId == clone.Id && x.TenantId == TestTenantId);
		}
		JsonSerializer.Serialize(await service.GetAsync(source.Id)).ShouldBe(before);
		(await service.GetVersionsAsync(source.Id, new())).TotalCount.ShouldBe(1);
		(await service.GetListAsync(new() { AcademicYearId = year, Status = "Active" }))
			.Items.ShouldHaveSingleItem().Id.ShouldBe(source.Id);
	}

	[Fact]
	public async Task Proration_Clone_Rejects_Explicit_Normalized_Name_Conflict_Without_Changing_Default()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, _, head) = await SeedStandardAsync();
		var service = GetRequiredService<IProrationPolicyAppService>();
		var source = await service.CreateAsync(ProrationCloneInput(year, head));
		var copy = await service.CloneAsync(source.Id, new());
		await service.ArchiveAsync(copy.Id);
		var error = await Should.ThrowAsync<BusinessException>(() => service.CloneAsync(source.Id,
			new() { Name = "  " + copy.PolicyName.ToUpperInvariant().Replace(" ", "   ") + "  " }));
		error.Code.ShouldBe(ProrationPolicyErrorCodes.DuplicateName);
		error.Data["Location"].ShouldBe("Archived");
		(await service.GetAsync(source.Id)).IsDefault.ShouldBeTrue();
		(await service.GetVersionsAsync(source.Id, new())).TotalCount.ShouldBe(1);
	}

	[Fact]
	public async Task Proration_Clone_Explains_Unavailable_Fee_Heads_Without_Creating_A_Draft()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, _, head) = await SeedStandardAsync();
		var service = GetRequiredService<IProrationPolicyAppService>();
		var source = await service.CreateAsync(ProrationCloneInput(year, head));
		var feeHead = await _feeHeadRepo.GetAsync(head);
		feeHead.SetActiveStatus(false, "No longer offered");
		await _feeHeadRepo.UpdateAsync(feeHead, autoSave: true);

		var error = await Should.ThrowAsync<BusinessException>(() => service.CloneAsync(source.Id, new()));
		error.Code.ShouldBe("Finance:ProrationPolicy:010027");
		error.Data["FeeHeadNames"].ShouldBe(feeHead.Name);
		(await service.GetListAsync(new() { AcademicYearId = year })).TotalCount.ShouldBe(1);
		(await service.GetAsync(source.Id)).IsDefault.ShouldBeTrue();
		(await service.GetVersionsAsync(source.Id, new())).TotalCount.ShouldBe(1);
	}

	[Fact]
	public async Task Proration_Clone_Cannot_Read_Another_Tenants_Default()
	{
		using var reviewer = Reviewer();
		Guid sourceId;
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, _, _, head) = await SeedStandardAsync();
			sourceId = (await GetRequiredService<IProrationPolicyAppService>()
				.CreateAsync(ProrationCloneInput(year, head))).Id;
		}
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var error = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<IProrationPolicyAppService>().CloneAsync(sourceId, new()));
			error.Code.ShouldBe(ProrationPolicyErrorCodes.PolicyNotFound);
			(await GetRequiredService<IRepository<ProrationPolicy, Guid>>().GetCountAsync()).ShouldBe(0);
		}
	}

	[Fact]
	public async Task Proration_Clone_Rejects_Source_Heads_From_Another_Academic_Year()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, _, head) = await SeedStandardAsync();
		var service = GetRequiredService<IProrationPolicyAppService>();
		var source = await service.CreateAsync(ProrationCloneInput(year, head));
		var targetYear = Guid.NewGuid();
		await _academicYearRepo.InsertAsync(new AcademicYear(targetYear, TestTenantId, "2026-27",
			new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), autoSave: true);
		var error = await Should.ThrowAsync<BusinessException>(() => service.CloneAsync(source.Id,
			new() { TargetAcademicYearId = targetYear }));
		error.Code.ShouldBe("Finance:ProrationPolicy:010027");
		(await service.GetListAsync(new() { AcademicYearId = targetYear })).TotalCount.ShouldBe(0);
		(await service.GetAsync(source.Id)).IsDefault.ShouldBeTrue();
	}

	[Fact]
	public async Task Proration_Archived_Clone_Starts_New_Draft_History_Without_Restoring_Source()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, _, head) = await SeedStandardAsync();
		var service = GetRequiredService<IProrationPolicyAppService>();
		var source = await service.CreateAsync(ProrationCloneInput(year, head));
		var archived = await service.CloneAsync(source.Id, new());
		await service.ArchiveAsync(archived.Id);
		var clone = await service.CloneAsync(archived.Id, new());
		clone.Status.ShouldBe("Draft");
		clone.IsDefault.ShouldBeFalse();
		clone.Version.ShouldBe(1);
		(await service.GetAsync(archived.Id)).Status.ShouldBe("Archived");
		(await service.GetAsync(source.Id)).IsDefault.ShouldBeTrue();
		(await service.GetVersionsAsync(clone.Id, new())).TotalCount.ShouldBe(1);
	}

	private static CreateUpdateProrationPolicyDto ProrationCloneInput(Guid year, Guid head) => new()
	{
		AcademicYearId = year,
		PolicyName = "Mid-Session Admission & Withdrawal Proration Policy (2026-27)",
		Description = "Preserve independent admission and withdrawal configuration.",
		Status = "Active",
		IsDefault = true,
		FeeHeadRules = [new()
		{
			FeeHeadId = head,
			RuleType = "Daily",
			RoundingMode = "RoundUpRupee",
			ApplyOnAdmission = true,
			ApplyOnEarlyWithdrawal = true,
			EarlyWithdrawalRuleType = "MonthlyCutoff",
			EarlyWithdrawalCutoffDay = 19,
			EarlyWithdrawalBeforeCutoffAction = "HalfCharge"
		}]
	};
}

public class ProrationPolicyRecoveryResource_Tests
{
	[Theory]
	[InlineData("010001", "Refresh")]
	[InlineData("010007", "Draft or Active")]
	[InlineData("010013", "fee head")]
	[InlineData("010014", "Review")]
	[InlineData("010020", "default")]
	[InlineData("010025", "Active")]
	[InlineData("010026", "another Active")]
	[InlineData("010027", "reactivate")]
	public void Business_Errors_Have_Localized_Recovery_Instructions(string rule, string action)
	{
		var assembly = typeof(ShikshaMitraDomainSharedModule).Assembly;
		using var resource = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames()
			.Single(name => name.EndsWith("Localization.ShikshaMitra.en.json")))!;
		using var messages = JsonDocument.Parse(resource);
		messages.RootElement.GetProperty("Texts").TryGetProperty("Finance:ProrationPolicy:" + rule, out var message)
			.ShouldBeTrue("The business error must have a localized recovery message.");
		message.GetString().ShouldNotBeNull().ShouldContain(action);
	}
}
