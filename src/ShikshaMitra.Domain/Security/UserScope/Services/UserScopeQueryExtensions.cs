using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

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

		// EF Core cannot translate a cast to IUserScopedEntity because interface members
		// are often explicit/computed properties rather than mapped columns. Build the
		// predicate from the entity's real CLR properties instead.
		var parameter = Expression.Parameter(typeof(TEntity), "entity");
		var academicYear = FindMappedGuidProperty<TEntity>(parameter, "AcademicYearId");
		var board = FindMappedGuidProperty<TEntity>(parameter, "EducationBoardId", "BoardId");
		var medium = FindMappedGuidProperty<TEntity>(parameter, "EducationMediumId", "MediumId");
		var mediumStage = FindMappedGuidProperty<TEntity>(parameter, "EducationMediumStageId", "MediumStageId");
		var standard = FindMappedGuidProperty<TEntity>(parameter, "StandardId");
		var division = FindMappedGuidProperty<TEntity>(parameter, "DivisionId");

		Expression predicate = Expression.Constant(userScope.HasWildcardAcademicYear);
		predicate = Or(predicate, Contains(academicYear, userScope.WildcardBoardAcademicYearIds));
		predicate = Or(predicate, Contains(board, userScope.BoardIds));
		predicate = Or(predicate, Contains(medium, userScope.MediumIds));
		predicate = Or(predicate, Contains(mediumStage, userScope.MediumStageIds));
		predicate = Or(predicate, Contains(standard, userScope.StandardIds));
		predicate = Or(predicate, Contains(standard, userScope.WildcardDivisionStandardIds));
		predicate = Or(predicate, Contains(division, userScope.DivisionIds));

		if (userScope.HasWildcardDivision)
		{
			predicate = Or(predicate, Contains(standard, userScope.StandardIds));
			predicate = Or(predicate, Contains(standard, userScope.WildcardDivisionStandardIds));
		}

		if (userScope.HasWildcardStandard)
		{
			predicate = Or(predicate, Contains(mediumStage, userScope.MediumStageIds));
			predicate = Or(predicate, Contains(mediumStage, userScope.WildcardStandardMediumStageIds));
		}

		if (userScope.HasWildcardMediumStage)
		{
			predicate = Or(predicate, Contains(medium, userScope.MediumIds));
			predicate = Or(predicate, Contains(medium, userScope.WildcardMediumStageMediumIds));
		}

		if (userScope.HasWildcardMedium)
		{
			predicate = Or(predicate, Contains(board, userScope.BoardIds));
			predicate = Or(predicate, Contains(board, userScope.WildcardMediumBoardIds));
		}

		if (academicYear != null && !userScope.HasWildcardAcademicYear && userScope.AcademicYearIds.Any())
		{
			predicate = Expression.AndAlso(
				predicate,
				Expression.OrElse(
					IsNull(academicYear),
					Contains(academicYear, userScope.AcademicYearIds)!));
		}

		return queryable.Where(Expression.Lambda<Func<TEntity, bool>>(predicate, parameter));
	}

	private static Expression Or(Expression left, Expression? right) =>
		right == null ? left : Expression.OrElse(left, right);

	private static MemberExpression? FindMappedGuidProperty<TEntity>(
		ParameterExpression parameter,
		params string[] names)
	{
		var property = names
			.Select(name => typeof(TEntity).GetProperty(name, BindingFlags.Instance | BindingFlags.Public))
			.FirstOrDefault(candidate => candidate?.PropertyType is not null &&
				(candidate.PropertyType == typeof(Guid) || candidate.PropertyType == typeof(Guid?)));
		return property == null ? null : Expression.Property(parameter, property);
	}

	private static Expression? Contains(MemberExpression? property, IReadOnlyCollection<Guid> values)
	{
		if (property == null || values.Count == 0)
		{
			return null;
		}

		var contains = typeof(Enumerable)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Single(method => method.Name == nameof(Enumerable.Contains) && method.GetParameters().Length == 2)
			.MakeGenericMethod(typeof(Guid));
		var value = property.Type == typeof(Guid?)
			? Expression.Property(property, nameof(Nullable<Guid>.Value))
			: property;
		var containsCall = Expression.Call(contains, Expression.Constant(values), value);
		return property.Type == typeof(Guid?)
			? Expression.AndAlso(Expression.Property(property, nameof(Nullable<Guid>.HasValue)), containsCall)
			: containsCall;
	}

	private static Expression IsNull(MemberExpression property) =>
		property.Type == typeof(Guid?)
			? Expression.Not(Expression.Property(property, nameof(Nullable<Guid>.HasValue)))
			: Expression.Constant(false);

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
