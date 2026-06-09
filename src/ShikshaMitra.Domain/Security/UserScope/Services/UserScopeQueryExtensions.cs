using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Security.UserScope;

public static class UserScopeQueryExtensions
{
	public static IQueryable<TEntity> ApplyUserScope<TEntity>(
		this IQueryable<TEntity> queryable,
		UserScopeContext userScope)
		where TEntity : IUserScopedEntity
	{
		if (userScope.IsAdmin)
		{
			return queryable;
		}

		if (!userScope.HasAnyAccess)
		{
			return queryable.Where(_ => false);
		}

		var baseFilter = queryable.Where(e =>
			userScope.HasWildcardAcademicYear ||
			(e.AcademicYearId.HasValue &&
			 userScope.WildcardBoardAcademicYearIds.Contains(e.AcademicYearId.Value)) ||
			(e.DivisionId.HasValue && (
				(userScope.HasWildcardDivision &&
				 (
					(e.StandardId.HasValue &&
					 (userScope.StandardIds.Contains(e.StandardId.Value) ||
					  userScope.WildcardDivisionStandardIds.Contains(e.StandardId.Value))) ||
					(userScope.HasWildcardStandard &&
					 e.EducationMediumStageId.HasValue &&
					 userScope.WildcardStandardMediumStageIds.Contains(e.EducationMediumStageId.Value)) ||
					(userScope.HasWildcardMediumStage &&
					 e.EducationMediumId.HasValue &&
					 userScope.WildcardMediumStageMediumIds.Contains(e.EducationMediumId.Value)) ||
					(userScope.HasWildcardMedium &&
					 e.EducationBoardId.HasValue &&
					 userScope.WildcardMediumBoardIds.Contains(e.EducationBoardId.Value)) ||
					userScope.HasWildcardAcademicYear ||
					(e.AcademicYearId.HasValue &&
					 userScope.WildcardBoardAcademicYearIds.Contains(e.AcademicYearId.Value))
				 )) ||
				userScope.DivisionIds.Contains(e.DivisionId.Value)
			)) ||
			(e.StandardId.HasValue && (
				(userScope.HasWildcardStandard &&
				 (
					(e.EducationMediumStageId.HasValue &&
					 (userScope.MediumStageIds.Contains(e.EducationMediumStageId.Value) ||
					  userScope.WildcardStandardMediumStageIds.Contains(e.EducationMediumStageId.Value))) ||
					(userScope.HasWildcardMediumStage &&
					 e.EducationMediumId.HasValue &&
					 userScope.WildcardMediumStageMediumIds.Contains(e.EducationMediumId.Value)) ||
					(userScope.HasWildcardMedium &&
					 e.EducationBoardId.HasValue &&
					 userScope.WildcardMediumBoardIds.Contains(e.EducationBoardId.Value)) ||
					userScope.HasWildcardAcademicYear ||
					(e.AcademicYearId.HasValue &&
					 userScope.WildcardBoardAcademicYearIds.Contains(e.AcademicYearId.Value))
				 )) ||
				userScope.StandardIds.Contains(e.StandardId.Value)
			)) ||
			(e.EducationMediumStageId.HasValue && (
				(userScope.HasWildcardMediumStage &&
				 (
					(e.EducationMediumId.HasValue &&
					 (userScope.MediumIds.Contains(e.EducationMediumId.Value) ||
					  userScope.WildcardMediumStageMediumIds.Contains(e.EducationMediumId.Value))) ||
					(userScope.HasWildcardMedium &&
					 e.EducationBoardId.HasValue &&
					 userScope.WildcardMediumBoardIds.Contains(e.EducationBoardId.Value)) ||
					userScope.HasWildcardAcademicYear ||
					(e.AcademicYearId.HasValue &&
					 userScope.WildcardBoardAcademicYearIds.Contains(e.AcademicYearId.Value))
				 )) ||
				userScope.MediumStageIds.Contains(e.EducationMediumStageId.Value)
			)) ||
			(e.EducationMediumId.HasValue && (
				(userScope.HasWildcardMedium &&
				 (
					(e.EducationBoardId.HasValue &&
					 userScope.WildcardMediumBoardIds.Contains(e.EducationBoardId.Value)) ||
					userScope.HasWildcardAcademicYear ||
					(e.AcademicYearId.HasValue &&
					 userScope.WildcardBoardAcademicYearIds.Contains(e.AcademicYearId.Value))
				 )) ||
				userScope.MediumIds.Contains(e.EducationMediumId.Value)
			)) ||
			(e.EducationBoardId.HasValue && (
				userScope.HasWildcardAcademicYear ||
				(e.AcademicYearId.HasValue &&
				 userScope.WildcardBoardAcademicYearIds.Contains(e.AcademicYearId.Value)) ||
				userScope.BoardIds.Contains(e.EducationBoardId.Value)
			))
		);

		if (userScope.HasWildcardAcademicYear)
		{
			return baseFilter;
		}

		if (userScope.AcademicYearIds.Any())
		{
			return baseFilter.Where(e =>
				!e.AcademicYearId.HasValue ||
				userScope.AcademicYearIds.Contains(e.AcademicYearId.Value));
		}

		return baseFilter.Where(e => !e.AcademicYearId.HasValue);
	}

