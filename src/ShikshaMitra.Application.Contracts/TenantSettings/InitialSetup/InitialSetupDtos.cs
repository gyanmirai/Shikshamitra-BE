using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Collections.Generic;
using Volo.Abp.Identity;
using ShikshaMitra.HostTenants;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;

namespace ShikshaMitra.TenantSettings.InitialSetup;

public class InitialSetupProfileDraft
{
	[StringLength(256)] public string InstituteName { get; set; } = "";
	[StringLength(256)] public string Email { get; set; } = "";
	[StringLength(32)] public string ContactNumber { get; set; } = "";
	[StringLength(256)] public string Address1 { get; set; } = "";
	[StringLength(256)] public string Address2 { get; set; } = "";
	[StringLength(256)] public string Address3 { get; set; } = "";
	[StringLength(128)] public string City { get; set; } = "";
	[StringLength(128)] public string State { get; set; } = "";
	[StringLength(128)] public string Country { get; set; } = "India";
	[StringLength(20)] public string PostalCode { get; set; } = "";
}
public class InitialSetupDraftDto
{
	[Required] public InitialSetupProfileDraft Profile { get; set; } = new();
	[StringLength(50)] public string YearName { get; set; } = "";
	public Guid? ExistingYearId { get; set; }
	public DateTime? StartDate { get; set; }
	public DateTime? EndDate { get; set; }
	public EducationBoardType? BoardType { get; set; }
	[StringLength(EducationBoardNames.MaxCustomNameLength)] public string CustomBoardName { get; set; } = "";
	[StringLength(128)] public string Medium { get; set; } = "";
	[StringLength(128)] public string Stage { get; set; } = "";
	[StringLength(128)] public string ClassName { get; set; } = "";
	public int? AgeLowerLimit { get; set; }
	[StringLength(64)] public string DivisionName { get; set; } = "";
	public int? Strength { get; set; }
}
public class InitialSetupDto
{
	public bool IsCompleted { get; set; }
	public bool CanManageSetup { get; set; }
	public int CurrentStep { get; set; }
	public int FurthestStep { get; set; }
	public string SchoolCode { get; set; } = "";
	public string ConcurrencyStamp { get; set; } = "";
	public Guid? AcademicYearId { get; set; }
	public Guid? DivisionId { get; set; }
	public DateTime? CompletedAt { get; set; }
	public InitialSetupDraftDto Draft { get; set; } = new();
	public List<SetupUserSummary> CreatedUsers { get; set; } = new();
	public int[] PendingSteps { get; set; } = Array.Empty<int>();
}
public class SetupUserSummary
{
	public Guid RequestId { get; set; }
	public Guid Id { get; set; }
	public string UserName { get; set; } = "";
	public string Email { get; set; } = "";
}
public class CreateSetupUserInput : IdentityUserCreateDto
{
	public Guid RequestId { get; set; }
	[Required, StringLength(64)] public string ConcurrencyStamp { get; set; } = "";
}
public class SetupConcurrencyInput
{
	[Required, StringLength(64)] public string ConcurrencyStamp { get; set; } = "";
}
public class SaveInitialSetupDraftInput : SetupConcurrencyInput
{
	[Range(0, 5)] public int CurrentStep { get; set; }
	[Required] public InitialSetupDraftDto Draft { get; set; } = new();
}
public class SaveSetupProfileInput : HostTenantDetailsInput
{
	[Required, StringLength(64)] public string ConcurrencyStamp { get; set; } = "";
}
public class SaveSetupYearInput : SetupConcurrencyInput
{
	public Guid? ExistingYearId { get; set; }
	public bool CreateNew { get; set; }
	[Required, StringLength(50)] public string Name { get; set; } = "";
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
}
public class SaveSetupStructureInput : SetupConcurrencyInput
{
	[Required] public InitialSetupDraftDto Draft { get; set; } = new();
}
public class AdvanceSetupInput : SetupConcurrencyInput
{
	[Range(0, 4)] public int Step { get; set; }
}
public interface IInitialSetupAppService
{
	Task<InitialSetupDto> GetAsync();
	Task<InitialSetupDto> SaveDraftAsync(SaveInitialSetupDraftInput input);
	Task<InitialSetupDto> SaveProfileAsync(SaveSetupProfileInput input);
	Task<InitialSetupDto> SaveYearAsync(SaveSetupYearInput input);
	Task<InitialSetupDto> SaveStructureAsync(SaveSetupStructureInput input);
	Task<InitialSetupDto> AdvanceAsync(AdvanceSetupInput input);
	Task<InitialSetupDto> CompleteAsync(SetupConcurrencyInput input);
	Task<InitialSetupDto> CreateUserAsync(CreateSetupUserInput input);
}
