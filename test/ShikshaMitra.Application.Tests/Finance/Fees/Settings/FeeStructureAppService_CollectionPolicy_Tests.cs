using System.Security.Claims;
using Volo.Abp.Security.Claims;
using Volo.Abp.Identity;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Shouldly;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
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
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

/// <summary>
/// Verifies FeeStructure ↔ CollectionPolicy attachment actually persists through the real
/// product endpoints — this was previously a gap: CollectionPolicy.AttachCollectionPolicy()
/// existed on the domain aggregate (Phase 1) but no AppService/controller ever called it, so
/// FeePoliciesTab.tsx's "Collection Policy" dropdown had nothing real to save to.
/// </summary>
public partial class FeeStructureAppService_CollectionPolicy_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestUserId = Guid.Parse("66666666-6666-6666-6666-666666666666");
	private static readonly Guid TestTenantId = Guid.Parse("55555555-5555-5555-5555-555555555555");

	private readonly IFeeStructureAppService _feeStructures;
	private readonly ICollectionPolicyAppService _collectionPolicies;
	private readonly IConcessionPolicyAppService _concessionPolicies;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepo;
	private readonly IRepository<EducationBoard, Guid> _boardRepo;
	private readonly IRepository<EducationMedium, Guid> _mediumRepo;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepo;
	private readonly IRepository<Standard, Guid> _standardRepo;
	private readonly IRepository<FeeStructureMetadata, Guid> _metadataRepo;
	private readonly IRepository<FeeCategory, Guid> _feeCategoryRepo;
	private readonly IRepository<FeeHead, Guid> _feeHeadRepo;
	private readonly ICurrentTenant _currentTenant;

	public FeeStructureAppService_CollectionPolicy_Tests()
	{
		_feeStructures = GetRequiredService<IFeeStructureAppService>();
		_collectionPolicies = GetRequiredService<ICollectionPolicyAppService>();
		_concessionPolicies = GetRequiredService<IConcessionPolicyAppService>();
		_academicYearRepo = GetRequiredService<IRepository<AcademicYear, Guid>>();
		_boardRepo = GetRequiredService<IRepository<EducationBoard, Guid>>();
		_mediumRepo = GetRequiredService<IRepository<EducationMedium, Guid>>();
		_stageRepo = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_standardRepo = GetRequiredService<IRepository<Standard, Guid>>();
		_metadataRepo = GetRequiredService<IRepository<FeeStructureMetadata, Guid>>();
		_feeCategoryRepo = GetRequiredService<IRepository<FeeCategory, Guid>>();
		_feeHeadRepo = GetRequiredService<IRepository<FeeHead, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	private async Task<(Guid AcademicYearId, Guid StandardId, Guid MetadataId, Guid FeeHeadId)> SeedStandardAsync()
	{
		await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new IdentityUser(TestUserId, "fee-reviewer", "fee-reviewer@example.test", TestTenantId), autoSave: true);
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: TestUserId), autoSave: true);
		var yearId = Guid.NewGuid();
		var boardId = Guid.NewGuid();
		var mediumId = Guid.NewGuid();
		var stageId = Guid.NewGuid();

		await _academicYearRepo.InsertAsync(
			new AcademicYear(yearId, TestTenantId, "2025-26", new DateTime(2025, 4, 1), new DateTime(2026, 3, 31)),
			autoSave: true);
		await _boardRepo.InsertAsync(new EducationBoard(boardId, TestTenantId, EducationBoardType.CBSE, yearId, new DateTime(2025, 4, 1), new DateTime(2026, 3, 31)), autoSave: true);
		await _mediumRepo.InsertAsync(new EducationMedium(mediumId, TestTenantId, "English", boardId), autoSave: true);
		await _stageRepo.InsertAsync(
			new EducationMediumStage(stageId, TestTenantId, "Primary", bannerPicture: null, mediumId), autoSave: true);

		var standard = new Standard(Guid.NewGuid(), TestTenantId, "Standard I", 5, stageId, null);
		await _standardRepo.InsertAsync(standard, autoSave: true);
		var metadata = new FeeStructureMetadata(
			Guid.NewGuid(), TestTenantId, yearId, boardId, mediumId, null, "Standard I fees");
		await _metadataRepo.InsertAsync(metadata, autoSave: true);
		var category = new FeeCategory(Guid.NewGuid(), TestTenantId, yearId, "Tuition", null);
		await _feeCategoryRepo.InsertAsync(category, autoSave: true);
		var incomeCategoryId = await FeeHeadAccountingTestData.CreateIncomeCategoryAsync(
			GetRequiredService<IRepository<AccountHead, Guid>>(),
			GetRequiredService<IRepository<TransactionCategory, Guid>>(),
			yearId, TestTenantId, "Structure Tuition");
		var feeHead = new FeeHead(
			Guid.NewGuid(), TestTenantId, yearId, category.Id, "Tuition Fee",
			FeeHeadModule.Finance, true, null, incomeCategoryId);
		await _feeHeadRepo.InsertAsync(feeHead, autoSave: true);

		return (yearId, standard.Id, metadata.Id, feeHead.Id);
	}

	private async Task<Guid> CreateCollectionPolicyAsync(Guid academicYearId, Guid feeHeadId, string name)
	{
		var policy = await _collectionPolicies.CreateAsync(new CreateUpdateCollectionPolicyDto
		{
			AcademicYearId = academicYearId,
			PolicyName = name,
			PolicyCode = $"CP-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
			Status = "Active",
			EnableLateFee = true,
			GracePeriodDays = 7,
			LateFeeApplyOnFeeHeads = new() { feeHeadId },
			LateFeeType = "PerDay",
			AmountPerDay = 10,
			PercentageBase = "OutstandingAmount",
			CompoundFrequency = "Daily",
			RoundingMethod = "None",
			AllowOnlineCollection = true,
		});
		return policy.Id;
	}

	private async Task<Guid> CreateConcessionPolicyAsync(Guid academicYearId, string name)
	{
		var policy = await _concessionPolicies.CreateAsync(new CreateUpdateConcessionPolicyDto
		{
			AcademicYearId = academicYearId,
			PolicyName = name,
			PolicyCode = $"CNP-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
			Status = "Active",
			ValueSource = "PolicyFixed",
			CalculationMethod = "Percentage",
			PercentageValue = 10,
			ApplyOn = "AllOutstanding",
			RoundingMethod = "None"
		});
		return policy.Id;
	}

	private static List<FeeHeadAssignmentRequest> BuildFeeHeads(Guid feeHeadId) =>
		[new FeeHeadAssignmentRequest { FeeHeadId = feeHeadId, Amount = 1_000 }];

	[Fact]
	public async Task Create_Persists_The_Attached_Collection_Policy()
	{
		using (GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, TestUserId.ToString()) }, "Test"))))
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, standardId, metadataId, feeHeadId) = await SeedStandardAsync();
			var policyId = await CreateCollectionPolicyAsync(academicYearId, feeHeadId, "Attach-on-create policy");
			var concessionPolicyId = await CreateConcessionPolicyAsync(academicYearId, "Attach-on-create concession");

			var created = await _feeStructures.CreateAsync(metadataId, new StandardFeeStructureSaveRequest
			{
				StandardId = standardId,
				StandardFeeStructures = new StandardFeeStructureRequest
				{
					Name = "Standard I Fee Structure",
					FeeHeads = BuildFeeHeads(feeHeadId),
					CollectionPolicyId = policyId,
					ConcessionPolicyId = concessionPolicyId,
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
		using (GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, TestUserId.ToString()) }, "Test"))))
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, standardId, metadataId, feeHeadId) = await SeedStandardAsync();
			var firstPolicyId = await CreateCollectionPolicyAsync(academicYearId, feeHeadId, "First policy");
			var secondPolicyId = await CreateCollectionPolicyAsync(academicYearId, feeHeadId, "Second policy");
			var concessionPolicyId = await CreateConcessionPolicyAsync(academicYearId, "Update concession");

			var created = await _feeStructures.CreateAsync(metadataId, new StandardFeeStructureSaveRequest
			{
				StandardId = standardId,
				StandardFeeStructures = new StandardFeeStructureRequest
				{
					Name = "Standard I Fee Structure",
					FeeHeads = BuildFeeHeads(feeHeadId),
					CollectionPolicyId = firstPolicyId,
					ConcessionPolicyId = concessionPolicyId,
				},
			});

			await _feeStructures.UpdateAsync(created.Id, new StandardFeeStructureRequest
			{
				ConcurrencyStamp = created.ConcurrencyStamp,
				Name = "Standard I Fee Structure",
				FeeHeads = BuildFeeHeads(feeHeadId),
				CollectionPolicyId = secondPolicyId,
				ConcessionPolicyId = concessionPolicyId,
			});

			var response = await _feeStructures.GetByStandardAsync(standardId);
			var structure = response.StandardFeeStructures.Single(fs => fs.Id == created.Id);
			structure.CollectionPolicyId.ShouldBe(secondPolicyId);
		}
	}

	[Fact]
	public async Task Update_With_Null_Collection_Policy_Is_Rejected()
	{
		using (GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, TestUserId.ToString()) }, "Test"))))
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, standardId, metadataId, feeHeadId) = await SeedStandardAsync();
			var policyId = await CreateCollectionPolicyAsync(academicYearId, feeHeadId, "Required policy");
			var concessionPolicyId = await CreateConcessionPolicyAsync(academicYearId, "Required concession");

			var created = await _feeStructures.CreateAsync(metadataId, new StandardFeeStructureSaveRequest
			{
				StandardId = standardId,
				StandardFeeStructures = new StandardFeeStructureRequest
				{
					Name = "Standard I Fee Structure",
					FeeHeads = BuildFeeHeads(feeHeadId),
					CollectionPolicyId = policyId,
					ConcessionPolicyId = concessionPolicyId,
				},
			});

			await Should.ThrowAsync<BusinessException>(() => _feeStructures.UpdateAsync(created.Id, new StandardFeeStructureRequest
			{
				Name = "Standard I Fee Structure",
				FeeHeads = BuildFeeHeads(feeHeadId),
				CollectionPolicyId = null,
				SaveAsDraft = false,
				ConcurrencyStamp = created.ConcurrencyStamp,
				ConcessionPolicyId = concessionPolicyId,
			}));

			var response = await _feeStructures.GetByStandardAsync(standardId);
			var structure = response.StandardFeeStructures.Single(fs => fs.Id == created.Id);
			structure.CollectionPolicyId.ShouldBe(policyId);
		}
	}

	[Fact]
	public async Task Policy_Heads_Outside_The_Structure_Are_Ignored_Without_Blocking_Attachment()
	{
		using (GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, TestUserId.ToString()) }, "Test"))))
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, standardId, metadataId, structureFeeHeadId) = await SeedStandardAsync();
			var policyCategory = new FeeCategory(
				Guid.NewGuid(), TestTenantId, academicYearId, "Policy-only category", null);
			await _feeCategoryRepo.InsertAsync(policyCategory, autoSave: true);
			var incomeCategoryId = await FeeHeadAccountingTestData.CreateIncomeCategoryAsync(
				GetRequiredService<IRepository<AccountHead, Guid>>(),
				GetRequiredService<IRepository<TransactionCategory, Guid>>(),
				academicYearId, TestTenantId, "Policy Only");
			var policyOnlyFeeHead = new FeeHead(
				Guid.NewGuid(), TestTenantId, academicYearId, policyCategory.Id, "Policy-only Fee",
				FeeHeadModule.Finance, true, null, incomeCategoryId);
			await _feeHeadRepo.InsertAsync(policyOnlyFeeHead, autoSave: true);
			var policyId = await CreateCollectionPolicyAsync(
				academicYearId, policyOnlyFeeHead.Id, "Reusable policy");
			var concessionPolicyId = await CreateConcessionPolicyAsync(
				academicYearId, "Reusable-policy concession");

			var impact = await _feeStructures.ValidateCollectionPolicyAttachmentAsync(
				new CollectionPolicyAttachmentImpactRequest
				{
					FeeStructureMetadataId = metadataId,
					CollectionPolicyId = policyId,
					FeeHeadIds = [structureFeeHeadId]
				});

			impact.IsAttachable.ShouldBeTrue();
			impact.CoveredLateFeeHeadIds.ShouldBeEmpty();
			impact.StructureHeadsWithoutLateFee.ShouldContain(structureFeeHeadId);
			impact.PolicyLateFeeHeadsNotInStructure.ShouldContain(policyOnlyFeeHead.Id);
			impact.Warnings.ShouldContain(x => x.Contains("No late fee will be charged"));

			var created = await _feeStructures.CreateAsync(metadataId, new StandardFeeStructureSaveRequest
			{
				StandardId = standardId,
				StandardFeeStructures = new StandardFeeStructureRequest
				{
					Name = "Structure with a reusable policy",
					FeeHeads = BuildFeeHeads(structureFeeHeadId),
					CollectionPolicyId = policyId,
					ConcessionPolicyId = concessionPolicyId
				}
			});

			var response = await _feeStructures.GetByStandardAsync(standardId);
			response.StandardFeeStructures.Single(x => x.Id == created.Id)
				.CollectionPolicyId.ShouldBe(policyId);
		}
	}
}
