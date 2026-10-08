using System;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Common.Files;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Students;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.Concessions;

[RemoteService(false)]
[Authorize]
public partial class ConcessionRequestAppService(
	IConcessionRequestRepository concessionRequestRepository,
	ConcessionRequestManager concessionRequestManager,
	IRepository<Student, Guid> studentRepository,
	IRepository<ConcessionType, Guid> concessionTypeRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<DueFee, Guid> dueFeeRepository,
	IRepository<FeeCollectionDueFeeAllocation, Guid> paymentAllocationRepository,
	IRepository<FeePaymentReceipt, Guid> paymentReceiptRepository,
	IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
	IFeeRefundRepository feeRefundRepository,
	FeeRefundManager feeRefundManager,
	IRepository<ConcessionPolicy, Guid> concessionPolicyRepository,
	IRepository<FinancePolicyVersion, Guid> financePolicyVersionRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<ConcessionCommandReceipt, Guid> concessionCommandReceiptRepository,
	IRepository<ConcessionDocumentUpload, Guid> concessionDocumentUploadRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	FinanceScopeAccessService financeScopeAccessService,
	IFileStorageAppService fileStorageAppService,
	ICurrentUser currentUser,
	IUserScopeService userScopeService,
	IGuidGenerator guidGenerator,
	FinanceAuditWriter financeAuditWriter,
	IUnitOfWorkManager unitOfWorkManager,
	Volo.Abp.EventBus.Distributed.IDistributedEventBus eventBus)
	: ApplicationService, IConcessionRequestAppService
{
}
