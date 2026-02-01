using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using ShikshaMitra.Application.Contracts.Groups;
using ShikshaMitra.Groups;

namespace ShikshaMitra.Application.Groups;

/// <summary>
/// Helper class for building dynamic LINQ queries from rule definitions
/// </summary>
public static class RuleQueryBuilder
{
    /// <summary>
    /// Applies student rules to a queryable
    /// </summary>
    public static IQueryable<T> ApplyStudentRules<T>(
        IQueryable<T> query,
        List<StudentRuleDto> rules,
        Func<StudentRuleField, string> fieldMapper)
    {
        foreach (var rule in rules)
        {
            var propertyPath = fieldMapper(rule.Field);
            if (string.IsNullOrEmpty(propertyPath))
                continue;

            var predicate = BuildPredicate<T>(propertyPath, rule.Operator, rule.Value);
            if (predicate != null)
            {
                query = query.Where(predicate);
            }
        }

        return query;
    }

    /// <summary>
    /// Applies employee rules to a queryable
    /// </summary>
    public static IQueryable<T> ApplyEmployeeRules<T>(
        IQueryable<T> query,
        List<EmployeeRuleDto> rules,
        Func<EmployeeRuleField, string> fieldMapper)
    {
        foreach (var rule in rules)
        {
            var propertyPath = fieldMapper(rule.Field);
            if (string.IsNullOrEmpty(propertyPath))
                continue;

            var predicate = BuildPredicate<T>(propertyPath, rule.Operator, rule.Value);
            if (predicate != null)
            {
                query = query.Where(predicate);
            }
        }

        return query;
    }

    /// <summary>
    /// Applies generic filter rules to a queryable
    /// </summary>
    public static IQueryable<T> ApplyFilters<T>(
        IQueryable<T> query,
        List<UserSelectionFilterDto> filters,
        Func<int, string> fieldMapper)
    {
        foreach (var filter in filters)
        {
            var propertyPath = fieldMapper(filter.Field);
            if (string.IsNullOrEmpty(propertyPath))
                continue;

            var predicate = BuildPredicate<T>(propertyPath, filter.Operator, filter.Value);
            if (predicate != null)
            {
                query = query.Where(predicate);
            }
        }

        return query;
    }

