using System;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace ShikshaMitra.Students.Records;

/// <summary>Inline profile edits apply immediately as one audited transaction; generic approval commands retain their policy.</summary>
[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View)]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create)]
public class StudentRecordFormAppService(StudentRecordCustomDetailsService customDetails, StudentRecordFormProjection projection,
	StudentRecordsAccessService access, StudentRecordsPolicyResolver policies, IRepository<StudentRecordChange, Guid> changes,
	StudentRecordChangeAppService changeReader, StudentGuardianAccessReviewService guardianReviews) : ApplicationService, IStudentRecordFormAppService
{
	public async Task<StudentRecordSectionSnapshotDto> GetSnapshotAsync(Guid studentId)
	{
		await EnsurePermissionsAsync();
		await access.EnsureWritableAsync(studentId);
		var json = await CaptureAsync(studentId);
		return new() { Section = "registration-form", Snapshot = StudentRecordPayloadService.Hash(json), Data = JsonSerializer.Deserialize<JsonElement>(json) };
	}
	public async Task<StudentRecordChangeDto> SaveAsync(Guid studentId, SaveStudentRecordFormDto input)
	{
		await EnsurePermissionsAsync();
		try { return await SaveCoreAsync(studentId, input); }
		catch (AbpDbConcurrencyException) { throw new BusinessException("Students:Records:ConcurrencyConflict"); }
		catch (Exception exception) when (StudentOnboardingRollConflictMapper.IsSerializationConflict(exception))
		{ throw new BusinessException("Students:Records:ConcurrencyConflict"); }
	}
	private async Task<StudentRecordChangeDto> SaveCoreAsync(Guid studentId, SaveStudentRecordFormDto input)
	{
		Guid id;
		using (var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true))
		{
			var record = await access.EnsureWritableAsync(studentId);
			if (input.Payload.ValueKind != JsonValueKind.Object || input.Payload.GetRawText().Length > 65536)
				throw new BusinessException("Students:Records:InvalidPayload");
			var rawHash = StudentRecordPayloadService.Hash(input.Payload.GetRawText());
			var existing = await changes.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.ClientKey == input.ClientKey);
			if (existing != null)
			{
				if (existing.Section != "registration-form" || existing.GetProperty<string>("FormRequestHash") != rawHash || existing.Reason != input.Reason.Trim())
					throw new BusinessException("Students:Records:IdempotencyConflict");
				id = existing.Id;
			}
			else
			{
				var before = await CaptureAsync(studentId);
				if (StudentRecordPayloadService.Hash(before) != input.ExpectedSnapshot) throw new BusinessException("Students:Records:StaleSnapshot");
				var json = await customDetails.ValidateAndNormalizeAsync(studentId, input.Payload, registrationForm: true);
				if (StudentRecordPayloadService.Canonical(before) == json) throw new BusinessException("Students:Records:NoChanges");
				var year = record.Student.AcademicYearId ?? throw new BusinessException("Students:Records:EnrollmentMissing");
				var policy = await policies.ResolveAsync(year);
				var now = new DateTimeOffset(Clock.Now.ToUniversalTime());
				var item = new StudentRecordChange(GuidGenerator.Create(), CurrentTenant.Id, studentId, year, CurrentUser.GetId(),
					"registration-form", "Correction", input.ClientKey, input.Reason, before, json, policy.Policy.ToJson(), policy.VersionId, "[]", 0, now);
				item.SetProperty("FormRequestHash", rawHash);
				await changes.InsertAsync(item);
				var guardianChanged = await HasGuardianChangesAsync(studentId, before, json);
				await customDetails.ApplyAsync(studentId, json, registrationForm: true);
				if (guardianChanged) await guardianReviews.CaptureAsync(studentId, item.Id, item.PolicyJson);
				item.Apply(now);
				await changes.UpdateAsync(item);
				id = item.Id;
			}
			await work.CompleteAsync();
		}
		return await changeReader.GetAsync(id);
	}
	private async Task EnsurePermissionsAsync()
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View);
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create);
	}
	private async Task<bool> HasGuardianChangesAsync(Guid studentId, string beforeJson, string afterJson)
	{
		var before = JsonSerializer.Deserialize<StudentRecordCustomDetailsPayload>(beforeJson, StudentRecordPayloadService.JsonOptions)!;
		var after = JsonSerializer.Deserialize<StudentRecordCustomDetailsPayload>(afterJson, StudentRecordPayloadService.JsonOptions)!;
		foreach (var source in await customDetails.ReadAsync(studentId))
			foreach (var field in StudentRecordFormProjection.Fields(source).Where(f => f.BindingType == AdmissionFormBindingType.System && StudentRecordFormProjection.Target(f.BindingKey)?.Section == "family"))
				if (!StudentRecordCustomDetailsService.Equal(before.Sources.Single(x => x.SubmissionId == source.SubmissionId).Values[field.Code],
					after.Sources.Single(x => x.SubmissionId == source.SubmissionId).Values[field.Code])) return true;
		return false;
	}
	private async Task<string> CaptureAsync(Guid studentId)
	{
		var sources = await customDetails.ReadAsync(studentId);
		if (sources.Count == 0) throw new BusinessException("Students:Records:InvalidCustomSource");
		await projection.PopulateAsync(studentId, sources, true);
		if (!sources.Any(x => x.FormEditableFieldCodes.Count > 0)) throw new BusinessException("Students:Records:ReadOnly");
		return StudentRecordPayloadService.Canonical(JsonSerializer.Serialize(StudentRecordCustomDetailsService.ToPayload(sources, true), StudentRecordPayloadService.JsonOptions));
	}
}
