using AutoMapper;

namespace ShikshaMitra.Library.Policies;

public class LibraryPolicyMappingProfile : Profile
{
	public LibraryPolicyMappingProfile()
	{
		CreateMap<LibraryBorrowingTerms, LibraryBorrowingTermsDto>();
		CreateMap<LibraryBorrowingTermsDto, LibraryBorrowingTerms>().ConstructUsing(x => new(x.MaxBooksAtTime,
			x.ReservationLimit, x.LoanDays, (LibraryDayCountingMode)x.DayCountingMode, x.MaxRenewals,
			x.RenewalDays, x.PickupWindowDays, x.MoveDueDateToNextOpenDay, (LibraryDueDateMode)x.DueDateMode));
		CreateMap<LibraryBorrowingPolicyDraftRequest, LibraryBorrowingPolicyDefinition>().ConstructUsing((x, context) => new(
			x.Name, x.Role, x.EffectiveFrom, x.EffectiveTo, x.BookCategoryId, x.SectionId,
			x.EducationMediumStageId, x.StandardId, x.Priority, context.Mapper.Map<LibraryBorrowingTerms>(x.Terms)));
		CreateMap<LibraryBorrowingPolicyVersion, LibraryBorrowingPolicyVersionDto>();
		CreateMap<LibraryCalendarClosure, LibraryCalendarClosureDto>();
		CreateMap<LibraryBorrowingPolicyDecision, LibraryBorrowingPolicySnapshotDto>().ConvertUsing(x => new()
		{
			PolicyVersionId = x.Policy == null ? null : x.Policy.Id, PolicyId = x.Policy == null ? null : x.Policy.PolicyId,
			Version = x.Policy == null ? null : x.Policy.Version, Role = x.Role, IsLegacyManualDate = x.IsLegacyManualDate,
			EffectiveFrom = x.Policy == null ? null : x.Policy.EffectiveFrom,
			EffectiveTo = x.Policy == null || x.Policy.LastEffectiveDate == System.DateOnly.MaxValue ? null : x.Policy.LastEffectiveDate,
			Terms = new()
			{
				MaxBooksAtTime = x.MaxBooksAtTime, ReservationLimit = x.ReservationLimit,
				DueDateMode = x.Policy == null ? null : (int)x.Policy.Terms.DueDateMode,
				LoanDays = x.Policy == null || x.Policy.Terms.DueDateMode == LibraryDueDateMode.Manual ? null : x.Policy.Terms.LoanDays,
				DayCountingMode = x.Policy == null || x.Policy.Terms.DueDateMode == LibraryDueDateMode.Manual ? null : (int)x.Policy.Terms.DayCountingMode,
				MaxRenewals = x.Policy == null ? null : x.Policy.Terms.MaxRenewals,
				RenewalDays = x.Policy == null || x.Policy.Terms.DueDateMode == LibraryDueDateMode.Manual ? null : x.Policy.Terms.RenewalDays,
				PickupWindowDays = x.Policy == null || x.Policy.Terms.DueDateMode == LibraryDueDateMode.Manual ? null : x.Policy.Terms.PickupWindowDays,
				MoveDueDateToNextOpenDay = x.Policy == null || x.Policy.Terms.DueDateMode == LibraryDueDateMode.Manual ? null : x.Policy.Terms.MoveDueDateToNextOpenDay
			}
		});
	}
}
