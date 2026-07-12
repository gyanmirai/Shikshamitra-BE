using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class RunAdmissionCrmJourneyRulesDto
{
	public AdmissionCrmJourneyTrigger Trigger { get; set; } = AdmissionCrmJourneyTrigger.LeadCreated;
	public Guid? LeadId { get; set; }
	public AdmissionInquiryStage? Stage { get; set; }
}

public class AdmissionCrmJourneyRunResultDto
{
	public AdmissionCrmJourneyTrigger Trigger { get; set; }
	public Guid? LeadId { get; set; }
	public AdmissionInquiryStage? Stage { get; set; }
	public int EvaluatedLeadCount { get; set; }
	public int MatchedRuleCount { get; set; }
	public int CreatedTaskCount { get; set; }
	public int SkippedExistingTaskCount { get; set; }
	public int SentMessageCount { get; set; }
	public int SkippedExistingMessageCount { get; set; }
	public int FailedActionCount { get; set; }
	public List<string> FailureMessages { get; set; } = [];
}
