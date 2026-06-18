using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.Charges;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View)]
public partial class ChargeAppService(
	IChargeRequestRepository chargeRequestRepository,
	IRepository<Charge, Guid> chargeRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<IdentityUser, Guid> userRepository,
	IRepository<ChargeCategory, Guid> chargeCategoryRepository,
	IRepository<Student, Guid> studentRepository,
	FinanceScopeAccessService financeScopeAccessService,
	ICurrentUser currentUser,
	ChargeRequestManager chargeRequestManager,
	FinanceAuditWriter financeAuditWriter,
	IDistributedEventBus eventBus,
	ILogger<ChargeAppService> logger)
	: ApplicationService, IChargeAppService
{
	private const int MaxPageSize = 100;
}
