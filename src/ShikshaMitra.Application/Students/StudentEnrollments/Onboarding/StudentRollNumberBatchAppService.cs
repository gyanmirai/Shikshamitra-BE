using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

[Authorize]
[RemoteService(false)]
public partial class StudentRollNumberBatchAppService(IRepository<StudentRollNumberBatch, Guid> batches,
	StudentRollBatchAccessService access, StudentRollRosterReader roster, StudentRollBatchApplication application,
	StudentRecordsAccessService records, IdentityUserManager users)
	: ApplicationService, IStudentRollNumberBatchAppService
{
	public async Task<StudentRollBatchContextDto> GetContextAsync(StudentRollScopeInput input)
	{
		await access.EnsureScopeAsync(input);
		var missing = await BuildAsync(new() { AcademicYearId = input.AcademicYearId, StandardId = input.StandardId, DivisionId = input.DivisionId });
		var full = await BuildAsync(new() { AcademicYearId = input.AcademicYearId, StandardId = input.StandardId, DivisionId = input.DivisionId, Operation = "renumber" });
		var result = new StudentRollBatchContextDto {
			ScopeLabel = missing.ExtraProperties["ScopeLabel"]?.ToString() ?? "",
			UnassignedCount = missing.Rows().Count,
			CanAssignMissing = missing.Rows().Count > 0 && await CanWriteAsync(missing),
			CanRenumber = full.Rows().Count > 0 && await CanWriteAsync(full),
			BlockedReason = missing.Rows().Count == 0 ? "No unassigned students in this division." : null };
		if (!result.CanAssignMissing && result.BlockedReason == null) result.BlockedReason = "You do not have access to assign every unassigned student in this division.";
		var recent = await AsyncExecuter.ToListAsync((await batches.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == input.AcademicYearId && x.DivisionId == input.DivisionId && x.Status != "Preview")
			.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id).Take(20));
		var summaries = new List<StudentRollNumberBatchDto>();
		foreach (var item in recent)
		{
			try { await access.EnsureRowsAsync(item, false); summaries.Add(await ProjectAsync(item, 0, 0)); }
			catch (AbpAuthorizationException) { }
			catch (BusinessException) { }
		}
		result.Batches = summaries; return result;
	}
	public async Task<StudentRollNumberBatchDto> PreviewAsync(PreviewStudentRollBatchInput input)
	{
		if (!(await new PreviewStudentRollBatchInputValidator().ValidateAsync(input)).IsValid) throw new BusinessException(StudentRollNumberErrorCodes.InvalidBatch);
		await access.EnsureScopeAsync(input);
		var item = await BuildAsync(input);
		await access.EnsureRowsAsync(item, true);
		await batches.InsertAsync(item, true);
		return await ProjectAsync(item, 0, 25);
	}
	public async Task<StudentRollNumberBatchDto> GetAsync(Guid id, PagedResultRequestDto input)
	{
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100) throw new BusinessException(StudentRollNumberErrorCodes.InvalidBatch);
		var item = await GetScopedAsync(id); return await ProjectAsync(item, input.SkipCount, input.MaxResultCount);
	}
	private async Task<StudentRollNumberBatch> BuildAsync(PreviewStudentRollBatchInput input)
	{
		var data = await roster.ReadAsync(input);
		var result = new StudentRollNumberBatch(GuidGenerator.Create(), CurrentTenant.Id!.Value, input.AcademicYearId, input.StandardId, input.DivisionId,
			CurrentUser.Id!.Value, input.Operation, data.Hash, data.RuleJson, JsonSerializer.Serialize(data.Rows), data.PolicyJson, Now());
		result.ExtraProperties["ScopeLabel"] = data.ScopeLabel;
		if (data.PolicyVersionId.HasValue) result.ExtraProperties["PolicyVersionId"] = data.PolicyVersionId.Value.ToString("D");
		return result;
	}
	private async Task<StudentRollNumberBatch> GetScopedAsync(Guid id)
	{
		var item = await batches.FindAsync(x => x.TenantId == CurrentTenant.Id && x.Id == id)
			?? throw new EntityNotFoundException(typeof(StudentRollNumberBatch), id);
		await access.EnsureRowsAsync(item, false); return item;
	}
	private async Task<bool> CanWriteAsync(StudentRollNumberBatch item)
	{
		try { await access.EnsureRowsAsync(item, true); return true; }
		catch (AbpAuthorizationException) { return false; }
		catch (BusinessException) { return false; }
	}
	private async Task<bool> CanDecideAsync(StudentRollNumberBatch item)
	{
		if (item.Status != "PendingApproval" || item.RequestedBy == CurrentUser.Id || item.Decisions().Any(x => x.ActorId == CurrentUser.Id) ||
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve)) return false;
		var stages = JsonSerializer.Deserialize<List<StudentRecordsApprovalStage>>(item.ApprovalStagesJson)!;
		var stage = item.Decisions().Count;
		return stage < stages.Count && (await records.GetDatabaseRoleNamesAsync()).Intersect(stages[stage].RoleNames, StringComparer.OrdinalIgnoreCase).Any();
	}
	private async Task<StudentRollNumberBatchDto> ProjectAsync(StudentRollNumberBatch item, int skip, int take)
	{
		var rows = item.Rows(); var rule = StudentOnboardingRollRuleReader.Parse(item.RuleJson)!.Value;
		var ordering = new StudentRollOrdering(rule);
		var write = item.Status is "Preview" or "Approved" && await CanWriteAsync(item);
		return new() { Id = item.Id, CreationTime = item.CreationTime, AcademicYearId = item.AcademicYearId, StandardId = item.StandardId, DivisionId = item.DivisionId,
			ScopeLabel = item.ExtraProperties.TryGetValue("ScopeLabel", out var label) ? label?.ToString() ?? "" : "",
			ConcurrencyStamp = item.ConcurrencyStamp, Operation = item.Operation, Status = item.Status, Reason = item.Reason, IdempotencyKey = item.SubmissionKey,
			Grouping = ordering.Grouping, SortBy = ordering.SortBy,
			NameFormat = ordering.NameFormat, SortDirection = ordering.SortDirection,
			ReasonRequired = item.Operation == "renumber" || rows.Any(x => x.EnrollmentId.HasValue) && StudentRecordsPolicy.FromJson(item.PolicyJson).Changes["Placement"].RequireReason,
			TotalCount = rows.Count, BlockedCount = rows.Count(x => x.Issue != null), AppliedAt = item.AppliedAt,
			CanSubmit = item.Status == "Preview" && item.RequestedBy == CurrentUser.Id && write && rows.Count > 0 && rows.All(x => x.Issue == null) && Now() - item.PreviewedAt <= TimeSpan.FromMinutes(15),
			CanApply = item.Status == "Approved" && write, CanDecide = await CanDecideAsync(item),
			Items = rows.Skip(skip).Take(take).Select(x => new StudentRollBatchRowDto { StudentId = x.StudentId, StudentName = x.StudentName, CurrentRollNumber = x.CurrentRollNumber, ProposedRollNumber = x.ProposedRollNumber, Issue = x.Issue }).ToList() };
	}
	private DateTimeOffset Now() => new(Clock.Now.ToUniversalTime());
	private static void Stamp(StudentRollNumberBatch item, string stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || item.ConcurrencyStamp != stamp) throw new BusinessException(StudentRollNumberErrorCodes.StalePreview);
	}
	private async Task EnsureFreshAsync(StudentRollNumberBatch item)
	{
		var fresh = await roster.ReadAsync(new() { AcademicYearId = item.AcademicYearId, StandardId = item.StandardId, DivisionId = item.DivisionId, Operation = item.Operation });
		if (fresh.Hash != item.RosterHash || fresh.Rows.Any(x => x.Issue != null) || !fresh.Rows.SequenceEqual(item.Rows())) throw new BusinessException(StudentRollNumberErrorCodes.StalePreview);
	}
}
