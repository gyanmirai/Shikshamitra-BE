using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Students module - Enrollment related permissions
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Students
	{
		/// <summary>
		/// Student Enrollment - Admissions CRM permissions
		/// </summary>
		public static class Enrollment_AdmissionsCrm
		{
			private const string SubmoduleName = $"{ModuleName}.Enrollment.AdmissionsCrm";
			private const string SubmoduleDisplayName = $"{DisplayName}.Enrollment.AdmissionsCrm";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string ManageConfiguration = $"{SubmoduleName}.ManageConfiguration";
			public const string ManagePlaybooks = $"{SubmoduleName}.ManagePlaybooks";
			public const string PublishPlaybooks = $"{SubmoduleName}.PublishPlaybooks";
			public const string ViewSeatPlans = $"{SubmoduleName}.ViewSeatPlans";
			public const string ManageExamConfiguration = $"{SubmoduleName}.ManageExamConfiguration";
			public const string PublishExamResults = $"{SubmoduleName}.PublishExamResults";
			public const string OverrideExamResult = $"{SubmoduleName}.OverrideExamResult";
			public const string MergeDuplicates = $"{SubmoduleName}.MergeDuplicates";
			public const string AssignOwner = $"{SubmoduleName}.AssignOwner";
			public const string SetPriority = $"{SubmoduleName}.SetPriority";
			public const string IssueRegistrationForm = $"{SubmoduleName}.IssueRegistrationForm";
			public const string ManageRegistrationTemplates = $"{SubmoduleName}.ManageRegistrationTemplates";
			public const string PublishRegistrationTemplates = $"{SubmoduleName}.PublishRegistrationTemplates";
			public const string ManageFormNumberSeries = $"{SubmoduleName}.ManageFormNumberSeries";
			public const string ManageRegistrationLinks = $"{SubmoduleName}.ManageRegistrationLinks";
			public const string VerifyRegistration = $"{SubmoduleName}.VerifyRegistration";
			public const string WaiveRegistrationFee = $"{SubmoduleName}.WaiveRegistrationFee";
			public const string ManageDocumentConfiguration = $"{SubmoduleName}.ManageDocumentConfiguration";
			public const string ReviewDocuments = $"{SubmoduleName}.ReviewDocuments";
			public const string WaiveDocuments = $"{SubmoduleName}.WaiveDocuments";
			public const string ConfirmOriginalSeen = $"{SubmoduleName}.ConfirmOriginalSeen";
			public const string ConfirmAdmissionReadiness = $"{SubmoduleName}.ConfirmAdmissionReadiness";
			public const string OverrideAdmissionReadiness = $"{SubmoduleName}.OverrideAdmissionReadiness";
			public const string ChangeStage = $"{SubmoduleName}.ChangeStage";
			public const string ManageTasks = $"{SubmoduleName}.ManageTasks";
			public const string ArchiveReopen = $"{SubmoduleName}.ArchiveReopen";
			public const string ScheduleExam = $"{SubmoduleName}.ScheduleExam";
			public const string OverrideExamCapacity = $"{SubmoduleName}.OverrideExamCapacity";
			public const string ChangeExamResult = $"{SubmoduleName}.ChangeExamResult";
			public const string OverrideEligibility = $"{SubmoduleName}.OverrideEligibility";
			public const string ManageTemplates = $"{SubmoduleName}.ManageTemplates";
			public const string ManageAutomation = $"{SubmoduleName}.ManageAutomation";
			public const string Analytics = $"{SubmoduleName}.Analytics";
			public const string ManageSeatPlans = $"{SubmoduleName}.ManageSeatPlans";
			public const string ManageSeatReservations = $"{SubmoduleName}.ManageSeatReservations";
			public const string ManageOffers = $"{SubmoduleName}.ManageOffers";
			public const string ApproveOffers = $"{SubmoduleName}.ApproveOffers";
			public const string ManageWaitlist = $"{SubmoduleName}.ManageWaitlist";
			public const string MakeAdmissionDecision = $"{SubmoduleName}.MakeAdmissionDecision";
			public const string OverrideSeatCapacity = $"{SubmoduleName}.OverrideSeatCapacity";
			public const string CancelAdmission = $"{SubmoduleName}.CancelAdmission";
			public const string ViewFinancialStatus = $"{SubmoduleName}.ViewFinancialStatus";
			public const string ManageFeePolicies = $"{SubmoduleName}.ManageFeePolicies";
			public const string ApproveFeePolicies = $"{SubmoduleName}.ApproveFeePolicies";
			public const string IssueQuotations = $"{SubmoduleName}.IssueQuotations";
			public const string RecordFinanceStatus = $"{SubmoduleName}.RecordFinanceStatus";
			public const string RequestConcession = $"{SubmoduleName}.RequestConcession";
			public const string ApproveConcession = $"{SubmoduleName}.ApproveConcession";
			public const string OverridePaymentGates = $"{SubmoduleName}.OverridePaymentGates";
			public const string ViewHouseholds = $"{SubmoduleName}.ViewHouseholds";
			public const string ManageHouseholds = $"{SubmoduleName}.ManageHouseholds";
			public const string ManageGuardianAuthority = $"{SubmoduleName}.ManageGuardianAuthority";
			public const string ViewProtectedRelationships = $"{SubmoduleName}.ViewProtectedRelationships";
			public const string ManageProtectedRelationships = $"{SubmoduleName}.ManageProtectedRelationships";
			public const string VerifyIdentity = $"{SubmoduleName}.VerifyIdentity";
			public const string ManageGovernmentReadiness = $"{SubmoduleName}.ManageGovernmentReadiness";
			public const string LinkAccounts = $"{SubmoduleName}.LinkAccounts";
			public const string OverrideIdentityReadiness = $"{SubmoduleName}.OverrideIdentityReadiness";
			public const string ViewPolicyPacks = $"{SubmoduleName}.ViewPolicyPacks";
			public const string ManagePolicyPacks = $"{SubmoduleName}.ManagePolicyPacks";
			public const string ApprovePolicyPacks = $"{SubmoduleName}.ApprovePolicyPacks";
			public const string AdoptPolicyPacks = $"{SubmoduleName}.AdoptPolicyPacks";
			public const string EvaluatePolicy = $"{SubmoduleName}.EvaluatePolicy";
			public const string RequestPolicyException = $"{SubmoduleName}.RequestPolicyException";
			public const string ApprovePolicyException = $"{SubmoduleName}.ApprovePolicyException";
			public const string ViewActivation = $"{SubmoduleName}.ViewActivation";
			public const string ManageNumberSeries = $"{SubmoduleName}.ManageNumberSeries";
			public const string PublishNumberSeries = $"{SubmoduleName}.PublishNumberSeries";
			public const string PreviewActivation = $"{SubmoduleName}.PreviewActivation";
			public const string RequestActivation = $"{SubmoduleName}.RequestActivation";
			public const string ApproveActivation = $"{SubmoduleName}.ApproveActivation";
			public const string ExecuteActivation = $"{SubmoduleName}.ExecuteActivation";
			public const string RepairActivation = $"{SubmoduleName}.RepairActivation";
			public const string ViewAdmissionRoutes = $"{SubmoduleName}.ViewAdmissionRoutes";
			public const string ManageAdmissionRoutes = $"{SubmoduleName}.ManageAdmissionRoutes";
			public const string ApproveAdmissionRoutes = $"{SubmoduleName}.ApproveAdmissionRoutes";
			public const string AssignAdmissionRoute = $"{SubmoduleName}.AssignAdmissionRoute";
			public const string EvaluateAdmissionRoute = $"{SubmoduleName}.EvaluateAdmissionRoute";
			public const string ManageRouteAppeals = $"{SubmoduleName}.ManageRouteAppeals";
			public const string ApproveRouteAppeals = $"{SubmoduleName}.ApproveRouteAppeals";
			public const string ViewDataQuality = $"{SubmoduleName}.ViewDataQuality";
			public const string RunReconciliation = $"{SubmoduleName}.RunReconciliation";
			public const string ResolveDataQuality = $"{SubmoduleName}.ResolveDataQuality";
			public const string AssignManualIdentifier = $"{SubmoduleName}.AssignManualIdentifier";
			public const string ManageServiceHandoffs = $"{SubmoduleName}.ManageServiceHandoffs";
			public const string BulkOperations = $"{SubmoduleName}.BulkOperations";
			public const string ManageOperationsQueues = $"{SubmoduleName}.ManageOperationsQueues";
			public const string ManageDelegation = $"{SubmoduleName}.ManageDelegation";
			public const string ViewAudit = $"{SubmoduleName}.ViewAudit";
			public const string ManageRetention = $"{SubmoduleName}.ManageRetention";
			public const string SensitiveExport = $"{SubmoduleName}.SensitiveExport";
			public const string ProtectedAnalytics = $"{SubmoduleName}.ProtectedAnalytics";
			public const string ManageScheduledReports = $"{SubmoduleName}.ManageScheduledReports";

			public static readonly string[] All =
			{
				View,
				Add,
				ManageConfiguration,
				ManagePlaybooks,
				PublishPlaybooks,
				ViewSeatPlans,
				ManageExamConfiguration,
				PublishExamResults,
				OverrideExamResult,
				MergeDuplicates,
				AssignOwner,
				SetPriority,
				IssueRegistrationForm,
				ManageRegistrationTemplates,
				PublishRegistrationTemplates,
				ManageFormNumberSeries,
				ManageRegistrationLinks,
				VerifyRegistration,
				WaiveRegistrationFee,
				ManageDocumentConfiguration,
				ReviewDocuments,
				WaiveDocuments,
				ConfirmOriginalSeen,
				ConfirmAdmissionReadiness,
				OverrideAdmissionReadiness,
				ChangeStage,
				ManageTasks,
				ArchiveReopen,
				ScheduleExam,
				OverrideExamCapacity,
				ChangeExamResult,
				OverrideEligibility,
				ManageTemplates,
				ManageAutomation,
				Analytics,
				ManageSeatPlans,
				ManageSeatReservations,
				ManageOffers,
				ApproveOffers,
				ManageWaitlist,
				MakeAdmissionDecision,
				OverrideSeatCapacity,
				CancelAdmission,
				ViewFinancialStatus,
				ManageFeePolicies,
				ApproveFeePolicies,
				IssueQuotations,
				RecordFinanceStatus,
				RequestConcession,
				ApproveConcession,
				OverridePaymentGates,
				ViewHouseholds,
				ManageHouseholds,
				ManageGuardianAuthority,
				ViewProtectedRelationships,
				ManageProtectedRelationships,
				VerifyIdentity,
				ManageGovernmentReadiness,
				LinkAccounts,
				OverrideIdentityReadiness,
				ViewPolicyPacks,
				ManagePolicyPacks,
				ApprovePolicyPacks,
				AdoptPolicyPacks,
				EvaluatePolicy,
				RequestPolicyException,
				ApprovePolicyException,
				ViewActivation,
				ManageNumberSeries,
				PublishNumberSeries,
				PreviewActivation,
				RequestActivation,
				ApproveActivation,
				ExecuteActivation,
				RepairActivation,
				ViewAdmissionRoutes,
				ManageAdmissionRoutes,
				ApproveAdmissionRoutes,
				AssignAdmissionRoute,
				EvaluateAdmissionRoute,
				ManageRouteAppeals,
				ApproveRouteAppeals,
				ViewDataQuality,
				RunReconciliation,
				ResolveDataQuality,
				AssignManualIdentifier,
				ManageServiceHandoffs,
				BulkOperations,
				ManageOperationsQueues,
				ManageDelegation,
				ViewAudit,
				ManageRetention,
				SensitiveExport,
				ProtectedAnalytics,
				ManageScheduledReports
			};

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ ManageConfiguration, $"{SubmoduleDisplayName}.ManageConfiguration" },
				{ ManagePlaybooks, $"{SubmoduleDisplayName}.ManagePlaybooks" },
				{ PublishPlaybooks, $"{SubmoduleDisplayName}.PublishPlaybooks" },
				{ ViewSeatPlans, $"{SubmoduleDisplayName}.ViewSeatPlans" },
				{ ManageExamConfiguration, $"{SubmoduleDisplayName}.ManageExamConfiguration" },
				{ PublishExamResults, $"{SubmoduleDisplayName}.PublishExamResults" },
				{ OverrideExamResult, $"{SubmoduleDisplayName}.OverrideExamResult" },
				{ MergeDuplicates, $"{SubmoduleDisplayName}.MergeDuplicates" },
				{ AssignOwner, $"{SubmoduleDisplayName}.AssignOwner" },
				{ SetPriority, $"{SubmoduleDisplayName}.SetPriority" },
				{ IssueRegistrationForm, $"{SubmoduleDisplayName}.IssueRegistrationForm" },
				{ ManageRegistrationTemplates, $"{SubmoduleDisplayName}.ManageRegistrationTemplates" },
				{ PublishRegistrationTemplates, $"{SubmoduleDisplayName}.PublishRegistrationTemplates" },
				{ ManageFormNumberSeries, $"{SubmoduleDisplayName}.ManageFormNumberSeries" },
				{ ManageRegistrationLinks, $"{SubmoduleDisplayName}.ManageRegistrationLinks" },
				{ VerifyRegistration, $"{SubmoduleDisplayName}.VerifyRegistration" },
				{ WaiveRegistrationFee, $"{SubmoduleDisplayName}.WaiveRegistrationFee" },
				{ ManageDocumentConfiguration, $"{SubmoduleDisplayName}.ManageDocumentConfiguration" },
				{ ReviewDocuments, $"{SubmoduleDisplayName}.ReviewDocuments" },
				{ WaiveDocuments, $"{SubmoduleDisplayName}.WaiveDocuments" },
				{ ConfirmOriginalSeen, $"{SubmoduleDisplayName}.ConfirmOriginalSeen" },
				{ ConfirmAdmissionReadiness, $"{SubmoduleDisplayName}.ConfirmAdmissionReadiness" },
				{ OverrideAdmissionReadiness, $"{SubmoduleDisplayName}.OverrideAdmissionReadiness" },
				{ ChangeStage, $"{SubmoduleDisplayName}.ChangeStage" },
				{ ManageTasks, $"{SubmoduleDisplayName}.ManageTasks" },
				{ ArchiveReopen, $"{SubmoduleDisplayName}.ArchiveReopen" },
				{ ScheduleExam, $"{SubmoduleDisplayName}.ScheduleExam" },
				{ OverrideExamCapacity, $"{SubmoduleDisplayName}.OverrideExamCapacity" },
				{ ChangeExamResult, $"{SubmoduleDisplayName}.ChangeExamResult" },
				{ OverrideEligibility, $"{SubmoduleDisplayName}.OverrideEligibility" },
				{ ManageTemplates, $"{SubmoduleDisplayName}.ManageTemplates" },
				{ ManageAutomation, $"{SubmoduleDisplayName}.ManageAutomation" },
				{ Analytics, $"{SubmoduleDisplayName}.Analytics" },
				{ ManageSeatPlans, $"{SubmoduleDisplayName}.ManageSeatPlans" },
				{ ManageSeatReservations, $"{SubmoduleDisplayName}.ManageSeatReservations" },
				{ ManageOffers, $"{SubmoduleDisplayName}.ManageOffers" },
				{ ApproveOffers, $"{SubmoduleDisplayName}.ApproveOffers" },
				{ ManageWaitlist, $"{SubmoduleDisplayName}.ManageWaitlist" },
				{ MakeAdmissionDecision, $"{SubmoduleDisplayName}.MakeAdmissionDecision" },
				{ OverrideSeatCapacity, $"{SubmoduleDisplayName}.OverrideSeatCapacity" },
				{ CancelAdmission, $"{SubmoduleDisplayName}.CancelAdmission" },
				{ ViewFinancialStatus, $"{SubmoduleDisplayName}.ViewFinancialStatus" },
				{ ManageFeePolicies, $"{SubmoduleDisplayName}.ManageFeePolicies" },
				{ ApproveFeePolicies, $"{SubmoduleDisplayName}.ApproveFeePolicies" },
				{ IssueQuotations, $"{SubmoduleDisplayName}.IssueQuotations" },
				{ RecordFinanceStatus, $"{SubmoduleDisplayName}.RecordFinanceStatus" },
				{ RequestConcession, $"{SubmoduleDisplayName}.RequestConcession" },
				{ ApproveConcession, $"{SubmoduleDisplayName}.ApproveConcession" },
				{ OverridePaymentGates, $"{SubmoduleDisplayName}.OverridePaymentGates" },
				{ ViewHouseholds, $"{SubmoduleDisplayName}.ViewHouseholds" },
				{ ManageHouseholds, $"{SubmoduleDisplayName}.ManageHouseholds" },
				{ ManageGuardianAuthority, $"{SubmoduleDisplayName}.ManageGuardianAuthority" },
				{ ViewProtectedRelationships, $"{SubmoduleDisplayName}.ViewProtectedRelationships" },
				{ ManageProtectedRelationships, $"{SubmoduleDisplayName}.ManageProtectedRelationships" },
				{ VerifyIdentity, $"{SubmoduleDisplayName}.VerifyIdentity" },
				{ ManageGovernmentReadiness, $"{SubmoduleDisplayName}.ManageGovernmentReadiness" },
				{ LinkAccounts, $"{SubmoduleDisplayName}.LinkAccounts" },
				{ OverrideIdentityReadiness, $"{SubmoduleDisplayName}.OverrideIdentityReadiness" },
				{ ViewPolicyPacks, $"{SubmoduleDisplayName}.ViewPolicyPacks" },
				{ ManagePolicyPacks, $"{SubmoduleDisplayName}.ManagePolicyPacks" },
				{ ApprovePolicyPacks, $"{SubmoduleDisplayName}.ApprovePolicyPacks" },
				{ AdoptPolicyPacks, $"{SubmoduleDisplayName}.AdoptPolicyPacks" },
				{ EvaluatePolicy, $"{SubmoduleDisplayName}.EvaluatePolicy" },
				{ RequestPolicyException, $"{SubmoduleDisplayName}.RequestPolicyException" },
				{ ApprovePolicyException, $"{SubmoduleDisplayName}.ApprovePolicyException" },
				{ ViewActivation, $"{SubmoduleDisplayName}.ViewActivation" },
				{ ManageNumberSeries, $"{SubmoduleDisplayName}.ManageNumberSeries" },
				{ PublishNumberSeries, $"{SubmoduleDisplayName}.PublishNumberSeries" },
				{ PreviewActivation, $"{SubmoduleDisplayName}.PreviewActivation" },
				{ RequestActivation, $"{SubmoduleDisplayName}.RequestActivation" },
				{ ApproveActivation, $"{SubmoduleDisplayName}.ApproveActivation" },
				{ ExecuteActivation, $"{SubmoduleDisplayName}.ExecuteActivation" },
				{ RepairActivation, $"{SubmoduleDisplayName}.RepairActivation" },
				{ ViewAdmissionRoutes, $"{SubmoduleDisplayName}.ViewAdmissionRoutes" },
				{ ManageAdmissionRoutes, $"{SubmoduleDisplayName}.ManageAdmissionRoutes" },
				{ ApproveAdmissionRoutes, $"{SubmoduleDisplayName}.ApproveAdmissionRoutes" },
				{ AssignAdmissionRoute, $"{SubmoduleDisplayName}.AssignAdmissionRoute" },
				{ EvaluateAdmissionRoute, $"{SubmoduleDisplayName}.EvaluateAdmissionRoute" },
				{ ManageRouteAppeals, $"{SubmoduleDisplayName}.ManageRouteAppeals" },
				{ ApproveRouteAppeals, $"{SubmoduleDisplayName}.ApproveRouteAppeals" },
				{ ViewDataQuality, $"{SubmoduleDisplayName}.ViewDataQuality" },
				{ RunReconciliation, $"{SubmoduleDisplayName}.RunReconciliation" },
				{ ResolveDataQuality, $"{SubmoduleDisplayName}.ResolveDataQuality" },
				{ AssignManualIdentifier, $"{SubmoduleDisplayName}.AssignManualIdentifier" },
				{ ManageServiceHandoffs, $"{SubmoduleDisplayName}.ManageServiceHandoffs" },
				{ BulkOperations, $"{SubmoduleDisplayName}.BulkOperations" },
				{ ManageOperationsQueues, $"{SubmoduleDisplayName}.ManageOperationsQueues" },
				{ ManageDelegation, $"{SubmoduleDisplayName}.ManageDelegation" },
				{ ViewAudit, $"{SubmoduleDisplayName}.ViewAudit" },
				{ ManageRetention, $"{SubmoduleDisplayName}.ManageRetention" },
				{ SensitiveExport, $"{SubmoduleDisplayName}.SensitiveExport" },
				{ ProtectedAnalytics, $"{SubmoduleDisplayName}.ProtectedAnalytics" },
				{ ManageScheduledReports, $"{SubmoduleDisplayName}.ManageScheduledReports" }
			};
		}

		public static class IdentityCompliance
		{
			private const string SubmoduleName = $"{ModuleName}.IdentityCompliance";
			private const string SubmoduleDisplayName = $"{DisplayName}.IdentityCompliance";

			public const string View = $"{SubmoduleName}.View";
			public const string ManageSensitive = $"{SubmoduleName}.ManageSensitive";

			public static readonly string[] All = { View, ManageSensitive };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ ManageSensitive, $"{SubmoduleDisplayName}.ManageSensitive" }
			};
		}
	}
}
