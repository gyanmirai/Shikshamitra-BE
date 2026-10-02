using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentRecords;

namespace ShikshaMitra.Students.Records;

public partial class StudentRecordChangeAppService
{
	private async Task<StudentRecordChangeDto> MapAsync(StudentRecordChange item, bool includePayload, Student? suppliedStudent = null,
		StudentRecordsPolicy? suppliedPolicy = null, List<string>? suppliedRoles = null,
		List<StudentRecordChangeDecision>? suppliedDecisions = null, bool? suppliedCanApprove = null, bool? suppliedCanCancel = null)
	{
		var student = suppliedStudent ?? await access.GetScopedStudentAsync(item.StudentId);
		var policy = suppliedPolicy ?? (await policies.ResolveAsync(item.AcademicYearId)).Policy;
		var currentPolicy = student.AcademicYearId == item.AcademicYearId ? policy : (await policies.ResolveAsync(student.AcademicYearId)).Policy;
		var roles = suppliedRoles ?? await access.GetDatabaseRoleNamesAsync();
		var history = suppliedDecisions ?? await decisions.GetListAsync(x => x.ChangeId == item.Id && x.TenantId == CurrentTenant.Id);
		var canApprove = suppliedCanApprove ?? await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve);
		var canCancel = suppliedCanCancel ?? await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Cancel);
		var masked = StudentRecordVisibility.IsMasked(policy, item.Section, roles) || StudentRecordVisibility.IsMasked(currentPolicy, item.Section, roles);
		var result = new StudentRecordChangeDto
		{
			Id = item.Id, StudentId = item.StudentId, StudentName = policy.IsMasked("Identity", roles) || currentPolicy.IsMasked("Identity", roles)
				? "Restricted student" : $"{student.FirstName} {student.LastName}".Trim(),
			AcademicYearId = item.AcademicYearId, RequestedBy = item.RequestedBy, CorrectsChangeId = item.CorrectsChangeId,
			PolicyVersionId = item.PolicyVersionId, Section = item.Section, Action = item.Action,
			Reason = masked ? "Restricted record change" : item.Reason, Status = item.Status, ConcurrencyStamp = item.ConcurrencyStamp,
			EffectiveAt = item.EffectiveAt, AppliedAt = item.AppliedAt, CreationTime = item.CreationTime,
			ApprovalCount = item.ApprovalCount, RequiredApprovals = item.RequiredApprovals, ExceptionCode = item.ExceptionCode,
			PayloadMasked = masked, CanApprove = !masked && canApprove && IsStageApprover(item, roles, history),
			CanCancel = item.RequestedBy == CurrentUser.Id && (item.Status is "Pending" or "Scheduled") && canCancel
		};
		if (includePayload && !masked)
		{
			result.Before = JsonSerializer.Deserialize<JsonElement>(item.BeforeJson);
			result.Proposed = JsonSerializer.Deserialize<JsonElement>(item.PayloadJson);
			if (item.Section == "photo" && result.Proposed.Value.TryGetProperty("photoDraftId", out var photoId) && photoId.TryGetGuid(out var draftId))
				result.PhotoPreviewUrl = $"/api/students/records/changes/students/{item.StudentId}/photo-drafts/{draftId}";
		}
		if (includePayload)
			result.Decisions = history
				.OrderBy(x => x.Stage).Select(x => new StudentRecordDecisionHistoryDto { ActorId = x.ActorId, Stage = x.Stage,
					Decision = x.Decision, Reason = masked ? "Restricted" : x.Reason, CreationTime = x.CreationTime }).ToList();
		return result;
	}
}
