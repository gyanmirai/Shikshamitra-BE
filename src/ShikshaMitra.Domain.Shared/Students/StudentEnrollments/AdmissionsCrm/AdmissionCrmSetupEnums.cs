namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public enum AdmissionCrmSettingsAccessMode { Hidden = 0, ReadOnly = 1, Editable = 2 }
public enum AdmissionCrmSetupReadiness { NotStarted = 0, NeedsAttention = 1, Ready = 2, Managed = 3, Locked = 4 }
public enum AdmissionCrmCycleStatus { Draft = 0, ReadinessReview = 1, Open = 2, Paused = 3, Closed = 4, Archived = 5 }
public enum AdmissionCrmPlatformPresetStatus { Draft = 0, Published = 1, Retired = 2 }
