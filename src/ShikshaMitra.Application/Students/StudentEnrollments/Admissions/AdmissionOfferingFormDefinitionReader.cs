using System.Collections.Generic;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionOfferingFormDefinitionReader
{
	public static AdmissionFormDto? Read(AdmissionFormDefinition form, List<AdmissionFormSectionDefinition> sections,
		List<AdmissionFormFieldDefinition> fields)
	{
		try { return AdmissionFormAppService.Map(form, sections, fields); }
		catch (BusinessException exception) when (exception.Code == AdmissionFormInheritanceErrorCodes.InvalidPolicy)
		{
			// An invalid linked definition blocks its own Offering. Other scoped rows remain readable.
			return null;
		}
	}
}
