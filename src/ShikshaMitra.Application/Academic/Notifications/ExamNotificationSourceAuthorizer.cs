using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class ExamNotificationSourceAuthorizer(IExamRepository exams, ExamAppService schedule,
	IPermissionChecker permissions, ICurrentTenant tenant) : IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	public IReadOnlyCollection<string> EventKeys => ExamNotificationSource.SupportedEvents;

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (!EventKeys.Contains(eventKey) || sourceId == Guid.Empty || tenant.Id is not Guid tenantId ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.View))
			throw new AbpAuthorizationException();
		var exam = await exams.FindAsync(sourceId, includeDetails: true, cancellationToken: token);
		if (exam == null || exam.TenantId != tenantId || exam.IsDeleted) throw new AbpAuthorizationException();
		try { await schedule.EnsureCanAccessExamAsync(exam); }
		catch (BusinessException) { throw new AbpAuthorizationException(); }
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) => EnsureCanViewAsync(eventKey, sourceId, token);
}
