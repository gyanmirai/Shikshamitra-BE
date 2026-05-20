## ADDED Requirements

### Requirement: ShikshaMitraApiTestBase provides CreateClientAs helper
The base test class SHALL expose a `CreateClientAs(Guid userId)` method that returns an `HttpClient` that authenticates all requests as the given user ID.

#### Scenario: CreateClientAs sets X-Test-UserId header
- **WHEN** `CreateClientAs(someGuid)` is called and the returned client makes a request
- **THEN** the server resolves `ICurrentUser.Id` to `someGuid` for that request (via `X-Test-UserId` header on the `TestAuthHandler` mechanism)
