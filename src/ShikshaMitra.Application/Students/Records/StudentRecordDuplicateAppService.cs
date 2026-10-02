using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View)]
public class StudentRecordDuplicateAppService(IRepository<StudentRecordDuplicateReview, Guid> reviews, IStudentRepository students,
	StudentRecordsAccessService access, StudentRecordsEnrollmentScopeService scope, IPermissionChecker permissions) : ApplicationService, IStudentRecordDuplicateAppService
{
	public async Task<PagedResultDto<StudentRecordDuplicateDto>> GetListAsync(StudentRecordDuplicateQuery input)
	{
		var studentQuery = await scope.ApplyStudentScopeAsync(await students.GetQueryableWithoutAutoIncludesAsync());
		var query = (await reviews.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && studentQuery.Any(s => s.Id == x.StudentId) && studentQuery.Any(s => s.Id == x.OtherStudentId));
		if (input.StudentId.HasValue) query = query.Where(x => x.StudentId == input.StudentId || x.OtherStudentId == input.StudentId);
		if (!string.IsNullOrEmpty(input.Status)) query = query.Where(x => x.Status == input.Status);
		var count = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id).Skip(input.SkipCount).Take(Math.Clamp(input.MaxResultCount, 1, 100)));
		var result = new List<StudentRecordDuplicateDto>(); foreach (var item in items) result.Add(await MapAsync(item));
		return new(count, result);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create)]
	public async Task<StudentRecordDuplicateDto> CreateAsync(StudentRecordDuplicateInput input)
	{
		await EnsureVisibleAsync(input.StudentId); await EnsureVisibleAsync(input.OtherStudentId);
		var review = new StudentRecordDuplicateReview(GuidGenerator.Create(), CurrentTenant.Id!.Value, input.StudentId, input.OtherStudentId, CurrentUser.GetId(), input.Reason);
		var existing = await reviews.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == review.StudentId && x.OtherStudentId == review.OtherStudentId && (x.Status == "Open" || x.Status == "ConfirmedDuplicate"));
		if (existing != null) return await MapAsync(existing);
		await reviews.InsertAsync(review, autoSave: true); return await MapAsync(review);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve)]
	public async Task<StudentRecordDuplicateDto> ResolveAsync(Guid id, StudentRecordDuplicateDecision input)
	{
		var review = await reviews.GetAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id);
		await EnsureVisibleAsync(review.StudentId); await EnsureVisibleAsync(review.OtherStudentId);
		if (review.ConcurrencyStamp != input.ConcurrencyStamp) throw new BusinessException("Students:Records:ConcurrencyConflict");
		review.Resolve(CurrentUser.GetId(), input.ConfirmedDuplicate, input.Reason); await reviews.UpdateAsync(review, autoSave: true);
		return await MapAsync(review);
	}
	private async Task<bool> IsVisibleAsync(Guid id, List<string> roles)
	{
		var record = await access.EnsureReadableAsync(id);
		return record.Access == "Full" && !record.Policy.Access.Masks.Any(x => record.Policy.IsMasked(x.FieldGroup, roles));
	}
	private async Task EnsureVisibleAsync(Guid id)
	{
		if (!await IsVisibleAsync(id, await access.GetDatabaseRoleNamesAsync())) throw new BusinessException("Students:Records:RestrictedFields");
	}
	private async Task<StudentRecordDuplicateDto> MapAsync(StudentRecordDuplicateReview review)
	{
		var roles = await access.GetDatabaseRoleNamesAsync();
		var visible = await IsVisibleAsync(review.StudentId, roles) && await IsVisibleAsync(review.OtherStudentId, roles);
		var first = await access.GetScopedStudentAsync(review.StudentId); var other = await access.GetScopedStudentAsync(review.OtherStudentId);
		return new() { Id = review.Id, StudentId = review.StudentId, OtherStudentId = review.OtherStudentId,
			StudentName = visible ? $"{first.FirstName} {first.LastName}".Trim() : "Restricted student",
			OtherStudentName = visible ? $"{other.FirstName} {other.LastName}".Trim() : "Restricted student",
			Reason = visible ? review.Reason : "Restricted", ReviewReason = visible ? review.ReviewReason : "Restricted",
			Status = review.Status, ConcurrencyStamp = review.ConcurrencyStamp, RequestedBy = review.RequestedBy, ReviewedBy = review.ReviewedBy,
			CreationTime = review.CreationTime, CanReview = visible && review.Status == "Open" && review.RequestedBy != CurrentUser.Id &&
				await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve) };
	}
}
