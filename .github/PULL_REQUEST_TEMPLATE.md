## Change and evidence

Describe the root cause, scope, related frontend/backend PR, and exact validation results.

## Security review

- [ ] Read [the canonical security standard](../docs/common/security/permission-consistency.md).
- [ ] Reviewed permission, navigation, identity, scope, seeder changes, including incidental changes and reverts.
- [ ] Exact actions, provider eligibility, tenant isolation, and scope enforcement agree across layers.
- [ ] No automatic restoration of revoked grants or overwrite of manual scopes.
- [ ] Allowed/denied, grant round-trip, atomic rollback, and upgrade-seeding regression checks passed.
- [ ] Security Regression checks passed in both repositories where applicable.
- [ ] Browser and performance evidence distinguishes Passed, Failed, and Not run.
- [ ] No sensitive audit exports, credentials, auth state, or generated logs are committed.

Security sign-off requires executable evidence.
Live repairs, deployment, and branch-protection changes require separate authorization.
