using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Finance.Mobile;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using Xunit;
using Permissions = ShikshaMitra.Finance.Mobile.MobileFinancePermissions;

namespace ShikshaMitra.Academic.Mobile;

// Reuses the persisted portal/enrollment fixture, replacing the suite's AlwaysAllow checker with real ABP providers.
public partial class MobileAcademicAppService_Tests
{
	private MobileFinanceAccessService RealFinanceAccess()
	{
		var checker=ActivatorUtilities.CreateInstance<ShikshaMitraPermissionChecker>(ServiceProvider);
		var service=ActivatorUtilities.CreateInstance<MobileFinanceAccessService>(ServiceProvider,checker);
		service.LazyServiceProvider=new AbpLazyServiceProvider(ServiceProvider);return service;
	}
	private MobileFinanceReadService RealFinanceReads()
	{
		var service=ActivatorUtilities.CreateInstance<MobileFinanceReadService>(ServiceProvider,RealFinanceAccess());
		service.LazyServiceProvider=new AbpLazyServiceProvider(ServiceProvider);return service;
	}
	[Fact]
	public Task Finance_existing_parent_role_grant_honors_explicit_user_denial()=>WithUnitOfWorkAsync(async()=>
	{
		var tenantId=Guid.NewGuid();using var tenant=GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f=await Seed(tenantId,true);
		var role=await Insert(new Volo.Abp.Identity.IdentityRole(Guid.NewGuid(),"Parent",tenantId));
		f.User.AddRole(role.Id);
		await GetRequiredService<IRepository<Volo.Abp.Identity.IdentityUser,Guid>>().UpdateAsync(f.User,true);
		using var identity=FinanceIdentity(f.User.Id,"Parent");
		using (var grants=GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>().Begin(requiresNew:true,isTransactional:true))
		{
			await GetRequiredService<IPermissionManager>().SetAsync(Permissions.View,"R","Parent",true);
			await grants.CompleteAsync();
		}
		var access=RealFinanceAccess();
		(await access.RequireAsync(f.Student.Id,f.Year.Id)).IsGuardian.ShouldBeTrue();
		await GetRequiredService<IUserPermissionDenyStore>().SetDeniedAsync(f.User.Id,Permissions.View,true);
		await Should.ThrowAsync<AbpAuthorizationException>(()=>access.RequireAsync(f.Student.Id,f.Year.Id));
	});
	[Fact]
	public Task Finance_context_reports_existing_read_grants_without_enabling_requests()=>WithUnitOfWorkAsync(async()=>
	{
		var tenantId=Guid.NewGuid();using var tenant=GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f=await Seed(tenantId,true);using var identity=FinanceIdentity(f.User.Id);
		await GrantFinancePermissionAsync(f.User.Id,Permissions.View);
		await GrantFinancePermissionAsync(f.User.Id,Permissions.ViewConcessions);
		var reads=RealFinanceReads();
		var capabilities=(await reads.ContextAsync(f.Student.Id)).Capabilities;
		capabilities.CanView.ShouldBeTrue();
		capabilities.CanDownloadStatement.ShouldBeTrue();
		capabilities.CanViewConcessions.ShouldBeTrue();
		capabilities.CanViewCharges.ShouldBeFalse();
		capabilities.CanViewReceipts.ShouldBeFalse();
		capabilities.CanRequestConcession.ShouldBeFalse();
		capabilities.CanRequestRefund.ShouldBeFalse();
		await Should.ThrowAsync<AbpAuthorizationException>(()=>reads.ChargesAsync(f.Student.Id,f.Year.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(()=>reads.ReceiptsAsync(f.Student.Id,f.Year.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(()=>reads.RequestsAsync(f.Student.Id,f.Year.Id,"refund"));
		(await reads.RequestsAsync(f.Student.Id,f.Year.Id,"concession")).Items.ShouldBeEmpty();
	});
	[Fact]
	public Task Finance_concession_add_does_not_authorize_submit_cancel_edit_or_upload()=>WithUnitOfWorkAsync(async()=>
	{
		var tenantId=Guid.NewGuid();using var tenant=GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f=await Seed(tenantId,true);using var identity=FinanceIdentity(f.User.Id);
		await GrantFinancePermissionAsync(f.User.Id,Permissions.RequestConcession);
		var service=FamilyService<ShikshaMitra.Finance.Fees.Concessions.ConcessionRequestAppService>();
		var id=Guid.NewGuid();
		await Should.ThrowAsync<AbpAuthorizationException>(()=>service.FamilyActionAsync(f.Student.Id,f.Year.Id,id,new(),true));
		await Should.ThrowAsync<AbpAuthorizationException>(()=>service.FamilyActionAsync(f.Student.Id,f.Year.Id,id,new(),false));
		await Should.ThrowAsync<AbpAuthorizationException>(()=>service.FamilySaveAsync(f.Student.Id,f.Year.Id,id,new()));
		await Should.ThrowAsync<AbpAuthorizationException>(()=>service.FamilyUploadAsync(f.Student.Id,f.Year.Id,id,id,new()));
	});
	[Fact]
	public Task Finance_existing_collection_view_allows_linked_guardian_but_not_refund_creation()=>WithUnitOfWorkAsync(async()=>
	{
		var tenantId=Guid.NewGuid();using var tenant=GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f=await Seed(tenantId,true);using var identity=FinanceIdentity(f.User.Id);
		var grants=GetRequiredService<IPermissionManager>();var access=RealFinanceAccess();
		await GrantFinancePermissionAsync(f.User.Id,"Finance.Fees.FeeCollections.View");
		(await access.RequireAsync(f.Student.Id,f.Year.Id)).IsGuardian.ShouldBeTrue();
		await Should.ThrowAsync<AbpAuthorizationException>(()=>access.RequireAsync(f.Student.Id,f.Year.Id,true,"Finance.Fees.Refund.Add"));
		await GrantFinancePermissionAsync(f.User.Id,"Finance.Fees.Refund.Add");
		(await access.RequireAsync(f.Student.Id,f.Year.Id,true,"Finance.Fees.Refund.Add")).StudentId.ShouldBe(f.Student.Id);
	});
	[Fact]
	public Task Finance_old_self_service_grant_alone_does_not_authorize_collection_read()=>WithUnitOfWorkAsync(async()=>
	{
		var tenantId=Guid.NewGuid();using var tenant=GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f=await Seed(tenantId,true);using var identity=FinanceIdentity(f.User.Id);
		await GrantFinancePermissionAsync(f.User.Id,"Finance.SelfService.View");
		await Should.ThrowAsync<AbpAuthorizationException>(()=>RealFinanceAccess().RequireAsync(f.Student.Id,f.Year.Id));
	});
	[Fact]
	public Task Finance_rejects_foreign_child_foreign_year_and_revoked_link_after_allowed_read()=>WithUnitOfWorkAsync(async()=>
	{
		var tenantId=Guid.NewGuid();using var tenant=GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f=await Seed(tenantId,true);using var identity=FinanceIdentity(f.User.Id);
		await GrantFinancePermissionAsync(f.User.Id,Permissions.View);
		var access=RealFinanceAccess();(await access.RequireAsync(f.Student.Id,f.Year.Id)).StudentId.ShouldBe(f.Student.Id);
		await Should.ThrowAsync<AbpAuthorizationException>(()=>access.RequireAsync(Guid.NewGuid(),f.Year.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(()=>access.RequireAsync(f.Student.Id,Guid.NewGuid()));
		await GetRequiredService<IRepository<StudentPortalAccountLink,Guid>>().DeleteAsync(f.Link,true);
		await Should.ThrowAsync<AbpAuthorizationException>(()=>access.RequireAsync(f.Student.Id,f.Year.Id));
	});
	[Fact]
	public Task Finance_self_with_guardian_link_and_request_grant_stays_read_only()=>WithUnitOfWorkAsync(async()=>
	{
		var tenantId=Guid.NewGuid();using var tenant=GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f=await Seed(tenantId,false);using var identity=FinanceIdentity(f.User.Id);
		await Insert(new StudentPortalAccountLink(Guid.NewGuid(),tenantId,f.Student.Id,f.User.Id,"guardian","father",Guid.NewGuid()));
		var grants=GetRequiredService<IPermissionManager>();
		await GrantFinancePermissionAsync(f.User.Id,Permissions.View);
		await GrantFinancePermissionAsync(f.User.Id,Permissions.RequestRefund);
		var access=RealFinanceAccess();(await access.RequireAsync(f.Student.Id,f.Year.Id)).IsGuardian.ShouldBeFalse();
		await Should.ThrowAsync<AbpAuthorizationException>(()=>access.RequireAsync(f.Student.Id,f.Year.Id,true,Permissions.RequestRefund));
	});
	[Fact]
	public Task Finance_revoked_enrollment_does_not_regain_legacy_year_access()=>WithUnitOfWorkAsync(async()=>
	{
		var tenantId=Guid.NewGuid();using var tenant=GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f=await Seed(tenantId,true);using var identity=FinanceIdentity(f.User.Id);
		await GrantFinancePermissionAsync(f.User.Id,Permissions.View);
		await GetRequiredService<IRepository<StudentEnrollment,Guid>>().DeleteAsync(f.Enrollment,true);
		await Should.ThrowAsync<AbpAuthorizationException>(()=>RealFinanceAccess().RequireAsync(f.Student.Id,f.Year.Id));
	});
	[Fact]
	public Task Finance_payment_lists_real_receipt_ids_and_downloads_only_owned_pdf()=>WithUnitOfWorkAsync(async()=>
	{
		var tenantId=Guid.NewGuid();using var tenant=GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f=await Seed(tenantId,true);using var identity=FinanceIdentity(f.User.Id);
		var grants=GetRequiredService<IPermissionManager>();
		await GrantFinancePermissionAsync(f.User.Id,Permissions.View);
		var transaction=await Insert(new FeeRecordMetadata(Guid.NewGuid(),tenantId,f.Student.Id,f.Year.Id,0.30,"META",FeeCollectionStatus.Paid));
		await Insert(PaymentMethodRecord.Create(f.Student.Id,transaction.Id,0.10m,DateTimeOffset.UtcNow,PaymentMethodEnum.Cash,tenantId:tenantId));
		await Insert(PaymentMethodRecord.Create(f.Student.Id,transaction.Id,0.20m,DateTimeOffset.UtcNow,PaymentMethodEnum.Cash,tenantId:tenantId));
		var receipt=await Insert(new FeePaymentReceipt(Guid.NewGuid()){TenantId=tenantId,StudentId=f.Student.Id,AcademicYearId=f.Year.Id,
			FeeCollectionTransactionId=transaction.Id,ReceiptNumber="FIN-FAMILY-1",ReceiptKind=FeeReceiptKind.FinalFee,ReceiptScope=FeeReceiptScope.Fee,TotalPaidAmount=0.30,ReceiptDate=DateOnly.FromDateTime(DateTime.UtcNow),BoardId=f.Student.AcademicBoardId!.Value,EducationMediumId=f.Student.AcademicMediumId});
		var other=await Insert(new FeePaymentReceipt(Guid.NewGuid()){TenantId=tenantId,StudentId=Guid.NewGuid(),AcademicYearId=f.Year.Id,
			FeeCollectionTransactionId=transaction.Id,ReceiptNumber="FIN-FAMILY-OTHER",ReceiptKind=FeeReceiptKind.FinalFee,ReceiptScope=FeeReceiptScope.Fee,TotalPaidAmount=1,ReceiptDate=receipt.ReceiptDate});
		var reads=RealFinanceReads();var item=(await reads.PaymentsAsync(f.Student.Id,f.Year.Id)).Items.Single();
		item.Amount.ShouldBe("0.30");item.Receipts.ShouldBeEmpty("Receipt details require the existing Receipts View grant.");
		await Should.ThrowAsync<AbpAuthorizationException>(()=>reads.ReceiptPdfAsync(f.Student.Id,f.Year.Id,receipt.Id));
		await GrantFinancePermissionAsync(f.User.Id,Permissions.DownloadReceipts);
		var permittedPayment=(await reads.PaymentsAsync(f.Student.Id,f.Year.Id)).Items.Single();
		permittedPayment.Receipts.Single().Id.ShouldBe(receipt.Id);
		permittedPayment.Receipts.Single().PaymentId.ShouldBe(transaction.Id);
		await Should.ThrowAsync<AbpAuthorizationException>(()=>reads.ReceiptPdfAsync(f.Student.Id,f.Year.Id,other.Id));
		var pdf=await reads.ReceiptPdfAsync(f.Student.Id,f.Year.Id,receipt.Id);
		Encoding.ASCII.GetString(pdf,0,5).ShouldBe("%PDF-");
		// Collections View also authorizes the linked child's statement; no separate download grant.
		await Should.ThrowAsync<AbpAuthorizationException>(() => reads.StatementPdfAsync(f.Student.Id, Guid.NewGuid()));
		var statement = await reads.StatementPdfAsync(f.Student.Id, f.Year.Id);
		Encoding.ASCII.GetString(statement, 0, 5).ShouldBe("%PDF-");
	});
}
