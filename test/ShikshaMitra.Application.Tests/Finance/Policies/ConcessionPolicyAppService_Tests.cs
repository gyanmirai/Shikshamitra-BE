using Shouldly;
using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;
using Volo.Abp;

namespace ShikshaMitra.Domain.Finance.Policies;

public class ConcessionPolicyAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IConcessionPolicyAppService _concessionPolicies;
	private readonly IGuidGenerator _guidGenerator;
	private readonly IRepository<ConcessionType, Guid> _concessionTypes;

	public ConcessionPolicyAppService_Tests()
	{
		_concessionPolicies = GetRequiredService<IConcessionPolicyAppService>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_concessionTypes = GetRequiredService<IRepository<ConcessionType, Guid>>();
	}

	private static CreateUpdateConcessionPolicyDto BuildInput(Guid academicYearId, string name) => new()
	{
		AcademicYearId = academicYearId,
		PolicyName = name,
		Status = "Draft",
		ValueSource = "RequestEnteredWithinLimits",
		CalculationMethod = "Percentage",
		MaximumPercentage = 25,
		ApplyOn = "AllOutstanding",
		RoundingMethod = "None"
	};

	[Fact]
	public async Task List_Should_Separate_Current_And_Archived_Policies()
	{
		var academicYearId = _guidGenerator.Create();
		var current = await _concessionPolicies.CreateAsync(
			BuildInput(academicYearId, "Current Concession Policy"));
		var archived = await _concessionPolicies.CreateAsync(
			BuildInput(academicYearId, "Archived Concession Policy"));
		await _concessionPolicies.ArchiveAsync(archived.Id);

		var currentPage = await _concessionPolicies.GetListAsync(new ConcessionPolicyListRequestDto
		{
			AcademicYearId = academicYearId,
			MaxResultCount = 20
		});
		var archivedPage = await _concessionPolicies.GetListAsync(new ConcessionPolicyListRequestDto
		{
			AcademicYearId = academicYearId,
			Status = "Archived",
			MaxResultCount = 20
		});

		currentPage.Items.ShouldContain(x => x.Id == current.Id);
		currentPage.Items.ShouldNotContain(x => x.Id == archived.Id);
		archivedPage.Items.ShouldContain(x => x.Id == archived.Id);
		archivedPage.Items.ShouldNotContain(x => x.Id == current.Id);
	}

	[Fact]
	public async Task First_Active_Default_Is_Protected_And_Update_Transfers_It_With_Version_History()
	{
		var year = _guidGenerator.Create();
		var firstInput = BuildInput(year, "First active concession");
		firstInput.Status = "Active";
		var first = await _concessionPolicies.CreateAsync(firstInput);
		first.IsDefault.ShouldBeTrue();
		var rejected = await Should.ThrowAsync<BusinessException>(() => _concessionPolicies.ArchiveAsync(first.Id));
		rejected.Code.ShouldBe(ConcessionPolicyErrorCodes.DefaultPolicyCannotBeRemoved);

		var secondInput = BuildInput(year, "Replacement concession");
		secondInput.Status = "Active";
		var second = await _concessionPolicies.CreateAsync(secondInput);
		second.IsDefault.ShouldBeFalse();
		secondInput.IsDefault = true;
		secondInput.ConcurrencyStamp = second.ConcurrencyStamp;
		secondInput.ChangeReason = "Finance committee selected a new default.";
		(await _concessionPolicies.UpdateAsync(second.Id, secondInput)).IsDefault.ShouldBeTrue();
		(await _concessionPolicies.GetAsync(first.Id)).IsDefault.ShouldBeFalse();
		var history = await _concessionPolicies.GetVersionsAsync(first.Id, new FinancePolicyVersionListRequestDto());
		history.Items.ShouldContain(x => x.ChangeAction == "default_replaced");
		await _concessionPolicies.ArchiveAsync(first.Id);
	}

	[Fact]
	public async Task Selecting_A_New_Default_Replaces_The_Previous_Default()
	{
		var academicYearId = _guidGenerator.Create();
		var concessionType = new ConcessionType(_guidGenerator.Create(), null, academicYearId,
			"Default policy type");
		await _concessionTypes.InsertAsync(concessionType, autoSave: true);
		var firstInput = BuildInput(academicYearId, "First Default Concession");
		firstInput.Status = "Active";
		firstInput.IsDefault = true;
		firstInput.ApplicableConcessionTypeIds = [concessionType.Id];
		var first = await _concessionPolicies.CreateAsync(firstInput);

		var secondInput = BuildInput(academicYearId, "Second Default Concession");
		secondInput.Status = "Active";
		secondInput.IsDefault = true;
		secondInput.ApplicableConcessionTypeIds = [concessionType.Id];
		var second = await _concessionPolicies.CreateAsync(secondInput);

		(await _concessionPolicies.GetAsync(first.Id)).IsDefault.ShouldBeFalse();
		(await _concessionPolicies.GetAsync(second.Id)).IsDefault.ShouldBeTrue();
	}
}
