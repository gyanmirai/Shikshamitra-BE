using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>One admission fact, synchronized to editable answers without rewriting submission history.</summary>
public class AdmissionJoiningDateCapture(
	IRepository<AdmissionFormInstance, Guid> instances,
	IRepository<AdmissionFormFieldDefinition, Guid> fields,
	IRepository<AdmissionFormFieldValue, Guid> values,
	IRepository<AdmissionFormChangeHistory, Guid> history,
	IRepository<AdmissionApplicant, Guid> applicants,
	IRepository<Student, Guid> students,
	IRepository<FinanceFeeAccount, Guid> accounts,
	Volo.Abp.Guids.IGuidGenerator guids,
	Volo.Abp.Users.ICurrentUser currentUser,
	AdmissionJoiningDateReader reader) : ITransientDependency
{
	public Task<bool> IsLockedAsync(AdmissionApplication application) => application.HasFinanceFeePayment
		? Task.FromResult(true)
		: accounts.AnyAsync(a => a.ApplicationId == application.Id && a.TenantId == application.TenantId &&
			(a.AssignmentSnapshotJson != null || a.HasPaymentEvidence));

	public static bool IsDateField(AdmissionFormFieldDefinition field) => field.FieldType == AdmissionFormFieldType.Date &&
		(field.BindingType == AdmissionFormBindingType.System && field.BindingKey == "Academic.JoiningDate" ||
			field.Code is "JOINING_DATE" or "ACTUAL_JOINING_DATE");

	public async Task SynchronizeAsync(AdmissionApplication application, DateTime date)
	{
		if (await reader.ReadAsync(application) != date && await IsLockedAsync(application)) ThrowLocked();
		application.ConfirmJoiningDate(date, false);
		var formInstances = await instances.GetListAsync(i => i.ApplicationId == application.Id && i.TenantId == application.TenantId);
		foreach (var instance in formInstances)
		{
			var definitions = (await fields.GetListAsync(f => f.FormDefinitionId == instance.FormDefinitionId)).Where(IsDateField).ToList();
			if (definitions.Count == 0) continue;
			var codes = definitions.Select(d => d.Code).ToArray();
			var answers = await values.GetListAsync(v => v.FormInstanceId == instance.Id && v.TenantId == application.TenantId && Enumerable.Contains(codes, v.FieldCode));
			foreach (var field in definitions)
			{
				var json = JsonSerializer.Serialize(date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
				var answer = answers.FirstOrDefault(v => v.FieldCode == field.Code);
				if (answer == null) await values.InsertAsync(new AdmissionFormFieldValue(guids.Create(), application.TenantId, instance.Id, field.Code, json), false);
				else { answer.SetValue(json); await values.UpdateAsync(answer, false); }
			}
			// Submitted snapshots remain immutable; the explicit audited date confirmation
			// updates the current bound answer without reopening or resubmitting the form.
			if (instance.Status != AdmissionFormInstanceStatus.Submitted)
			{
				instance.MarkSaved();
				await instances.UpdateAsync(instance, false);
			}
			await history.InsertAsync(new AdmissionFormChangeHistory(guids.Create(), application.TenantId, instance.Id,
				application.Id, "JoiningDateConfirmed", JsonSerializer.Serialize(codes), currentUser.Id), false);
		}
		var applicant = await applicants.GetAsync(application.ApplicantId);
		AdmissionAdditionalStudentFieldCatalog.ApplyValue("Academic.JoiningDate", date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), applicant);
		await applicants.UpdateAsync(applicant, false);
		if (application.StudentId.HasValue)
		{
			var student = await students.GetAsync(application.StudentId.Value);
			if (student.TenantId != application.TenantId) throw new Volo.Abp.Authorization.AbpAuthorizationException();
			student.ExtraProperties["AdmissionStudentField.Academic.JoiningDate"] = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
			await students.UpdateAsync(student, false);
		}
	}

	public async Task GuardFormChangeAsync(AdmissionApplication application, IReadOnlyList<AdmissionFormFieldDefinition> definitions,
		IReadOnlyList<AdmissionFormValueDto> requested)
	{
		var dateCodes = definitions.Where(f => !f.ReadOnly && IsDateField(f)).Select(f => f.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var changes = requested.Where(v => dateCodes.Contains(v.FieldCode)).ToList();
		if (changes.Count == 0) return;
		var current = await reader.ReadAsync(application);
		var dates = changes.Select(v => AdmissionApplicationValueConverter.Unwrap(v.ValueJson))
			.Select(v => string.IsNullOrWhiteSpace(v) ? (DateTime?)null : AdmissionJoiningDateReader.Parse(v)).Distinct().ToList();
		if (dates.Count != 1) throw new BusinessException(ShikshaMitra.Domain.Finance.Policies.ProrationPolicyErrorCodes.JoiningDateInvalid);
		if (dates[0] == current) return;
		if (await IsLockedAsync(application)) ThrowLocked();
		// Before Payment confirms a date, ordinary Registration remains the authority.
		if (application.GetJoiningDate().HasValue)
		{
			if (!dates[0].HasValue) throw new BusinessException(AdmissionFeeSelectionErrorCodes.InvalidSelection,
				"Confirm the actual joining date before clearing the saved admission date.");
			await SynchronizeAsync(application, dates[0]!.Value);
		}
	}

	internal static void ThrowLocked() => throw new BusinessException(AdmissionFeeSelectionErrorCodes.Locked,
		"Fees have already been prepared. Ask Finance to review a joining date correction.");
}
