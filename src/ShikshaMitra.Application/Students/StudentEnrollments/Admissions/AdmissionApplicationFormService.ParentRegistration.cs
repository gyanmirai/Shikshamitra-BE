using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionApplicationFormService
{
	// Internal capability adapter: the public service authenticates the grant, tenant and snapshot first.
	// Reuses canonical bindings while preventing any shared guardian recovery or identity writes.
	internal async Task SaveParentDraftAsync(AdmissionApplication application, AdmissionParentRegistration grant,
		AdmissionParentForm form, List<AdmissionFormValueDto> values, IReadOnlySet<string> writable)
	{
		application.SaveDraft();
		await ApplyValuesAsync(application, form.Instance, new() { Values = values }, writable, parentAuthor: true);
		form.Instance.MarkSaved();
		await instanceRepository.UpdateAsync(form.Instance, false);
		await historyRepository.InsertAsync(new AdmissionFormChangeHistory(GuidGenerator.Create(), CurrentTenant.Id, form.Instance.Id,
			application.Id, "ParentDraftSaved", JsonSerializer.Serialize(new { ParentRegistrationId = grant.Id,
				Recipient = JsonSerializer.Deserialize<AdmissionParentRecipientDto>(grant.RecipientJson), FieldCodes = values.Select(value => value.FieldCode) }), null), false);
		await applicationRepository.UpdateAsync(application, false);
	}
}
