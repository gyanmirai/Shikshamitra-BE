using Shouldly;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

/// <summary>
/// Verifies FeeStructure ↔ CollectionPolicy attachment actually persists through the real
/// product endpoints — this was previously a gap: CollectionPolicy.AttachCollectionPolicy()
/// existed on the domain aggregate (Phase 1) but no AppService/controller ever called it, so
/// FeePoliciesTab.tsx's "Collection Policy" dropdown had nothing real to save to.
/// </summary>
public class FeeStructureAppService_CollectionPolicy_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("55555555-5555-5555-5555-555555555555");

	private readonly IFeeStructureAppService _feeStructures;
	private readonly ICollectionPolicyAppService _collectionPolicies;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepo;
	private readonly IRepository<EducationBoard, Guid> _boardRepo;
	private readonly IRepository<EducationMedium, Guid> _mediumRepo;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepo;
	private readonly IRepository<Standard, Guid> _standardRepo;
	private readonly ICurrentTenant _currentTenant;

	public FeeStructureAppService_CollectionPolicy_Tests()
	{
		_feeStructures = GetRequiredService<IFeeStructureAppService>();
		_collectionPolicies = GetRequiredService<ICollectionPolicyAppService>();
		_academicYearRepo = GetRequiredService<IRepository<AcademicYear, Guid>>();
		_boardRepo = GetRequiredService<IRepository<EducationBoard, Guid>>();
		_mediumRepo = GetRequiredService<IRepository<EducationMedium, Guid>>();
		_stageRepo = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_standardRepo = GetRequiredService<IRepository<Standard, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	private async Task<(Guid AcademicYearId, Guid StandardId)> SeedStandardAsync()
	{
		var yearId = Guid.NewGuid();
		var boardId = Guid.NewGuid();
		var mediumId = Guid.NewGuid();
		var stageId = Guid.NewGuid();

		await _academicYearRepo.InsertAsync(
			new AcademicYear(yearId, TestTenantId, "2025-26", new DateTime(2025, 4, 1), new DateTime(2026, 3, 31)),
			autoSave: true);
		await _boardRepo.InsertAsync(new EducationBoard(boardId, TestTenantId, EducationBoardType.CBSE, yearId), autoSave: true);
		await _mediumRepo.InsertAsync(new EducationMedium(mediumId, TestTenantId, "English", boardId), autoSave: true);
		await _stageRepo.InsertAsync(
			new EducationMediumStage(stageId, TestTenantId, "Primary", bannerPicture: null, mediumId), autoSave: true);

		var standard = new Standard(Guid.NewGuid(), TestTenantId, "Standard I", 5, stageId, null);
		await _standardRepo.InsertAsync(standard, autoSave: true);

		return (yearId, standard.Id);
	}

	private async Task<Guid> CreateCollectionPolicyAsync(Guid academicYearId, string name)
	{
		var policy = await _collectionPolicies.CreateAsync(new CreateUpdateCollectionPolicyDto
		{
			AcademicYearId = academicYearId,
			PolicyName = name,
			PolicyCode = $"CP-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
			Status = "Active",
			EnableLateFee = true,
			GracePeriodDays = 7,
			LateFeeType = "PerDay",
			AmountPerDay = 10,
			PercentageBase = "OutstandingAmount",
			CompoundFrequency = "Daily",
			RoundingMethod = "None",
			AllowOnlineCollection = true,
		});
		return policy.Id;
	}

	[Fact]
	public async Task Create_Persists_The_Attached_Collection_Policy()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, standardId) = await SeedStandardAsync();
			var policyId = await CreateCollectionPolicyAsync(academicYearId, "Attach-on-create policy");

			var created = await _feeStructures.CreateAsync(Guid.NewGuid(), new StandardFeeStructureSaveRequest
			{
				StandardId = standardId,
				StandardFeeStructures = new StandardFeeStructureRequest
				{
					Name = "Standard I Fee Structure",
					FeeHeads = new List<FeeHeadAssignmentRequest>(),
					CollectionPolicyId = policyId,
				},
			});

			var response = await _feeStructures.GetByStandardAsync(standardId);
			var structure = response.StandardFeeStructures.Single(fs => fs.Id == created.Id);
			structure.CollectionPolicyId.ShouldBe(policyId);
		}
	}

	[Fact]
	public async Task Update_Changes_The_Attached_Collection_Policy()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, standardId) = await SeedStandardAsync();
			var firstPolicyId = await CreateCollectionPolicyAsync(academicYearId, "First policy");
			var secondPolicyId = await CreateCollectionPolicyAsync(academicYearId, "Second policy");

			var created = await _feeStructures.CreateAsync(Guid.NewGuid(), new StandardFeeStructureSaveRequest
			{
				StandardId = standardId,
				StandardFeeStructures = new StandardFeeStructureRequest
				{
					Name = "Standard I Fee Structure",
					FeeHeads = new List<FeeHeadAssignmentRequest>(),
					CollectionPolicyId = firstPolicyId,
				},
			});

			await _feeStructures.UpdateAsync(created.Id, new StandardFeeStructureRequest
			{
				Name = "Standard I Fee Structure",
				FeeHeads = new List<FeeHeadAssignmentRequest>(),
				CollectionPolicyId = secondPolicyId,
			});

			var response = await _feeStructures.GetByStandardAsync(standardId);
			var structure = response.StandardFeeStructures.Single(fs => fs.Id == created.Id);
			structure.CollectionPolicyId.ShouldBe(secondPolicyId);
		}
	}

	[Fact]
	public async Task Update_With_Null_Clears_The_Attachment()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, standardId) = await SeedStandardAsync();
			var policyId = await CreateCollectionPolicyAsync(academicYearId, "Clearable policy");

			var created = await _feeStructures.CreateAsync(Guid.NewGuid(), new StandardFeeStructureSaveRequest
			{
				StandardId = standardId,
				StandardFeeStructures = new StandardFeeStructureRequest
				{
					Name = "Standard I Fee Structure",
					FeeHeads = new List<FeeHeadAssignmentRequest>(),
					CollectionPolicyId = policyId,
				},
			});

			await _feeStructures.UpdateAsync(created.Id, new StandardFeeStructureRequest
			{
				Name = "Standard I Fee Structure",
				FeeHeads = new List<FeeHeadAssignmentRequest>(),
				CollectionPolicyId = null,
			});

			var response = await _feeStructures.GetByStandardAsync(standardId);
			var structure = response.StandardFeeStructures.Single(fs => fs.Id == created.Id);
			structure.CollectionPolicyId.ShouldBeNull();
		}
	}
}