    /// <summary>
    /// Builds a predicate expression for a given property, operator, and value
    /// </summary>
    private static Expression<Func<T, bool>>? BuildPredicate<T>(
        string propertyPath,
        RuleOperator ruleOperator,
        string value)
    {
        try
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyExpression = BuildPropertyExpression(parameter, propertyPath);

            if (propertyExpression == null)
                return null;

            var propertyType = propertyExpression.Type;
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            // Handle null check for nullable types
            Expression? nullCheck = null;
            if (propertyType != underlyingType)
            {
                nullCheck = Expression.NotEqual(propertyExpression, Expression.Constant(null, propertyType));
            }

            Expression comparison;

            switch (ruleOperator)
            {
                case RuleOperator.Equals:
                    comparison = BuildEqualsExpression(propertyExpression, value, underlyingType);
                    break;

                case RuleOperator.NotEquals:
                    comparison = Expression.Not(BuildEqualsExpression(propertyExpression, value, underlyingType));
                    break;

                case RuleOperator.Contains:
                    comparison = BuildStringMethodExpression(propertyExpression, "Contains", value, underlyingType);
                    break;

                case RuleOperator.StartsWith:
                    comparison = BuildStringMethodExpression(propertyExpression, "StartsWith", value, underlyingType);
                    break;

                case RuleOperator.EndsWith:
                    comparison = BuildStringMethodExpression(propertyExpression, "EndsWith", value, underlyingType);
                    break;

                case RuleOperator.GreaterThan:
                    comparison = BuildComparisonExpression(propertyExpression, value, underlyingType, ExpressionType.GreaterThan);
                    break;

                case RuleOperator.LessThan:
                    comparison = BuildComparisonExpression(propertyExpression, value, underlyingType, ExpressionType.LessThan);
                    break;

                case RuleOperator.GreaterOrEqual:
                    comparison = BuildComparisonExpression(propertyExpression, value, underlyingType, ExpressionType.GreaterThanOrEqual);
                    break;

                case RuleOperator.LessOrEqual:
                    comparison = BuildComparisonExpression(propertyExpression, value, underlyingType, ExpressionType.LessThanOrEqual);
                    break;

                case RuleOperator.In:
                    comparison = BuildInExpression(propertyExpression, value, underlyingType);
                    break;

                default:
                    return null;
            }

            // Combine null check with comparison for nullable types
            if (nullCheck != null)
            {
                comparison = Expression.AndAlso(nullCheck, comparison);
            }

            return Expression.Lambda<Func<T, bool>>(comparison, parameter);
        }
        catch
        {
            // If we can't build the expression, return null and skip this rule
            return null;
        }
    }

    /// <summary>
    /// Builds a property access expression that supports nested properties (e.g., "AdditionalDetail.Gender")
    /// </summary>
    private static Expression? BuildPropertyExpression(Expression parameter, string propertyPath)
    {
        var parts = propertyPath.Split('.');
        Expression current = parameter;

        foreach (var part in parts)
        {
            var property = current.Type.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null)
                return null;

            current = Expression.Property(current, property);
        }

        return current;
    }

    /// <summary>
    /// Builds an equals expression handling type conversion
    /// </summary>
    private static Expression BuildEqualsExpression(Expression property, string value, Type targetType)
    {
        var convertedValue = ConvertValue(value, targetType);
        var constant = Expression.Constant(convertedValue, property.Type);
        return Expression.Equal(property, constant);
    }

    /// <summary>
    /// Builds a string method call expression (Contains, StartsWith, EndsWith)
    /// </summary>
    private static Expression BuildStringMethodExpression(Expression property, string methodName, string value, Type targetType)
    {
        // For string methods, we need to convert the property to string if it's not already
        Expression stringProperty = property;

        if (targetType != typeof(string))
        {
            // For non-string types, call ToString()
            var toStringMethod = targetType.GetMethod("ToString", Type.EmptyTypes);
            if (toStringMethod != null)
            {
                stringProperty = Expression.Call(property, toStringMethod);
            }
            else
            {
                // Fallback: just use string comparison
                var convertedProperty = Expression.Convert(property, typeof(object));
                var objectToString = typeof(object).GetMethod("ToString");
                stringProperty = Expression.Call(convertedProperty, objectToString!);
            }
        }

        var method = typeof(string).GetMethod(methodName, new[] { typeof(string) });
        var constant = Expression.Constant(value, typeof(string));

        // Handle potential null strings
        var nullCheck = Expression.NotEqual(stringProperty, Expression.Constant(null, typeof(string)));
        var methodCall = Expression.Call(stringProperty, method!, constant);

        return Expression.AndAlso(nullCheck, methodCall);
    }

    /// <summary>
    /// Builds a comparison expression (>, <, >=, <=)
    /// </summary>
    private static Expression BuildComparisonExpression(Expression property, string value, Type targetType, ExpressionType comparisonType)
    {
        var convertedValue = ConvertValue(value, targetType);
        var constant = Expression.Constant(convertedValue, property.Type);

        return comparisonType switch
        {
            ExpressionType.GreaterThan => Expression.GreaterThan(property, constant),
            ExpressionType.LessThan => Expression.LessThan(property, constant),
            ExpressionType.GreaterThanOrEqual => Expression.GreaterThanOrEqual(property, constant),
            ExpressionType.LessThanOrEqual => Expression.LessThanOrEqual(property, constant),
            _ => Expression.Equal(property, constant)
        };
    }

    /// <summary>
    /// Builds an "In" expression (value is in a comma-separated list)
    /// </summary>
    private static Expression BuildInExpression(Expression property, string value, Type targetType)
    {
        var values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var convertedValues = values.Select(v => ConvertValue(v, targetType)).ToList();

        // Build: values.Contains(property)
        var listType = typeof(List<>).MakeGenericType(targetType);
        var list = Activator.CreateInstance(listType);
        var addMethod = listType.GetMethod("Add");

        foreach (var cv in convertedValues)
        {
            addMethod!.Invoke(list, new[] { cv });
        }

        var containsMethod = listType.GetMethod("Contains", new[] { targetType });
        var constant = Expression.Constant(list, listType);

        // For nullable types, we need to access the value
        var valueExpression = property;
        if (Nullable.GetUnderlyingType(property.Type) != null)
        {
            var valueProperty = property.Type.GetProperty("Value");
            valueExpression = Expression.Property(property, valueProperty!);
        }

        return Expression.Call(constant, containsMethod!, valueExpression);
    }

    /// <summary>
    /// Converts a string value to the target type
    /// </summary>
    private static object? ConvertValue(string value, Type targetType)
    {
        if (targetType == typeof(string))
            return value;

        if (targetType == typeof(int))
            return int.Parse(value);

        if (targetType == typeof(long))
            return long.Parse(value);

        if (targetType == typeof(decimal))
            return decimal.Parse(value);

        if (targetType == typeof(double))
            return double.Parse(value);

        if (targetType == typeof(float))
            return float.Parse(value);

        if (targetType == typeof(bool))
            return bool.Parse(value);

        if (targetType == typeof(DateTime))
            return DateTime.Parse(value);

        if (targetType == typeof(DateTimeOffset))
            return DateTimeOffset.Parse(value);

        if (targetType == typeof(DateOnly))
            return DateOnly.Parse(value);

        if (targetType == typeof(Guid))
            return Guid.Parse(value);

        if (targetType.IsEnum)
        {
            // Try to parse as enum value or name
            if (int.TryParse(value, out var intValue))
                return Enum.ToObject(targetType, intValue);
            return Enum.Parse(targetType, value, ignoreCase: true);
        }

        // Fallback to Convert.ChangeType
        return Convert.ChangeType(value, targetType);
    }
}
