using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using Volo.Abp.Identity;

namespace ShikshaMitra.Academic.Notifications;

public sealed record AcademicNotificationAccount(StudentPortalAccountLink Link, IdentityUser User);
public sealed record AcademicNotificationStudent(Student Student, StudentFamilyDetail? Family,
	bool GuardianReviewPending, IReadOnlyList<AcademicNotificationAccount> Accounts);
public sealed record AcademicNotificationStaff(Guid UserId, Guid? EmployeeId, bool IsActive,
	string? Email, string? Phone);
public sealed record AcademicNotificationRecipientData(IReadOnlyList<AcademicNotificationStudent> Students,
	IReadOnlyList<AcademicNotificationStaff> Staff);
public sealed record AcademicNotificationAccessContext(Guid TenantId, Guid UserId,
	UserScopeContext Scope, NotificationSourceSnapshot Source);

public interface IAcademicNotificationRecipientProjection
{
	Task<AcademicNotificationRecipientData> LoadAsync(Guid tenantId,
		IReadOnlyList<NotificationRecipientRefDto> sourceRecipients, CancellationToken cancellationToken);
}

public interface IAcademicNotificationAccessService
{
	Task<AcademicNotificationAccessContext> RequireSendAsync(AcademicNotificationAudienceInput input,
		CancellationToken cancellationToken);
}
