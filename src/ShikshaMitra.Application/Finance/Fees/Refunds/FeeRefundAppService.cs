using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Students;
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
	IDueFeeRepository dueFeeRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<Student, Guid> studentRepository,
	IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
	FeeRefundManager feeRefundManager,
	FinanceScopeAccessService financeScopeAccessService,
	ICurrentUser currentUser,
	IUnitOfWorkManager unitOfWorkManager,
	IDistributedEventBus eventBus
) : ApplicationService, IFeeRefundAppService;
