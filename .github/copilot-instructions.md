# FinAI Copilot Instructions

These instructions guide GitHub Copilot when generating, modifying, or reviewing code in the FinAI repository. FinAI is a small AI-powered personal finance assistant built to demonstrate an AI-Driven Development Lifecycle (AI-DLC). Prefer simple, readable, verifiable solutions over clever or large ones.

## 1. Coding Standards

- Target .NET 8 and C# 12 features only where they improve clarity.
- Enable and respect nullable reference types. Do not suppress warnings with `!` or `#pragma` without a justifying comment.
- Use `async`/`await` for all I/O (database, HTTP, file). Do not block with `.Result` or `.Wait()`. Suffix async methods with `Async` and accept a `CancellationToken` on public async APIs.
- Use `record` types or immutable DTOs for data transfer. Use `sealed` on classes that are not designed for inheritance.
- Use dependency injection. Do not use static service locators or static mutable state.
- Follow standard .NET naming conventions: PascalCase for public members, camelCase for locals and parameters, `_camelCase` for private fields.
- Keep methods short and single-purpose. Prefer early returns over deep nesting.
- Do not leave commented-out code, `TODO`s without an issue reference, or unused usings.
- Format code with `dotnet format` and keep the repository's `.editorconfig` rules.

## 2. Project Architecture Principles

- Use a layered structure:
  - `FinAI.Api`: ASP.NET Core Web API (controllers or minimal endpoints, request/response DTOs, middleware, DI setup).
  - `FinAI.Application`: use cases, services, validation, and interfaces (for example `ITransactionService`, `ICategorizationService`).
  - `FinAI.Domain`: entities, value objects, enums, and domain rules. No dependencies on infrastructure or frameworks.
  - `FinAI.Infrastructure`: EF Core `DbContext`, repositories, migrations, Gemini client implementation.
  - `FinAI.Web`: simple static or lightweight web UI that calls the API only.
  - `FinAI.Tests.Unit` and `FinAI.Tests.Integration`: xUnit test projects.
- Dependencies flow inward: Api and Infrastructure depend on Application; Application depends on Domain. Domain depends on nothing.
- Depend on interfaces defined in Application for external services (database, AI provider, clock, etc.). Infrastructure provides implementations.
- Inject `TimeProvider` (or an `IClock` abstraction) instead of calling `DateTime.UtcNow` directly, so behavior is testable.
- Do not put business logic in controllers, endpoints, UI code, or EF Core configuration classes.
- Do not introduce new projects, layers, or frameworks without a documented reason and human approval.

## 3. Security Requirements

- Never hard-code secrets, API keys, connection strings with passwords, or tokens in source code, tests, sample files, or documentation.
- Validate all external input at the API boundary: request sizes, required fields, string lengths, amount ranges, date ranges, and enum values.
- Use parameterized queries only. Never build SQL by string concatenation or interpolation. Prefer EF Core LINQ.
- Return only the data the caller is allowed to see. Even though the app starts as single-user, design data access so that a `UserId` scope can be added without rewriting queries.
- Do not expose stack traces, EF Core exception details, or internal type names in API responses.
- Use HTTPS redirection and secure defaults. Restrict CORS to explicitly configured origins.
- Do not log personal financial details (full transaction descriptions with account data, amounts tied to identities) beyond what is needed for diagnostics.
- Do not disable certificate validation, TLS checks, or security analyzers.
- Pin and review NuGet package additions. Prefer well-known, maintained packages.

## 4. AI Integration Rules

- Access Gemini only through an interface in `FinAI.Application` (for example `IAiCategorizationClient`) with an implementation in `FinAI.Infrastructure`. Never call the Gemini HTTP API from controllers, UI code, or domain code.
- Read the Gemini API key from configuration (user secrets locally, environment variables or secret store in CI and deployment). Never commit it.
- Treat AI output as untrusted. Parse responses into strongly typed results and validate them against allowed categories. If the output is invalid or the call fails, fall back to a default category such as `Uncategorized` and log the failure.
- Keep prompts in a dedicated, versioned location (for example constants or embedded text files in Infrastructure). Do not scatter prompt strings through the codebase.
- Send the minimum data needed to the AI provider. Strip identifiers and unnecessary personal details from prompts.
- Apply timeouts, retry limits (for example at most 2 retries with backoff), and respect free-tier rate limits. Use a cache for repeated categorization of identical descriptions where appropriate.
- Make AI features optional: the application must remain functional for manual categorization if the AI service is unavailable.
- Do not let AI-generated text bypass validation to write to the database or change financial totals directly. Financial calculations must be deterministic code, not AI output.
- Do not hard-code model names. Read them from configuration.

## 5. Testing Expectations

- Use xUnit for all tests and FluentAssertions for assertions.
- Every new domain rule and application service method must have unit tests covering the success path, validation failures, and edge cases (zero, negative, boundary dates, empty input).
- Use fakes or hand-written test doubles for `IAiCategorizationClient`, clock, and repositories in unit tests. Do not call the real Gemini API from tests.
- Integration tests should target a locally available SQL Server instance. Do not introduce Testcontainers or Docker for this project.
- - Test names should describe behavior, for example `Method_Scenario_ExpectedResult`.
- Do not delete or weaken existing tests to make a change pass. Explain any test change in the pull request.
- Bug fixes must include a test that fails before the fix and passes after.

## 6. Database Practices

