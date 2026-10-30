using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShikshaMitra.Common;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.FeeRefundTests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Seeders;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.ConcessionRequests;

[Collection(ConcessionUpdateApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ConcessionRequestUpdateApi_Tests : ConcessionRequestApiTestsBase
{
	public ConcessionRequestUpdateApi_Tests(ConcessionUpdateApiTestFactory factory) : base(factory)
	{
		FeeRefundTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
		FeeRefundTestHelper.DefaultBoardId = factory.SeededBoardId;
		FeeRefundTestHelper.DefaultMediumId = factory.SeededMediumId;
		FeeRefundTestHelper.DefaultMediumStageId = factory.SeededMediumStageId;
		FeeRefundTestHelper.DefaultStandardId = factory.SeededStandardId;
		FeeRefundTestHelper.DefaultDivisionId = factory.SeededDivisionId;
		FeeCollectionTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
		FeeCollectionTestHelper.DefaultMediumId = factory.SeededMediumId;
		FeeCollectionTestHelper.DefaultStandardId = factory.SeededStandardId;
		FeeCollectionTestHelper.DefaultDivisionId = factory.SeededDivisionId;
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Fixed_policy_draft_update_returns_the_persisted_stamp_and_can_be_submitted_immediately()
	{
		var (studentId, originalDueId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		Guid assignmentId;
		Guid policyId;
		Guid versionId;
		var headIds = new List<Guid>();
		using (var scope = Factory.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
			using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var dues = services.GetRequiredService<IDueFeeRepository>();
			var original = await dues.GetAsync(originalDueId);
			assignmentId = original.StudentFeeStructureId!.Value;
			// The refund helper intentionally seeds only an assignment header. Context
			// also resolves its structure, so provide that real prerequisite here.
			var assignment = await services.GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetAsync(assignmentId);
			var metadata = new FeeStructureMetadata(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
				Factory.SeededBoardId, Factory.SeededMediumId, null, $"Benefit structure {Guid.NewGuid():N}");
			await services.GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(metadata);
			await services.GetRequiredService<IRepository<FeeStructure, Guid>>().InsertAsync(new FeeStructure(
				assignment.FeeStructureStandardId, tenantId, metadata.Id, Factory.SeededStandardId, metadata.Name!));
			var heads = services.GetRequiredService<IRepository<FeeHead, Guid>>();
			var originalHead = await heads.GetAsync(original.FeeHeadId);
			var policy = new ConcessionPolicy(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
				new ConcessionPolicyUpdateModel($"Fixed update {Guid.NewGuid():N}", $"FU{Guid.NewGuid():N}"[..20], null,
					ConcessionPolicyStatus.Active, false, ConcessionValueSource.PolicyFixed,
					ConcessionCalculationMethod.FixedAmount, null, 1000m, null, null,
					ConcessionApplyOn.AllFees, RoundingMethodType.RoundUp, AutoCreateRefund: false));
			await services.GetRequiredService<IRepository<ConcessionPolicy, Guid>>().InsertAsync(policy);
			var version = new FinancePolicyVersion(Guid.NewGuid(), tenantId, "concession", policy.Id,
				policy.AcademicYearId, policy.Version, policy.PolicyName, "Active", JsonSerializer.Serialize(policy.ToSnapshot()), "created");
			await services.GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().InsertAsync(version);
			policyId = policy.Id;
			versionId = version.Id;
			foreach (var amount in new[] { 58166m, 717m })
			{
				var head = new FeeHead(Guid.NewGuid(), tenantId, policy.AcademicYearId, originalHead.FeeCategoryId,
					$"Update head {amount}", originalHead.Module, true, null, originalHead.IncomeCategoryId);
				await heads.InsertAsync(head);
				headIds.Add(head.Id);
				var due = new DueFee(Guid.NewGuid(), tenantId, studentId, head.Id, MonthEnum.January,
					2026, false, amount, DateTime.UtcNow);
				due.BindAssignment(assignmentId);
				due.ApplyPayment(amount == 717m ? amount : 1000m);
				await dues.InsertAsync(due);
			}
			await uow.CompleteAsync();
		}
		var draftInput = new CreateConcessionDraftDto
		{
			StudentId = studentId, StudentFeeStructureId = assignmentId, ConcessionTypeId = typeId,
			ConcessionPolicyId = policyId, ConcessionPolicyVersionId = versionId,
		};
		var draft = await ConcessionRequestTestHelper.CreateDraftAsync(Client, draftInput);
		var inputs = headIds.Select(id => new FeeHeadConcessionDto { FeeHeadId = id, BasedOn = 0, Amount = 0m }).ToList();
		var update = await Client.PutAsJsonAsync($"/api/concession-request/{draft.ConcessionRequestId}?academicYearId={Factory.SeededAcademicYearId}",
			new UpdateConcessionRequestDto
			{
				StudentId = studentId, StudentFeeStructureId = assignmentId, ConcessionTypeId = typeId,
				ConcessionPolicyId = policyId, ConcessionPolicyVersionId = versionId,
				ConcurrencyStamp = draft.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid().ToString("N"),
				FeeHeadConcessions = inputs, Remarks = "Changed saved draft",
			}, JsonOptions);
		update.EnsureSuccessStatusCode();
		var saved = (await update.Content.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions))!.Data!;
		var persisted = (await Client.GetFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(
			$"/api/concession-request/{draft.ConcessionRequestId}", JsonOptions))!.Data!;
		saved.ConcurrencyStamp.ShouldBe(persisted.ConcurrencyStamp);
		saved.FeeHeadConcessions.Sum(x => x.DiscountAmount).ShouldBe(1000m);
		var stale = await Client.PutAsJsonAsync($"/api/concession-request/{draft.ConcessionRequestId}?academicYearId={Factory.SeededAcademicYearId}",
			new UpdateConcessionRequestDto
			{
				StudentId = studentId, StudentFeeStructureId = assignmentId, ConcessionTypeId = typeId,
				ConcessionPolicyId = policyId, ConcessionPolicyVersionId = versionId,
				ConcurrencyStamp = draft.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid().ToString("N"),
				FeeHeadConcessions = inputs, Remarks = "Stale editor must not overwrite",
			}, JsonOptions);
		stale.IsSuccessStatusCode.ShouldBeFalse();
		using (var failure = JsonDocument.Parse(await stale.Content.ReadAsStringAsync()))
		{
			failure.RootElement.GetProperty("errorCode").GetString().ShouldBe(FinanceConcessionErrorCodes.ConcurrencyConflict);
			failure.RootElement.GetProperty("detail").GetString()!.ShouldContain("changed elsewhere");
			failure.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
		}
		var previewResponse = await Client.PostAsJsonAsync($"/api/concession-request/preview?academicYearId={Factory.SeededAcademicYearId}",
			new ConcessionRequestPreviewInputDto
			{
				StudentId = studentId, StudentFeeStructureId = assignmentId, ConcessionTypeId = typeId,
				ConcessionPolicyId = policyId, ConcessionPolicyVersionId = versionId, FeeHeadConcessions = inputs,
			}, JsonOptions);
		previewResponse.EnsureSuccessStatusCode();
		var preview = (await previewResponse.Content.ReadFromJsonAsync<ConcessionRequestPreviewDto>(JsonOptions))!;
		var submitInput = new SubmitConcessionRequestDto { ConcurrencyStamp = saved.ConcurrencyStamp,
			PreviewFingerprint = preview.PreviewFingerprint, IdempotencyKey = Guid.NewGuid().ToString("N") };
		var submit = await Client.PostAsJsonAsync($"/api/concession-request/{draft.ConcessionRequestId}/submit", submitInput, JsonOptions);
		submit.EnsureSuccessStatusCode();
		var submitReplay = await Client.PostAsJsonAsync($"/api/concession-request/{draft.ConcessionRequestId}/submit", submitInput, JsonOptions);
		submitReplay.EnsureSuccessStatusCode();
		(await submitReplay.Content.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions))!.Data!.ConcessionRequestId.ShouldBe(draft.ConcessionRequestId);
		// Preserve a pre-existing historical collision: an earlier approved ₹200
		// benefit must block the later pending request without rewriting either row.
		var historicalId = Guid.NewGuid();
		var pendingId = draft.ConcessionRequestId;
		decimal concessionBefore;
		using (var scope = Factory.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
			using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var requests = services.GetRequiredService<IConcessionRequestRepository>();
			var current = await requests.GetWithDetailsAsync(draft.ConcessionRequestId);
			var due = (await services.GetRequiredService<IDueFeeRepository>().GetListAsync(x =>
				x.StudentId == studentId && x.FeeHeadId == headIds[0])).Single();
			var priorPolicy = new ConcessionPolicy(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
				new ConcessionPolicyUpdateModel($"Earlier 200 {Guid.NewGuid():N}", $"OLD{Guid.NewGuid():N}"[..20], null,
					ConcessionPolicyStatus.Active, false, ConcessionValueSource.PolicyFixed,
					ConcessionCalculationMethod.FixedAmount, null, 200m, null, null,
					ConcessionApplyOn.AllFees, RoundingMethodType.RoundUp, AutoCreateRefund: false));
			await services.GetRequiredService<IRepository<ConcessionPolicy, Guid>>().InsertAsync(priorPolicy);
			var priorVersion = new FinancePolicyVersion(Guid.NewGuid(), tenantId, "concession", priorPolicy.Id,
				priorPolicy.AcademicYearId, priorPolicy.Version, priorPolicy.PolicyName, "Active",
				JsonSerializer.Serialize(priorPolicy.ToSnapshot()), "created");
			await services.GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().InsertAsync(priorVersion);
			var historical = new ConcessionRequest(historicalId, tenantId, studentId, assignmentId,
				priorPolicy.Id, priorVersion.Id, typeId, current.AcademicYearId, current.BoardId,
				current.EducationMediumId, current.StandardId, current.DivisionId,
				"Earlier approved benefit retained for audit", ConcessionRequestStatus.Pending);
			historical.ConcessionFeeHeadMappings.Add(new ConcessionFeeHeadMapping(Guid.NewGuid(), tenantId,
				historicalId, due.FeeHeadId, 58166m, 200m, 57966m, DiscountBasis.FlatAmount, 200m));
			historical.ApproveOnAdministratorCreation(Guid.NewGuid(), "Earlier administrator approval",
				[new ConcessionDueFeeAllocation(Guid.NewGuid(), tenantId, historicalId, due.Id, due.FeeHeadId, 200m)], DateTime.UtcNow);
			if (current.Status != ConcessionRequestStatus.Pending)
			{
				pendingId = Guid.NewGuid();
				var pending = new ConcessionRequest(pendingId, tenantId, studentId, assignmentId, policyId,
					versionId, typeId, current.AcademicYearId, current.BoardId, current.EducationMediumId,
					current.StandardId, current.DivisionId, "Existing pending request", ConcessionRequestStatus.Draft);
				foreach (var mapping in current.ConcessionFeeHeadMappings)
					pending.ConcessionFeeHeadMappings.Add(new ConcessionFeeHeadMapping(Guid.NewGuid(), tenantId,
						pendingId, mapping.FeeHeadId, mapping.PreConcessionFeeHeadAmount, mapping.FeeHeadConcessionAmount,
						mapping.PostConcessionFeeHeadAmount, mapping.UserSpecifiedConcessionAmountType, mapping.UserSpecifiedConcessionAmountValue));
				pending.PrepareSubmissionEvidence(current.GetSelectedDueFeeIds(), current.RequestInputSnapshotJson!, current.TypeRequirementsSnapshotJson!);
				pending.Submit(DateTime.UtcNow);
				await requests.InsertAsync(pending);
			}
			due.ApplyConcession(200m);
			concessionBefore = due.ConcessionAmount;
			await requests.InsertAsync(historical);
			await uow.CompleteAsync();
		}
		var context = await Client.GetFromJsonAsync<ConcessionRequestContextDto>(
			$"/api/concession-request/context?studentId={studentId}&academicYearId={Factory.SeededAcademicYearId}&requestId={draft.ConcessionRequestId}", JsonOptions);
		var blocker = context!.ConcessionTypes.Single(x => x.ConcessionTypeId == typeId).BenefitBlocker;
		blocker.ShouldNotBeNull();
		blocker.Code.ShouldBe(FinanceConcessionErrorCodes.BenefitAlreadyGranted);
		blocker.BlockingRequestId.ShouldBe(historicalId);
		blocker.BlockingStatus.ShouldBe("Approved");
		var draftReplay = await Client.PostAsJsonAsync($"/api/concession-request/drafts?academicYearId={Factory.SeededAcademicYearId}", draftInput, JsonOptions);
		draftReplay.EnsureSuccessStatusCode();
		(await draftReplay.Content.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions))!.Data!.ConcessionRequestId.ShouldBe(draft.ConcessionRequestId);
		// Read-side self exclusion is never authorization to exclude history from a create.
		var duplicate = await Client.PostAsJsonAsync($"/api/concession-request/drafts?academicYearId={Factory.SeededAcademicYearId}",
			new { studentId, studentFeeStructureId = assignmentId, concessionTypeId = typeId,
				concessionPolicyId = policyId, concessionPolicyVersionId = versionId, requestId = historicalId,
				idempotencyKey = Guid.NewGuid().ToString("N"), feeHeadConcessions = inputs }, JsonOptions);
		duplicate.IsSuccessStatusCode.ShouldBeFalse();
		using (var failure = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync()))
		{
			failure.RootElement.GetProperty("errorCode").GetString().ShouldBe(FinanceConcessionErrorCodes.BenefitAlreadyGranted);
			failure.RootElement.GetProperty("detail").GetString()!.ShouldContain("approved benefit");
			failure.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
		}
		{
			using var checker = await CreateBenefitCheckerClientAsync();
			var approvalContext = await checker.GetFromJsonAsync<ConcessionApprovalContextDto>(
				$"/api/concession-request/{pendingId}/approval-context", JsonOptions);
			approvalContext!.BenefitBlocker!.Code.ShouldBe(FinanceConcessionErrorCodes.BenefitAlreadyGranted);
			var review = await checker.PostAsJsonAsync($"/api/concession-requests/{pendingId}/approval-preview",
				new PreviewConcessionApprovalDto { RequestConcurrencyStamp = approvalContext.RequestConcurrencyStamp,
					PreviewFingerprint = approvalContext.PreviewFingerprint }, JsonOptions);
			review.IsSuccessStatusCode.ShouldBeFalse();
			using (var reviewFailure = JsonDocument.Parse(await review.Content.ReadAsStringAsync()))
				reviewFailure.RootElement.GetProperty("errorCode").GetString().ShouldBe(FinanceConcessionErrorCodes.BenefitAlreadyGranted);
			var approval = await checker.PostAsJsonAsync($"/api/concession-requests/{pendingId}/approve",
				new ApproveConcessionRequestDto { RequestConcurrencyStamp = approvalContext.RequestConcurrencyStamp,
					PreviewFingerprint = approvalContext.PreviewFingerprint, IdempotencyKey = Guid.NewGuid().ToString("N") }, JsonOptions);
			approval.IsSuccessStatusCode.ShouldBeFalse();
			using var failure = JsonDocument.Parse(await approval.Content.ReadAsStringAsync());
			failure.RootElement.GetProperty("errorCode").GetString().ShouldBe(FinanceConcessionErrorCodes.BenefitAlreadyGranted);
			var rejection = await checker.PostAsJsonAsync($"/api/concession-requests/{pendingId}/reject",
				new RejectConcessionRequestDto { RequestConcurrencyStamp = approvalContext.RequestConcurrencyStamp,
					Message = "Remove existing duplicate pending request", IdempotencyKey = Guid.NewGuid().ToString("N") }, JsonOptions);
			rejection.EnsureSuccessStatusCode();
		}
		var otherTypeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var other = await ConcessionRequestTestHelper.CreateDraftAsync(Client, new CreateConcessionDraftDto
		{
			StudentId = studentId, StudentFeeStructureId = assignmentId, ConcessionTypeId = otherTypeId,
			ConcessionPolicyId = policyId, ConcessionPolicyVersionId = versionId,
		});
		other.Status.ShouldBe(ConcessionRequestStatus.Draft);
		using (var scope = Factory.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
			using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var dues = await services.GetRequiredService<IDueFeeRepository>().GetListAsync(x =>
				x.StudentId == studentId && x.FeeHeadId == headIds[0]);
			dues.Single().ConcessionAmount.ShouldBe(concessionBefore);
			(await services.GetRequiredService<IConcessionRequestRepository>().GetAsync(historicalId)).Status.ShouldBe(ConcessionRequestStatus.Approved);
		}
	}

	private async Task<System.Net.Http.HttpClient> CreateBenefitCheckerClientAsync()
	{
		var id = Guid.NewGuid();
		var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		// Resolve repositories after beginning the UOW so both identity and scope
		// are committed before real production Finance scope authorization executes.
		var name = $"benefit-checker-{id:N}";
		await services.GetRequiredService<IRepository<Volo.Abp.Identity.IdentityUser, Guid>>().InsertAsync(
			new Volo.Abp.Identity.IdentityUser(id, name, $"{name}@example.test", tenantId));
		await services.GetRequiredService<IRepository<ShikshaMitra.Security.UserScope.UserScopeAssignment, Guid>>().InsertAsync(
			new ShikshaMitra.Security.UserScope.UserScopeAssignment(Guid.NewGuid(), tenantId, id,
				description: "Annual benefit regression checker tenant scope"));
		await uow.CompleteAsync();
		return CreateClientAs(id);
	}
}

