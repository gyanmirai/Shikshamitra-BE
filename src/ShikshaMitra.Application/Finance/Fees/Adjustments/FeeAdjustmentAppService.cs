using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Identity;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Repositories;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

[Authorize]
[RemoteService(false)]
public partial class FeeAdjustmentAppService(
    IFeeAdjustmentRepository feeAdjustmentRepository,
    FeeAdjustmentManager feeAdjustmentManager,
    IDueFeeRepository dueFeeRepository,
    IRepository<Student, Guid> studentRepository,
    IRepository<FeeHead, Guid> feeHeadRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    IRepository<IdentityUser, Guid> identityUserRepository,
    FinanceAuditWriter financeAuditWriter,
    FinanceScopeAccessService financeScopeAccessService,
    ICurrentUser currentUser,
    IUnitOfWorkManager unitOfWorkManager,
    IDistributedEventBus eventBus) : ApplicationService, IFeeAdjustmentAppService
{
    private static readonly string[] MonthNames =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    ];
}