- Use EF Core with code-first migrations. Every schema change must be a migration generated with `dotnet ef migrations add <DescriptiveName>`.
- Do not edit or delete applied migrations. Create a new migration to correct issues.
- Configure entities with Fluent API in separate `IEntityTypeConfiguration<T>` classes, not data annotations, unless trivial.
- Use `decimal` (with explicit precision, for example `decimal(18,2)`) for money. Never use `double` or `float` for currency.
- Store dates and times in UTC. Use `DateOnly` for transaction dates where time is not relevant.
- Add indexes for columns used in filtering and dashboards (for example transaction date and category).
- Use `AsNoTracking()` for read-only queries. Avoid N+1 queries; use projections for dashboard and summary endpoints.
- Do not run destructive operations (`DROP`, `TRUNCATE`, bulk `DELETE`) in application code or migrations without explicit human approval.
- Do not hard-code connection strings. Use configuration keys such as `ConnectionStrings:FinAI`.

## 7. API Development Practices

- Use RESTful, resource-oriented routes under `/api/v1/...` (for example `/api/v1/transactions`, `/api/v1/insights/monthly`).
- Use DTOs for requests and responses. Never expose EF Core entities directly.
- Use proper HTTP status codes: `200`, `201` with a `Location` header on creation, `204` for deletes, `400` for validation errors, `404` for missing resources, `409` for conflicts, `500` only for unexpected failures.
- Use `ProblemDetails` (RFC 7807) for all error responses.
- Support pagination for list endpoints (`page`, `pageSize` with a maximum page size).
- Document endpoints with OpenAPI (Swagger) annotations or XML comments.
- Version the API from the start. Make breaking changes only in a new version.
- Validate requests with FluentValidation or data annotations at the boundary, and keep domain invariants enforced in the domain layer.

## 8. Error Handling and Logging

- Use exceptions only for exceptional conditions. Expected failures (validation, not found, AI unavailable) should use result types or explicit handling.
- Use a global exception handler middleware that maps exceptions to `ProblemDetails` and logs them once.
- Use `ILogger<T>` with structured logging message templates, for example `_logger.LogInformation("Categorized {TransactionCount} transactions", count)`. Do not use string interpolation in log messages.
- Use appropriate log levels: `Debug` for detail, `Information` for significant lifecycle events, `Warning` for recoverable issues (AI fallback), `Error` for failures requiring attention.
- Never log secrets, API keys, full prompts containing personal data, or full request bodies.
- Include a correlation or request identifier in logs and error responses where practical.
- Catch specific exceptions. Do not swallow exceptions silently with empty `catch` blocks.
- Propagate `CancellationToken` and handle `OperationCanceledException` without logging it as an error.

## 9. Secrets Management

- Local development: use `dotnet user-secrets` for the Gemini API key and SQL Server connection string.
- CI (GitHub Actions): store secrets in GitHub repository or environment secrets and reference them as `${{ secrets.NAME }}`. Never echo them in logs.
- Deployment: use environment variables or a managed secret store. Do not place secrets in `appsettings.json`, only placeholders or non-sensitive defaults.
- Commit only `appsettings.json` and `appsettings.Development.json` with no real secrets. Ensure `.gitignore` excludes local settings files that may contain secrets.
- If a secret is accidentally exposed, stop, do not reproduce it in output, and alert the human to rotate it.

## 10. Documentation Expectations

- Each project should have a short `README.md` covering purpose, how to run, and how to test.
- The root `README.md` must include setup steps: prerequisites (.NET 8 SDK, SQL Server, Gemini API key), configuration, running migrations, running the API and UI, and running tests.
- Document architectural decisions in `docs/adr/` using short Architecture Decision Records (context, decision, consequences).
- Public APIs, interfaces in Application, and non-obvious logic should have XML documentation comments. Keep them concise and accurate.
- Update documentation in the same change when behavior, configuration, or endpoints change.
- Do not write comments that restate the code. Comments should explain why, not what.

## 11. Rules for Making Minimal, Focused Changes

- Change only what the task requires. Do not refactor, rename, reformat, or reorganize unrelated code in the same change.
- Do not modify unrelated files, dependencies, or configuration to solve a local problem.
- Prefer extending existing patterns in the codebase over introducing new ones. Search for existing helpers before creating new ones.
- Do not add new NuGet packages unless the existing libraries cannot reasonably solve the problem. State the reason when adding one.
- Keep each change set to a single concern. Split large requests into smaller, reviewable steps.
- Build and run tests after changes. Do not finish with compilation errors or failing tests that you introduced.
- Summarize what changed, why, and how it was verified.

-Authentication is required for the MVP. Use ASP.NET Core Identity with SQL Server. All expense, dashboard, AI insight, and financial data endpoints must require authentication and enforce user-level data isolation. Keep authentication limited to registration, login, logout, and secure password management. Social login, MFA, roles, and advanced identity features are out of scope unless time permits.

## 12. Human Review Required

Copilot must not finalize or merge changes that involve the following without explicit human review and approval. Propose options and tradeoffs instead of deciding unilaterally.

- Architectural decisions: new projects, new layers, changing dependency direction, switching frameworks, database providers, or AI providers.
- Security decisions: authentication and authorization design, CORS policy changes, encryption choices, secret handling changes, data retention, and logging of personal data.
- Data decisions: destructive migrations, changes to money types or precision, changes to stored personal data, and data deletion behavior.
- AI decisions: changes to prompts that alter categorization or insight outputs, changes to data sent to Gemini, and changes to fallback behavior.
- Dependency decisions: adding or upgrading packages with security or licensing implications.
- CI/CD changes: workflow permission changes, new secrets, or deployment steps.

When uncertain, ask for clarification rather than assuming intent.
