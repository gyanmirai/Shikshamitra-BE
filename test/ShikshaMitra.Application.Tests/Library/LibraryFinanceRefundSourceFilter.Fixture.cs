using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private Task<Guid> ReadRefundFilterLinkThroughLibraryAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		var services = new FinanceServices(ServiceProvider, owner, useStoredSource: true);
		var checker = services.GetRequiredService<IPermissionChecker>();
		(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View)).ShouldBeTrue();
		(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)).ShouldBeTrue();
		(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View)).ShouldBeFalse();
		(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add)).ShouldBeFalse();
		var service = ActivatorUtilities.CreateInstance<LibraryChargeAppService>(services);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		var detail = await service.GetAsync(owner.AssessmentId);
		detail.Id.ShouldBe(owner.AssessmentId);
		detail.FinanceSourceLinkId.ShouldNotBeNull();
		return detail.FinanceSourceLinkId!.Value;
	});

	private async Task<TypedRefundFixture> SeedRefundSourceForSamePayerAsync(TypedRefundFixture first)
	{
		var firstOwner = first.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(firstOwner.TenantId);
		using var actor = SignIn(firstOwner);
		var owner = await WithUnitOfWorkAsync(async () =>
		{
			var assessments = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var template = await assessments.GetAsync(firstOwner.AssessmentId);
			var sourceId = Guid.NewGuid();
			var assessment = LibraryCharge.CreateConfirmed(sourceId, firstOwner.PersonId,
				firstOwner.PayerKind == FinanceExternalPayerKind.Student ? 0 : 1, template.ChargeCategoryId,
				template.BookIssueId, 100.75m, firstOwner.AssessmentDate, firstOwner.DueDate, firstOwner.YearId,
				LibraryAcademicYearProvenance.ReviewedLegacy,
				JsonSerializer.Serialize(new { SourceRegister = "Second separately reviewed assessment", AssessmentId = sourceId,
					AcademicYearId = firstOwner.YearId, ReviewedBy = firstOwner.ActorId,
					Reason = "Reviewed the separate imported assessment against its school register." }),
				JsonSerializer.Serialize(new { AssessmentId = sourceId, firstOwner.AssessmentDate, firstOwner.DueDate,
					ReviewedBy = firstOwner.ActorId, Reason = "Reviewed this assessment's original school dates." }),
				"A separate reviewed assessment for the same actual school payer.", firstOwner.TenantId);
			await assessments.InsertAsync(assessment, true);
			return firstOwner with { AssessmentId = assessment.Id, Version = assessment.GetAssessmentVersion() };
		});
		var post = Post(owner);
		var posted = await WithUnitOfWorkAsync(() => Service(owner, useStoredSource: true).PostAsync(post));
		var reviewer = owner with { ActorId = first.Settlement.Reviewer.ActorId };
		var context = new TypedSettlement(owner, reviewer, post, posted, first.Settlement.CollectionPolicyVersionId);
		var form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(post.Source));
		await WithUnitOfWorkAsync(() => TypedService(owner).BindPolicyAsync(new()
		{
			Source = post.Source, Revision = form.Revision, CollectionPolicyVersionId = context.CollectionPolicyVersionId,
			RefundPolicyVersionId = first.PolicyVersionId,
			Reason = "Independently confirm the same compatible pinned school policies for the second source.",
			IdempotencyKey = Guid.NewGuid().ToString("N")
		}));
		using (SignIn(reviewer))
		{
			form = await WithUnitOfWorkAsync(() => TypedService(reviewer).GetCollectFormAsync(post.Source));
			await WithUnitOfWorkAsync(() => TypedService(reviewer).ApproveAsync(new()
			{
				Source = post.Source, Revision = form.Revision, Reason = "Independently reviewed the second source assessment.",
				IdempotencyKey = Guid.NewGuid().ToString("N")
			}));
		}
		form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(post.Source));
		var collection = await WithUnitOfWorkAsync(() => TypedService(owner).CollectAsync(CashCollectionInput(context, form.Revision)));
		var allocation = await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>()
			.GetListAsync(row => row.FeeRecordMetadataId == collection.CollectionId)).Single());
		return new(context, collection, allocation.Id, first.PolicyVersionId);
	}

	private async Task<FeeRefundRequestDto> SubmitRefundSourceFilterRequestAsync(TypedRefundFixture source)
	{
		var owner = source.Settlement.Owner;
		var form = await WithUnitOfWorkAsync(() => TypedRefundService(owner).GetFormAsync(source.Settlement.Post.Source));
		var request = await WithUnitOfWorkAsync(() => TypedRefundService(owner).CreateAsync(RefundRequest(source, form.Revision, 15.25m)));
		request.Status.ShouldBe(FeeRefundStatus.Submitted);
		request.TotalAmount.ShouldBe(15.25m);
		request.CalculatedAmount.ShouldBe(15m);
		return request;
	}

	private Task<Guid> ReadRefundSourceFilterLinkIdAsync(TypedSettlement settlement) => WithUnitOfWorkAsync(async () =>
	{
		var link = (await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
			.GetListAsync(row => row.TenantId == settlement.Owner.TenantId &&
				row.SourceKind == FinanceExternalSourceKind.LibraryAssessment && row.SourceId == settlement.Owner.AssessmentId)).Single();
		link.FinanceChargeId.ShouldBe(settlement.Posted.FinanceChargeId);
		link.SourceVersion.ShouldBe(settlement.Post.Source.SourceVersion);
		link.AcademicYearId.ShouldBe(settlement.Owner.YearId);
		return link.Id;
	});

	private Task AssertRefundSourceFilterEvidenceAsync(TypedRefundFixture source, FeeRefundRequestDto submitted) => WithUnitOfWorkAsync(async () =>
	{
		var owner = source.Settlement.Owner;
		var link = (await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
			.GetListAsync(row => row.TenantId == owner.TenantId && row.SourceId == owner.AssessmentId)).Single();
		var request = await GetRequiredService<IRepository<FeeRefundRequest, Guid>>().GetAsync(submitted.Id);
		var charge = await GetRequiredService<IRepository<Charge, Guid>>().GetAsync(link.FinanceChargeId);
		request.FinanceExternalSourceLinkId.ShouldBe(link.Id);
		request.FinanceExternalSourceLinkId.ShouldBe(submitted.FinanceExternalSourceLinkId);
		request.TenantId.ShouldBe(owner.TenantId);
		request.AcademicYearId.ShouldBe(owner.YearId);
		request.StudentId.ShouldBe(owner.PayerKind == FinanceExternalPayerKind.Student ? owner.PersonId : null);
		request.EmployeeId.ShouldBe(owner.PayerKind == FinanceExternalPayerKind.Employee ? owner.PersonId : null);
		request.StudentFeeStructureId.ShouldBeNull();
		request.FinanceFeeAccountId.ShouldBeNull();
		request.OriginalReceiptId.ShouldBe(source.Collection.ReceiptId);
		request.Status.ShouldBe(FeeRefundStatus.Submitted);
		request.TotalAmount.ShouldBe(15.25m);
		request.CalculatedAmount.ShouldBe(15m);
		request.ProcessedAmount.ShouldBe(0m);
		request.ReversedAmount.ShouldBe(0m);
		link.SourceVersion.ShouldBe(source.Settlement.Post.Source.SourceVersion);
		link.FinanceChargeId.ShouldBe(source.Settlement.Posted.FinanceChargeId);
		link.AcademicYearId.ShouldBe(owner.YearId);
		charge.StudentId.ShouldBe(owner.PayerKind == FinanceExternalPayerKind.Student ? owner.PersonId : null);
		charge.EmployeeId.ShouldBe(owner.PayerKind == FinanceExternalPayerKind.Employee ? owner.PersonId : null);
		charge.AcademicYearId.ShouldBe(owner.YearId);
		charge.Status.ShouldBe(ChargeStatus.Approved);
	});

	private Task<Guid> SeedRefundSourceFilterLegacyDraftAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		owner.PayerKind.ShouldBe(FinanceExternalPayerKind.Student);
		var id = Guid.NewGuid();
		var draft = new FeeRefundRequest(id, owner.TenantId, owner.PersonId, owner.YearId,
			$"DRAFT-{id:N}", refundMethod: null, refundPaymentDetails: null,
			reason: "A saved unsubmitted draft for the same actual school Student; no payment or refund claimed.", createAsDraft: true);
		await GetRequiredService<IRepository<FeeRefundRequest, Guid>>().InsertAsync(draft, true);
		return draft.Id;
	});

	private Task AssertRefundSourceFilterLegacyDraftUnchangedAsync(Fixture owner, Guid draftId) => WithUnitOfWorkAsync(async () =>
	{
		var draft = await GetRequiredService<IRepository<FeeRefundRequest, Guid>>().GetAsync(draftId);
		draft.TenantId.ShouldBe(owner.TenantId);
		draft.StudentId.ShouldBe(owner.PersonId);
		draft.EmployeeId.ShouldBeNull();
		draft.AcademicYearId.ShouldBe(owner.YearId);
		draft.FinanceExternalSourceLinkId.ShouldBeNull();
		draft.RefundPolicyVersionId.ShouldBeNull();
		draft.StudentFeeStructureId.ShouldBeNull();
		draft.FinanceFeeAccountId.ShouldBeNull();
		draft.OriginalReceiptId.ShouldBeNull();
		draft.Status.ShouldBe(FeeRefundStatus.Draft);
		draft.WorkflowVersion.ShouldBe(0);
		draft.TotalAmount.ShouldBe(0m);
		draft.CalculatedAmount.ShouldBe(0m);
		draft.ApprovedAmount.ShouldBe(0m);
		draft.ProcessedAmount.ShouldBe(0m);
		draft.ReversedAmount.ShouldBe(0m);
		draft.Lines.ShouldBeEmpty();
		draft.Reservations.ShouldBeEmpty();
		draft.Settlements.ShouldBeEmpty();
	});

	private async Task RestrictRefundSourceFilterReadActionsAsync(Fixture owner)
	{
		var unrelated = new[]
		{
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApplyWaiver,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApproveWaiver,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.RejectWaiver,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ReverseWaiver,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reject,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Process,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reverse,
			ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View,
			ShikshaMitraSubmodulePermissions.Finance.Auditing.View,
			ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View,
			ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.View
		};
		await WithUnitOfWorkAsync(async () =>
		{
			var permissions = GetRequiredService<IPermissionManager>();
			foreach (var action in unrelated)
				await permissions.SetAsync(action, "R", $"external-finance-{owner.ActorId:N}", false);
			await permissions.SetAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View,
				"R", $"external-finance-{owner.ActorId:N}", true);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var checker = new FinanceServices(ServiceProvider, owner, useStoredSource: true).GetRequiredService<IPermissionChecker>();
			(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)).ShouldBeTrue();
			(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View)).ShouldBeTrue();
			foreach (var action in unrelated) (await checker.IsGrantedAsync(action)).ShouldBeFalse();
		});
	}

	private async Task SetRefundSourceFilterViewAsync(Fixture owner, bool granted)
	{
		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View, "R", $"external-finance-{owner.ActorId:N}", granted));
		(await WithUnitOfWorkAsync(() => new FinanceServices(ServiceProvider, owner, useStoredSource: true)
			.GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)))
			.ShouldBe(granted);
	}

	private FeeRefundAppService RefundSourceFilterFailureService(Fixture owner, Exception fault)
	{
		var services = new DownstreamRefundServices(new RefundSourceFilterFailureServices(
			new FinanceServices(ServiceProvider, owner, useStoredSource: true), fault));
		var service = ActivatorUtilities.CreateInstance<FeeRefundAppService>(services);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		return service;
	}

	// Fault injection replaces only the canonical query port, after the real DB
	// action/identity checks. Successful source selection always uses actual SQL.
	private sealed class RefundSourceFilterFailureServices(IServiceProvider inner, Exception fault) : IServiceProvider
	{
		public object? GetService(Type type) => type == typeof(IFinanceExternalSourceScope)
			? new FailingRefundSourceScope(fault) : inner.GetService(type);
	}

	private sealed class FailingRefundSourceScope(Exception fault) : IFinanceExternalSourceScope
	{
		public Task<IQueryable<FinanceExternalReadableSource>> GetReadableSourcesAsync(FinanceExternalSourceKind kind,
			CancellationToken cancellationToken = default) => Task.FromException<IQueryable<FinanceExternalReadableSource>>(fault);
	}
}
