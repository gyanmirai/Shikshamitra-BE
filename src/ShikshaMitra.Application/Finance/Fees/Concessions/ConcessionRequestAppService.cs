using System;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Common.Files;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Students;
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
[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.View)]
public partial class ConcessionRequestAppService(
	IConcessionRequestRepository concessionRequestRepository,
	ConcessionRequestManager concessionRequestManager,
	IRepository<Student, Guid> studentRepository,
	IRepository<ConcessionType, Guid> concessionTypeRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<DueFee, Guid> dueFeeRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	FinanceScopeAccessService financeScopeAccessService,
	ICurrentUser currentUser,
	IGuidGenerator guidGenerator,
	IUnitOfWorkManager unitOfWorkManager,
	IFileStorageAppService fileStorageAppService,
	Volo.Abp.EventBus.Distributed.IDistributedEventBus eventBus)
	: ApplicationService, IConcessionRequestAppService
{
}
