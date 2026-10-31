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
public partial class FeeRefundAppService : ApplicationService, IFeeRefundAppService
{
	private readonly IFeeRefundRepository feeRefundRepository;
	private readonly IRepository<FeeRefundAccountingEntry, Guid> refundAccountingRepository;
	private readonly IDueFeeRepository dueFeeRepository;
	private readonly IRepository<FeeHead, Guid> feeHeadRepository;
	private readonly IRepository<Student, Guid> studentRepository;
	private readonly IRepository<FeePaymentReceipt, Guid> feePaymentReceiptRepository;
	private readonly IRepository<FeeCollectionDueFeeAllocation, Guid> allocationRepository;
	private readonly IRepository<FeeRecordMetadata, Guid> collectionRepository;
	private readonly PaidChargeRefundService paidChargeRefundService;
	private readonly PaidLateFeeRefundService paidLateFeeRefundService;
	private readonly IRepository<StudentFeeStructure, Guid> assignmentRepository;
	private readonly IRepository<FinanceFeeAccount, Guid> accountRepository;
	private readonly IRepository<AcademicYear, Guid> academicYearRepository;
	private readonly IRepository<FinancePolicyVersion, Guid> policyVersionRepository;
	private readonly ShikshaMitra.Domain.Finance.Accounts.Settings.FinanceRoutingManager routingManager;
	private readonly ShikshaMitra.Finance.Accounts.AutomaticTransactionManager automaticTransactions;
	private readonly FeeRefundManager feeRefundManager;
	private readonly RefundPolicyCalculationService refundPolicyCalculationService;
	private readonly FinanceAuditWriter financeAuditWriter;
	private readonly FinanceScopeAccessService financeScopeAccessService;
	private readonly ICurrentUser currentUser;
	private readonly IUserScopeService userScopeService;
	private readonly IUnitOfWorkManager unitOfWorkManager;
	private readonly IDistributedEventBus eventBus;

	public FeeRefundAppService(
		IFeeRefundRepository feeRefundRepository,
		IRepository<FeeRefundAccountingEntry, Guid> refundAccountingRepository,
		IDueFeeRepository dueFeeRepository,
		IRepository<FeeHead, Guid> feeHeadRepository,
		IRepository<Student, Guid> studentRepository,
		IRepository<ShikshaMitra.Domain.Finance.Accounts.Settings.BankAccount, Guid> bankAccountRepository,
		IRepository<FeePaymentReceipt, Guid> feePaymentReceiptRepository,
		IRepository<FeeCollectionDueFeeAllocation, Guid> allocationRepository,
		IRepository<FeeRecordMetadata, Guid> collectionRepository,
		PaidChargeRefundService paidChargeRefundService,
		PaidLateFeeRefundService paidLateFeeRefundService,
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
	)
	{
		this.feeRefundRepository = feeRefundRepository;
		this.refundAccountingRepository = refundAccountingRepository;
		this.dueFeeRepository = dueFeeRepository;
		this.feeHeadRepository = feeHeadRepository;
		this.studentRepository = studentRepository;
		_ = bankAccountRepository;
		this.feePaymentReceiptRepository = feePaymentReceiptRepository;
		this.allocationRepository = allocationRepository;
		this.collectionRepository = collectionRepository;
		this.paidChargeRefundService = paidChargeRefundService;
		this.paidLateFeeRefundService = paidLateFeeRefundService;
		this.assignmentRepository = assignmentRepository;
		this.accountRepository = accountRepository;
		this.academicYearRepository = academicYearRepository;
		this.policyVersionRepository = policyVersionRepository;
		this.routingManager = routingManager;
		this.automaticTransactions = automaticTransactions;
		this.feeRefundManager = feeRefundManager;
		this.refundPolicyCalculationService = refundPolicyCalculationService;
		this.financeAuditWriter = financeAuditWriter;
		this.financeScopeAccessService = financeScopeAccessService;
		this.currentUser = currentUser;
		this.userScopeService = userScopeService;
		this.unitOfWorkManager = unitOfWorkManager;
		this.eventBus = eventBus;
	}
}
