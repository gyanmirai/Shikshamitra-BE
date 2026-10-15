using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

[Authorize]
[RemoteService(false)]
public partial class FeeRefundAppService(
	IFeeRefundRepository feeRefundRepository,
	IRepository<FeeRefundAccountingEntry, Guid> refundAccountingRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<Student, Guid> studentRepository,
	IRepository<ShikshaMitra.Domain.Finance.Accounts.Settings.BankAccount, Guid> bankAccountRepository,
	IRepository<FeePaymentReceipt, Guid> feePaymentReceiptRepository,
	IRepository<FeeCollectionDueFeeAllocation, Guid> allocationRepository,
	IRepository<FeeRecordMetadata, Guid> collectionRepository,
	IRepository<StudentFeeStructure, Guid> assignmentRepository,
    IRepository<FinanceFeeAccount, Guid> accountRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<FinancePolicyVersion, Guid> policyVersionRepository,
	ShikshaMitra.Domain.Finance.Accounts.Settings.FinanceRoutingManager routingManager,
    ShikshaMitra.Finance.Accounts.AutomaticTransactionManager automaticTransactions,
    FeeRefundManager feeRefundManager,
	RefundPolicyCalculationService refundPolicyCalculationService,
	FinanceAuditWriter financeAuditWriter,
	FinanceScopeAccessService financeScopeAccessService,
	ICurrentUser currentUser,
	IUserScopeService userScopeService,
	IUnitOfWorkManager unitOfWorkManager,
	IDistributedEventBus eventBus
) : ApplicationService, IFeeRefundAppService;
