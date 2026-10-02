using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common.Excel;
using ShikshaMitra.Common.Excel.Elements;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentRecordReportRunner(IRepository<StudentRecordReportJob, Guid> jobs, StudentRecordReportGenerator generator,
	StudentRecordReportAuthority authority, IdentityUserManager users, ICurrentPrincipalAccessor principals,
	IPermissionChecker permissions, IExcelService excel, IBlobContainer blobs) : ApplicationService, ITransientDependency
{
	public async Task RunAsync(Guid id)
	{
		var generation = GuidGenerator.Create();
		Guid actor; string type; string request; string expectedAuthority;
		try
		{
			using var lease = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
			var job = await jobs.GetAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id);
			var now = new DateTimeOffset(Clock.Now.ToUniversalTime());
			if (job.ExpiresAt <= now || (job.Status != "Queued" && !(job.Status == "Generating" && job.LeaseUntil <= now))) return;
			job.Start(generation, now); await jobs.UpdateAsync(job);
			actor = job.RequestedBy; type = job.ReportType; request = job.RequestJson; expectedAuthority = job.AuthorityHash;
			await lease.CompleteAsync();
		}
		catch (AbpDbConcurrencyException) { return; }
		try
		{
			var user = await users.FindByIdAsync(actor.ToString());
			if (user == null || user.TenantId != CurrentTenant.Id || !user.IsActive) throw new AbpAuthorizationException();
			var claims = new List<Claim> { new(AbpClaimTypes.UserId, actor.ToString()), new(AbpClaimTypes.UserName, user.UserName) };
			if (CurrentTenant.Id.HasValue) claims.Add(new(AbpClaimTypes.TenantId, CurrentTenant.Id.Value.ToString()));
			foreach (var role in await users.GetRolesAsync(user)) claims.Add(new(AbpClaimTypes.Role, role));
			using var identity = principals.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "StudentRecordsReport")));
			if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Reports.Add)) throw new AbpAuthorizationException();
			var input = JsonSerializer.Deserialize<StudentRecordReportInput>(request, StudentRecordPayloadService.JsonOptions)!;
			var rows = new List<Dictionary<string, string?>>(); var columns = new List<string>();
			using (var read = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.RepeatableRead }, requiresNew: true))
			{
				if (expectedAuthority != await authority.CaptureAsync(type)) throw new BusinessException("Students:Records:ReportAuthorityChanged");
				input.SkipCount = 0; input.MaxResultCount = 100;
				while (true)
				{
					var page = await generator.GeneratePageAsync(input); columns = page.Columns;
					if (page.TotalCount > 100000) throw new BusinessException("Students:Records:ReportTooLarge");
					rows.AddRange(page.Rows);
					if (rows.Count >= page.TotalCount) break;
					if (page.Rows.Count == 0) throw new BusinessException("Students:Records:ReportSourceChanged");
					input.SkipCount += page.Rows.Count;
				}
				await read.CompleteAsync();
			}
			var sheetRows = new List<ExcelRow> { new() { Cells = columns.Select(x => new ExcelCell { Value = x }).ToList() } };
			sheetRows.AddRange(rows.Select(row => new ExcelRow { Cells = columns.Select(x => new ExcelCell { Value = row.GetValueOrDefault(x) ?? string.Empty }).ToList() }));
			var bytes = excel.GenerateExcelAsBytes(new() { Sheets = [new() { SheetName = type, Rows = sheetRows }] });
			using var completion = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
			var current = await jobs.GetAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id);
			if (current.Status != "Generating" || current.GenerationId != generation) return;
			if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Reports.Add)) throw new AbpAuthorizationException();
			if (expectedAuthority != await authority.CaptureAsync(type)) throw new BusinessException("Students:Records:ReportAuthorityChanged");
			if (current.ExpiresAt <= new DateTimeOffset(Clock.Now.ToUniversalTime())) throw new BusinessException("Students:Records:ReportNotReady");
			var blob = $"student-record-reports/{CurrentTenant.Id}/{actor}/{id}-{generation}.xlsx";
			await blobs.SaveAsync(blob, bytes, overrideExisting: false);
			current.Complete(generation, blob, rows.Count); await jobs.UpdateAsync(current);
			await completion.CompleteAsync();
		}
		catch (Exception error)
		{
			Logger.LogError(error, "Student Records report {JobId} generation failed.", id);
			using var failed = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
			var job = await jobs.GetAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id);
			if (job.Status == "Generating" && job.GenerationId == generation)
			{
				job.Fail(generation, error is BusinessException business ? business.Code ?? "Students:Records:ReportFailed" : "Students:Records:ReportFailed");
				await jobs.UpdateAsync(job);
			}
			await failed.CompleteAsync();
		}
	}
}
