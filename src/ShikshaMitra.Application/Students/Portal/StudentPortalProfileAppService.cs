using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShikshaMitra.Academic.Timetable.Portal;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Portal;

[Authorize]
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IProfileAppService), typeof(ProfileAppService), typeof(StudentPortalProfileAppService))]
[RemoteService(false)]
public class StudentPortalProfileAppService(
	IdentityUserManager userManager,
	IOptions<IdentityOptions> identityOptions,
	StudentPortalAccessService portalAccess,
	PortalStudentContextReader contextReader)
	: ProfileAppService(userManager, identityOptions)
{
	public override async Task<ProfileDto> GetAsync()
	{
		var profile = await base.GetAsync();
		var contexts = new List<PortalTimetableContextDto>();
		var session = new PortalSessionDto();
		profile.ExtraProperties["studentContexts"] = contexts;
		profile.ExtraProperties["portalSession"] = session;
		if (!CurrentTenant.Id.HasValue) return profile;
		var timer = Stopwatch.StartNew();
		try
		{
			var students = await portalAccess.GetMyStudentsAsync();
			Logger.LogInformation("Portal profile association phase: {ElapsedMs} ms, {StudentCount} records", timer.ElapsedMilliseconds, students.Count);
			timer.Restart();
			session.Status = "ready";
			session.Students = students.Select(account => new PortalSessionStudentDto { Account = account }).ToList();
			try
			{
				// Optional context reads have their own read-only UOW. Never reuse a failed DB context.
				using var read = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
				var results = await contextReader.ReadAsync(students.Select(x => x.StudentId).Distinct().ToArray());
				foreach (var item in session.Students)
				{
					var result = results[item.Account.StudentId];
					item.Context = result.Context;
					item.ContextStatus = result.Error == null ? "loaded" : "invalid";
				}
				await read.CompleteAsync();
			}
			catch (Exception error) when (error is TimeoutException || error is DbException { IsTransient: true })
			{
				foreach (var item in session.Students) { item.Context = null; item.ContextStatus = "unavailable"; }
				Logger.LogWarning("Portal profile context phase failed: {FailureCategory}", "transient");
			}
			contexts.AddRange(session.Students.Where(x => x.Context != null).Select(x => x.Context!).DistinctBy(x => x.Student.Id));
			Logger.LogInformation("Portal profile context phase: {ElapsedMs} ms, {LoadedCount} loaded, {InvalidCount} invalid, {UnavailableCount} unavailable",
				timer.ElapsedMilliseconds, contexts.Count, session.Students.Count(x => x.ContextStatus == "invalid"), session.Students.Count(x => x.ContextStatus == "unavailable"));
		}
		catch (AbpAuthorizationException)
		{
			// Reading identity does not grant portal access to unconfirmed or revoked links.
			contexts.Clear();
			session.Status = "restricted";
			session.Students.Clear();
			Logger.LogInformation("Portal profile access restricted");
		}
		return profile;
	}
}
