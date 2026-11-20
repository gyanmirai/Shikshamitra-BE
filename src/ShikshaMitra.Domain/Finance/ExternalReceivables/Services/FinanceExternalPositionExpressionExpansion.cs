using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace ShikshaMitra.Finance.ExternalReceivables;

// Provider queries receive ordinary expression bodies, never compiled delegates
// or client-evaluated invocation nodes. Detail reads use the same definitions.
public static class FinanceExternalPositionExpressionExpansion
{
	private static readonly IReadOnlyDictionary<MethodInfo, LambdaExpression> Expressions =
		new Dictionary<MethodInfo, LambdaExpression>
		{
			[typeof(FinanceExternalBalanceExpressions).GetMethod(nameof(FinanceExternalBalanceExpressions.HasValidStructure))!] = FinanceExternalBalanceExpressions.Structure,
			[typeof(FinanceExternalBalanceExpressions).GetMethod(nameof(FinanceExternalBalanceExpressions.HasStoredRange))!] = FinanceExternalBalanceExpressions.StoredRange,
			[typeof(FinanceExternalBalanceExpressions).GetMethod(nameof(FinanceExternalBalanceExpressions.Waived))!] = FinanceExternalBalanceExpressions.NetWaived,
			[typeof(FinanceExternalBalanceExpressions).GetMethod(nameof(FinanceExternalBalanceExpressions.Available))!] = FinanceExternalBalanceExpressions.Collectible,
			[typeof(FinanceExternalBalanceExpressions).GetMethod(nameof(FinanceExternalBalanceExpressions.Unsettled))!] = FinanceExternalBalanceExpressions.FinancialOutstanding,
			[typeof(FinanceExternalPositionEvidenceRules).GetMethod(nameof(FinanceExternalPositionEvidenceRules.IsMatch))!] = FinanceExternalPositionEvidenceRules.Matches,
			[typeof(FinanceExternalAllocationExpressions).GetMethod(nameof(FinanceExternalAllocationExpressions.MatchesIdentity))!] = FinanceExternalAllocationExpressions.Identity,
			[typeof(FinanceExternalAllocationExpressions).GetMethod(nameof(FinanceExternalAllocationExpressions.HasKnownStatus))!] = FinanceExternalAllocationExpressions.KnownStatus,
			[typeof(FinanceExternalAllocationExpressions).GetMethod(nameof(FinanceExternalAllocationExpressions.AcceptedAmount))!] = FinanceExternalAllocationExpressions.Accepted,
			[typeof(FinanceExternalAllocationExpressions).GetMethod(nameof(FinanceExternalAllocationExpressions.SettledAmount))!] = FinanceExternalAllocationExpressions.Settled,
			[typeof(FinanceExternalAllocationExpressions).GetMethod(nameof(FinanceExternalAllocationExpressions.PendingAmount))!] = FinanceExternalAllocationExpressions.Pending,
			[typeof(FinanceExternalWaiverExpressions).GetMethod(nameof(FinanceExternalWaiverExpressions.Matches))!] = FinanceExternalWaiverExpressions.Valid,
			[typeof(FinanceExternalRefundEvidenceExpressions).GetMethod(nameof(FinanceExternalRefundEvidenceExpressions.IsValid))!] = FinanceExternalRefundEvidenceExpressions.Valid,
			[typeof(FinanceExternalPositionProjection).GetMethod(nameof(FinanceExternalPositionProjection.IsSourceValid))!] = FinanceExternalPositionProjection.SourceValid,
			[typeof(FinanceExternalPositionProjection).GetMethod(nameof(FinanceExternalPositionProjection.IsValid))!] = FinanceExternalPositionProjection.Valid,
			[typeof(FinanceExternalPositionProjection).GetMethod(nameof(FinanceExternalPositionProjection.IsUnposted))!] = FinanceExternalPositionProjection.Unposted
		};

	public static Expression<T> Expand<T>(Expression<T> expression) => (Expression<T>)new ExpansionVisitor().Visit(expression)!;

	private sealed class ExpansionVisitor : ExpressionVisitor
	{
		protected override Expression VisitMethodCall(MethodCallExpression node)
		{
			if (!Expressions.TryGetValue(node.Method, out var lambda)) return base.VisitMethodCall(node);
			var replacements = new Dictionary<ParameterExpression, Expression>();
			for (var index = 0; index < lambda.Parameters.Count; index++) replacements[lambda.Parameters[index]] = node.Arguments[index];
			return Visit(new ParameterVisitor(replacements).Visit(lambda.Body))!;
		}
	}

	private sealed class ParameterVisitor(IReadOnlyDictionary<ParameterExpression, Expression> replacements) : ExpressionVisitor
	{
		protected override Expression VisitParameter(ParameterExpression node) => replacements.TryGetValue(node, out var value) ? value : node;
	}
}
