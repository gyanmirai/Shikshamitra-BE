using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmPublicLocalization
{
	private static readonly IReadOnlyDictionary<string, (string Gujarati, string Hindi)> Translations =
		new Dictionary<string, (string, string)>
		{
			["Completed"] = ("પૂર્ણ", "पूर्ण"), ["Current"] = ("ચાલુ", "वर्तमान"), ["Pending"] = ("બાકી", "लंबित"),
			["Inquiry received"] = ("પૂછપરછ પ્રાપ્ત થઈ", "पूछताछ प्राप्त हुई"), ["Admissions follow-up"] = ("પ્રવેશ અનુસરણ", "प्रवेश अनुवर्ती कार्रवाई"),
			["Entrance exam schedule"] = ("પ્રવેશ પરીક્ષા સમયપત્રક", "प्रवेश परीक्षा समय-सारणी"), ["Entrance exam result"] = ("પ્રવેશ પરીક્ષા પરિણામ", "प्रवेश परीक्षा परिणाम"),
			["Eligibility decision"] = ("પાત્રતા નિર્ણય", "पात्रता निर्णय"), ["New Inquiry"] = ("નવી પૂછપરછ", "नई पूछताछ"),
			["Follow-up"] = ("અનુસરણ", "अनुवर्ती कार्रवाई"), ["Exam Ready"] = ("પરીક્ષા માટે તૈયાર", "परीक्षा के लिए तैयार"),
			["Exam Scheduled"] = ("પરીક્ષા નિર્ધારિત", "परीक्षा निर्धारित"), ["Exam Completed"] = ("પરીક્ષા પૂર્ણ", "परीक्षा पूर्ण"),
			["Eligible"] = ("પાત્ર", "पात्र"), ["Waitlisted"] = ("પ્રતીક્ષા સૂચિ", "प्रतीक्षा सूची"), ["Rejected"] = ("નામંજૂર", "अस्वीकृत"),
			["Stale"] = ("અનુસરણ બાકી", "अनुवर्ती लंबित"), ["Archived"] = ("આર્કાઇવ", "संग्रहीत"),
			["The admissions team will review the inquiry and follow up."] = ("પ્રવેશ ટીમ પૂછપરછની સમીક્ષા કરીને સંપર્ક કરશે.", "प्रवेश टीम पूछताछ की समीक्षा करके संपर्क करेगी।"),
			["The admissions team is following up on the inquiry."] = ("પ્રવેશ ટીમ પૂછપરછ પર અનુસરણ કરી રહી છે.", "प्रवेश टीम पूछताछ पर अनुवर्ती कार्रवाई कर रही है।"),
			["The inquiry is ready for entrance exam scheduling."] = ("પૂછપરછ પ્રવેશ પરીક્ષા નક્કી કરવા તૈયાર છે.", "पूछताछ प्रवेश परीक्षा निर्धारित करने के लिए तैयार है।"),
			["The entrance exam has been scheduled."] = ("પ્રવેશ પરીક્ષા નક્કી થઈ ગઈ છે.", "प्रवेश परीक्षा निर्धारित हो गई है।"),
			["The entrance exam result is being reviewed."] = ("પ્રવેશ પરીક્ષાના પરિણામની સમીક્ષા થઈ રહી છે.", "प्रवेश परीक्षा परिणाम की समीक्षा हो रही है।"),
			["The applicant is eligible for admission handoff."] = ("અરજદાર પ્રવેશ હેન્ડઓફ માટે પાત્ર છે.", "आवेदक प्रवेश हस्तांतरण के लिए पात्र है।"),
			["The applicant is currently waitlisted."] = ("અરજદાર હાલમાં પ્રતીક્ષા સૂચિમાં છે.", "आवेदक वर्तमान में प्रतीक्षा सूची में है।"),
			["The applicant is not eligible at this stage."] = ("અરજદાર આ તબક્કે પાત્ર નથી.", "आवेदक इस चरण में पात्र नहीं है।"),
			["The inquiry needs renewed follow-up."] = ("પૂછપરછ માટે ફરી અનુસરણ જરૂરી છે.", "पूछताछ के लिए दोबारा अनुवर्ती कार्रवाई आवश्यक है।"),
			["The inquiry is archived."] = ("પૂછપરછ આર્કાઇવ કરવામાં આવી છે.", "पूछताछ संग्रहीत की गई है।"),
			["The admissions team will share the next step."] = ("પ્રવેશ ટીમ આગળનું પગલું જણાવશે.", "प्रवेश टीम अगला चरण बताएगी।"),
			["Inquiry submitted"] = ("પૂછપરછ મોકલાઈ", "पूछताछ भेजी गई"), ["Entrance exam scheduled"] = ("પ્રવેશ પરીક્ષા નિર્ધારિત", "प्रवेश परीक्षा निर्धारित"),
			["Entrance exam attendance updated"] = ("પ્રવેશ પરીક્ષા હાજરી સુધારાઈ", "प्रवेश परीक्षा उपस्थिति अपडेट हुई"),
			["Entrance exam result updated"] = ("પ્રવેશ પરીક્ષા પરિણામ સુધારાયું", "प्रवेश परीक्षा परिणाम अपडेट हुआ"),
			["Applicant checklist updated"] = ("અરજદાર ચેકલિસ્ટ સુધારાયું", "आवेदक चेकलिस्ट अपडेट हुई"),
			["Admission offer issued"] = ("પ્રવેશ ઓફર જારી", "प्रवेश प्रस्ताव जारी"), ["Admission offer accepted"] = ("પ્રવેશ ઓફર સ્વીકારાઈ", "प्रवेश प्रस्ताव स्वीकार"),
			["Admission offer declined"] = ("પ્રવેશ ઓફર નકારી", "प्रवेश प्रस्ताव अस्वीकार"), ["Student activation in progress"] = ("વિદ્યાર્થી સક્રિયકરણ ચાલુ", "छात्र सक्रियण जारी"),
			["Student ready to join"] = ("વિદ્યાર્થી જોડાવા તૈયાર", "छात्र प्रवेश के लिए तैयार")
		};

	internal static AdmissionApplicantStatusDto Apply(
		AdmissionApplicantStatusDto status, string? requestedLanguage, IReadOnlyList<string> supportedLanguages)
	{
		var language = AdmissionCrmPublicBranding.NormalizeLanguage(requestedLanguage, supportedLanguages);
		status.Language = language;
		status.SupportedLanguages = supportedLanguages;
		if (language is not ("gu" or "hi")) return status;
		status.StageName = Translate(language, GetDefaultStageName(status.Stage));
		status.NextStep = Translate(language, status.NextStep);
		foreach (var item in status.Checklist.Concat(status.ApplicantChecklist))
		{
			item.Label = Translate(language, item.Label);
			item.Status = Translate(language, item.Status);
			if (item.Details != null) item.Details = Translate(language, item.Details);
		}
		foreach (var item in status.Activity)
		{
			item.Title = Translate(language, item.Title);
			if (item.Details != null) item.Details = Translate(language, item.Details);
		}
		return status;
	}

	private static string Translate(string language, string text) => Translations.TryGetValue(text, out var value)
		? language == "gu" ? value.Gujarati : value.Hindi
		: text;

	private static string GetDefaultStageName(AdmissionInquiryStage stage) => stage switch
	{
		AdmissionInquiryStage.NewInquiry => "New Inquiry", AdmissionInquiryStage.FollowUp => "Follow-up",
		AdmissionInquiryStage.ExamReady => "Exam Ready", AdmissionInquiryStage.ExamScheduled => "Exam Scheduled",
		AdmissionInquiryStage.ExamCompleted => "Exam Completed", AdmissionInquiryStage.Eligible => "Eligible",
		AdmissionInquiryStage.Waitlisted => "Waitlisted", AdmissionInquiryStage.Rejected => "Rejected",
		AdmissionInquiryStage.Stale => "Stale", AdmissionInquiryStage.Archived => "Archived", _ => stage.ToString()
	};
}