	public static bool HasAccessToEntity<TEntity>(
		TEntity entity,
		UserScopeContext userScope)
		where TEntity : IUserScopedEntity
	{
		if (userScope.IsAdmin)
		{
			return true;
		}

		if (entity.AcademicYearId.HasValue &&
		    !userScope.HasWildcardAcademicYear &&
		    !userScope.AcademicYearIds.Contains(entity.AcademicYearId.Value))
		{
			return false;
		}

		if (HasBoardWideAccess(entity, userScope))
		{
			return true;
		}

		if (entity.DivisionId.HasValue && (
			(userScope.HasWildcardDivision &&
			 (
				(entity.StandardId.HasValue &&
				 (userScope.StandardIds.Contains(entity.StandardId.Value) ||
				  userScope.WildcardDivisionStandardIds.Contains(entity.StandardId.Value))) ||
				(userScope.HasWildcardStandard &&
				 entity.EducationMediumStageId.HasValue &&
				 userScope.WildcardStandardMediumStageIds.Contains(entity.EducationMediumStageId.Value)) ||
				(userScope.HasWildcardMediumStage &&
				 entity.EducationMediumId.HasValue &&
				 userScope.WildcardMediumStageMediumIds.Contains(entity.EducationMediumId.Value)) ||
				(userScope.HasWildcardMedium &&
				 entity.EducationBoardId.HasValue &&
				 userScope.WildcardMediumBoardIds.Contains(entity.EducationBoardId.Value)) ||
				HasBoardWideAccess(entity, userScope)
			 )) ||
			userScope.DivisionIds.Contains(entity.DivisionId.Value)))
		{
			return true;
		}

		if (entity.StandardId.HasValue && (
			(userScope.HasWildcardStandard &&
			 (
				(entity.EducationMediumStageId.HasValue &&
				 (userScope.MediumStageIds.Contains(entity.EducationMediumStageId.Value) ||
				  userScope.WildcardStandardMediumStageIds.Contains(entity.EducationMediumStageId.Value))) ||
				(userScope.HasWildcardMediumStage &&
				 entity.EducationMediumId.HasValue &&
				 userScope.WildcardMediumStageMediumIds.Contains(entity.EducationMediumId.Value)) ||
				(userScope.HasWildcardMedium &&
				 entity.EducationBoardId.HasValue &&
				 userScope.WildcardMediumBoardIds.Contains(entity.EducationBoardId.Value)) ||
				HasBoardWideAccess(entity, userScope)
			 )) ||
			userScope.StandardIds.Contains(entity.StandardId.Value)))
		{
			return true;
		}

		if (entity.EducationMediumStageId.HasValue && (
			(userScope.HasWildcardMediumStage &&
			 (
				(entity.EducationMediumId.HasValue &&
				 (userScope.MediumIds.Contains(entity.EducationMediumId.Value) ||
				  userScope.WildcardMediumStageMediumIds.Contains(entity.EducationMediumId.Value))) ||
				(userScope.HasWildcardMedium &&
				 entity.EducationBoardId.HasValue &&
				 userScope.WildcardMediumBoardIds.Contains(entity.EducationBoardId.Value)) ||
				HasBoardWideAccess(entity, userScope)
			 )) ||
			userScope.MediumStageIds.Contains(entity.EducationMediumStageId.Value)))
		{
			return true;
		}

		if (entity.EducationMediumId.HasValue && (
			(userScope.HasWildcardMedium &&
			 (
				(entity.EducationBoardId.HasValue &&
				 userScope.WildcardMediumBoardIds.Contains(entity.EducationBoardId.Value)) ||
				HasBoardWideAccess(entity, userScope)
			 )) ||
			userScope.MediumIds.Contains(entity.EducationMediumId.Value)))
		{
			return true;
		}

		if (entity.EducationBoardId.HasValue && (
			HasBoardWideAccess(entity, userScope) ||
			userScope.BoardIds.Contains(entity.EducationBoardId.Value)))
		{
			return true;
		}

		return false;
	}

	public static IEnumerable<TEntity> FilterByUserScope<TEntity>(
		this IEnumerable<TEntity> entities,
		UserScopeContext userScope)
		where TEntity : IUserScopedEntity
	{
		return userScope.IsAdmin
			? entities
			: entities.Where(entity => HasAccessToEntity(entity, userScope));
	}

	private static bool HasBoardWideAccess<TEntity>(TEntity entity, UserScopeContext userScope)
		where TEntity : IUserScopedEntity
	{
		if (userScope.HasWildcardAcademicYear)
		{
			return true;
		}

		return entity.AcademicYearId.HasValue &&
			userScope.WildcardBoardAcademicYearIds.Contains(entity.AcademicYearId.Value);
	}
}