[CollectionDefinition(Name)]
public sealed class ConcessionUpdateApiTestCollection : ICollectionFixture<ConcessionUpdateApiTestFactory>
{
	public const string Name = "Concession draft update API";
}

public sealed class ConcessionUpdateApiTestFactory : ShikshaMitraApiTestFactory
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
			new Dictionary<string, string?>
			{
				[ShikshaMitraSeederProfile.ContextPropertyName] = ShikshaMitraSeederProfile.Minimal,
			}));
		// Synthetic users authenticate through TestAuthHandler. Hashing strength is
		// outside this Finance test; keep fixture setup cheap without changing the host.
		builder.ConfigureTestServices(services =>
		{
			services.PostConfigure<PasswordHasherOptions>(options => options.IterationCount = 1);
			services.Replace(ServiceDescriptor.Transient<IRepository<Student, Guid>, ConcessionStudentTestRepository>());
		});
	}
}

public class ConcessionStudentTestRepository : EfCoreRepository<ShikshaMitraDbContext, Student, Guid>
{
	public ConcessionStudentTestRepository(IDbContextProvider<ShikshaMitraDbContext> provider,
		IAbpLazyServiceProvider lazyServiceProvider, IServiceProvider serviceProvider) : base(provider)
	{
		LazyServiceProvider = lazyServiceProvider;
		ServiceProvider = serviceProvider;
	}

	// Finance needs scalar student fields here. Avoid SQLite's recursive academic
	// auto-include expansion; explicit includes, query filters and EF persistence remain real.
	public override async Task<IQueryable<Student>> GetQueryableAsync() =>
		(await base.GetQueryableAsync()).IgnoreAutoIncludes();
}
