using System;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Portal;

public class StudentPortalEmailChangeJob(StudentPortalEmailChangeService changes, ICurrentTenant tenant)
	: AsyncBackgroundJob<StudentPortalEmailChangeJobArgs>, ITransientDependency
{
	public override async Task ExecuteAsync(StudentPortalEmailChangeJobArgs args)
	{
		using var current = tenant.Change(args.TenantId);
		try { await changes.SendVerificationAsync(args); }
		catch (Exception)
		{
			// Background-job logs must never retain SMTP exceptions or rendered token links.
			throw new InvalidOperationException("Portal email verification could not be sent. Review the saved request and email configuration.");
		}
	}
}
