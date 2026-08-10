using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmConfigurationSnapshotSignature
{
	public static object Build(AdmissionCrmConfigurationSnapshot snapshot) => new
	{
		Year = new { snapshot.AcademicYear.Id, snapshot.AcademicYear.StartDate, snapshot.AcademicYear.EndDate },
		snapshot.Cycle,
		PipelineStages = snapshot.PipelineStages.OrderBy(x => x.Id),
		InquirySources = snapshot.InquirySources.OrderBy(x => x.Id),
		InquiryForms = snapshot.InquiryForms.OrderBy(x => x.Id),
		MessageTemplates = snapshot.MessageTemplates.OrderBy(x => x.Id),
		Playbooks = snapshot.Playbooks.OrderBy(x => x.Id),
		RegistrationTemplates = snapshot.RegistrationTemplates.OrderBy(x => x.Id),
		FormNumberSeries = snapshot.FormNumberSeries.OrderBy(x => x.Id),
		DocumentChecklists = snapshot.DocumentChecklists.OrderBy(x => x.Id),
		SeatPools = snapshot.SeatPools.OrderBy(x => x.Id),
		FeePolicies = snapshot.FeePolicies.OrderBy(x => x.Id),
		RouteDefinitions = snapshot.RouteDefinitions.OrderBy(x => x.Id),
		PolicyAdoptions = snapshot.PolicyAdoptions.OrderBy(x => x.Id),
		NumberSeries = snapshot.NumberSeries.OrderBy(x => x.Id),
		PolicyPacks = snapshot.PolicyPacks.OrderBy(x => x.Id),
		EducationBoards = snapshot.EducationBoards.OrderBy(x => x.Id).Select(x => new { x.Id, x.Type, x.StartDate, x.EndDate }),
		EducationMediums = snapshot.EducationMediums.OrderBy(x => x.Id).Select(x => new { x.Id, x.EducationBoardId, x.Name }),
		EducationMediumStages = snapshot.EducationMediumStages.OrderBy(x => x.Id).Select(x => new { x.Id, x.EducationMediumId, x.Name }),
		Standards = snapshot.Standards.OrderBy(x => x.Id).Select(x => new { x.Id, x.EducationMediumStageId, x.Name }),
		Divisions = snapshot.Divisions.OrderBy(x => x.Id).Select(x => new { x.Id, x.StandardId, x.Name })
	};
}
