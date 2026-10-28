using System;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Portal;

public class StudentPortalEmailChangeJob(StudentPortalEmailChangeService changes, ICurrentTenant tenant)
	: AsyncBackgroundJob<StudentPortalEmailChangeJobArgs>, ITransientDependency
{
	internal const string FailureStageKey = "StudentPortalEmailVerificationStage";

	public override async Task ExecuteAsync(StudentPortalEmailChangeJobArgs args)
	{
		using var current = tenant.Change(args.TenantId);
		try { await changes.SendVerificationAsync(args); }
		catch (Exception exception)
		{
			// Background-job logs must never retain SMTP exceptions or rendered token links.
			var stage = exception.Data[FailureStageKey] as string;
			stage = stage is "tenant" or "account" or "actor" or "permissions" or "linked-children" or "source" or
				"request-state" or "topology" or "duplicate-email" or "claim" or "token" or "client-url" or "transport" or "persist"
				? stage : "transaction";
			throw new InvalidOperationException($"Portal email verification could not be sent (stage: {stage}; error: {exception.GetType().Name}). Review the saved request and email configuration.");
		}
	}
}
