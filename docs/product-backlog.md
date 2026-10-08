# FinAI Product Backlog

| Item | Value |
|------|-------|
| Sources | `docs/project-charter.md`, `docs/requirements.md` |
| Budget | Approximately 25 hours |
| Complexity | S (about 1-2 h), M (about 2-4 h), L (about 4-6 h) |

## Common Definition of Done

Every story must meet the Definition of Done in `docs/project-charter.md` section 15. In short: acceptance criteria met, tests pass locally and in CI, code follows `.github/copilot-instructions.md`, no secrets committed, documentation updated, and a human has reviewed the change.

Each story below lists only additional DoD items specific to that story.

## Priority Key

- **P0**: Required for the MVP.
- **P1**: Build only if the budget allows. Cut first.

---

## Epic 1: Project Foundation

### FND-01 Scaffold layered solution

- **Priority**: P0
- **User story**: As a developer, I want a layered .NET 8 solution so that business logic, data access, and the API are separated from the start.
- **Acceptance criteria**:
  - AC1: Solution contains `FinAI.Api`, `FinAI.Application`, `FinAI.Domain`, `FinAI.Infrastructure`, `FinAI.Web`, `FinAI.Tests.Unit`, and `FinAI.Tests.Integration`.
  - AC2: Project references follow the dependency rules (Api and Infrastructure reference Application; Application references Domain; Domain references nothing).
  - AC3: `dotnet build` succeeds with nullable reference types enabled.
- **Definition of Done**: Project references verified; `.editorconfig` applied; each project has a short `README.md`.
- **Dependencies**: None.
- **Complexity**: M

### FND-02 Configuration and secret placeholders

- **Priority**: P0
- **User story**: As a developer, I want configuration keys for the database, Gemini key, and model name so that secrets stay out of source control.
- **Acceptance criteria**:
  - AC1: `appsettings.json` contains placeholders only for `ConnectionStrings:FinAI`, `Gemini:ApiKey`, and `Gemini:Model`.
  - AC2: The app reads the Gemini key and model from configuration; neither is hard-coded.
  - AC3: `.gitignore` excludes local settings files that may contain secrets.
- **Definition of Done**: Configuration documented in the README; secret handling checked by a reviewer.
- **Dependencies**: FND-01.
- **Complexity**: S

### FND-03 Database context and initial migration

- **Priority**: P0
- **User story**: As a developer, I want an EF Core context and an initial migration so that transactions can be stored in SQL Server.
- **Acceptance criteria**:
  - AC1: `DbContext` is in `FinAI.Infrastructure` and configures the transaction entity with Fluent API in an `IEntityTypeConfiguration<T>` class.
  - AC2: `dotnet ef migrations add InitialCreate` produces a migration that creates the table.
  - AC3: Amount is `decimal(18,2)`; date is stored as `DateOnly`; an index exists on transaction date and category.
- **Definition of Done**: Migration applies cleanly to a local SQL Server; migration steps documented in the README.
- **Dependencies**: FND-01, FND-02.
- **Complexity**: M

### FND-04 Global error handling middleware

- **Priority**: P1
- **User story**: As a user, I want errors reported in a consistent, safe format so that I understand what went wrong without seeing internal details.
- **Acceptance criteria**:
  - AC1: Validation failures return 400 with ProblemDetails listing invalid fields.
  - AC2: Unexpected exceptions return 500 with a generic message and a correlation ID.
  - AC3: Responses contain no stack traces, SQL text, or type names.
- **Definition of Done**: Middleware unit-tested; correlation ID appears in logs.
- **Dependencies**: FND-01.
- **Complexity**: S

### FND-05 Web UI shell

- **Priority**: P0
- **User story**: As a user, I want a simple web page that loads and calls the API so that I can use the app in a browser.
- **Acceptance criteria**:
  - AC1: The home page loads over HTTPS with navigation to Add Expense, Dashboard, and Ask and Insights sections.
  - AC2: The UI calls only `/api/v1/...` endpoints.
  - AC3: Network errors show a readable message.
- **Definition of Done**: Manual smoke test passes in Chrome or Edge.
- **Dependencies**: FND-01.
- **Complexity**: S

---

## Epic 2: Transaction Management

### TXN-01 Add expense API with validation

- **Priority**: P0
- **User story**: As a user, I want to add an expense with an amount, date, description, and category so that I can record my spending.
- **Acceptance criteria**:
  - AC1: `POST /api/v1/transactions` accepts a valid expense and returns 201 with a `Location` header.
  - AC2: Missing amount, description, or date returns 400.
  - AC3: Amount of zero or less returns 400.
  - AC4: Date more than one year in the future returns 400.
  - AC5: Description longer than 200 characters returns 400.
  - AC6: Category must be in the allowed list; otherwise 400.
- **Definition of Done**: Validation rules unit-tested, including zero, negative, boundary dates, and empty input.
- **Dependencies**: FND-03, FND-04 (for error format; can start before FND-04 with a temporary response).
- **Complexity**: M

### TXN-02 Persist transaction

- **Priority**: P0
- **User story**: As a user, I want my expenses saved so that they remain available after I restart the app.
- **Acceptance criteria**:
  - AC1: A saved transaction keeps amount, date, description, and category as submitted.
  - AC2: The system assigns a unique ID.
  - AC3: After an application restart, `GET /api/v1/transactions/{id}` returns the same data.
  - AC4: A failed save returns an error and leaves no partial record.
- **Definition of Done**: Integration test covers save and read-back against SQL Server.
- **Dependencies**: TXN-01, FND-03.
- **Complexity**: S

### TXN-03 Add expense form in UI

- **Priority**: P0
- **User story**: As a user, I want a form to enter an expense so that I can record it without using the API directly.
- **Acceptance criteria**:
  - AC1: The form has fields for amount, date, description, and category, and a category dropdown populated from the API.
  - AC2: Validation errors from the API appear next to the relevant fields.
  - AC3: On success, the form shows a confirmation and clears.
- **Definition of Done**: Manual test of valid and invalid entries.
- **Dependencies**: TXN-02, FND-05.
- **Complexity**: S

---

## Epic 3: AI Transaction Processing

### AIT-01 Gemini client abstraction

- **Priority**: P0
- **User story**: As a developer, I want Gemini calls behind an interface so that the app can be tested without the real API and the provider can be swapped.
- **Acceptance criteria**:
  - AC1: `IAiCategorizationClient` is defined in `FinAI.Application`; the Gemini implementation is in `FinAI.Infrastructure`.
  - AC2: Each call has a timeout of 10 seconds or less.
  - AC3: At most 2 retries with backoff; retries are not attempted on validation failures.
  - AC4: The API key is read from configuration and never logged.
- **Definition of Done**: Timeout and retry behavior tested with a fake HTTP handler; no real Gemini calls in tests.
- **Dependencies**: FND-02, FND-01.
- **Complexity**: M

### AIT-02 Parse and validate AI output

- **Priority**: P0
- **User story**: As a user, I want AI suggestions checked before I see them so that wrong or malformed output does not reach my records.
- **Acceptance criteria**:
  - AC1: Responses are parsed into a strongly typed result; unparseable output is rejected.
  - AC2: Amount must be a positive decimal; date must be valid and within the allowed range.
  - AC3: A category outside the allowed list becomes `Uncategorized`.
  - AC4: Prompt text lives in one constants or embedded-file location in Infrastructure.
- **Definition of Done**: Unit tests cover valid, invalid, out-of-list, and empty responses.
- **Dependencies**: AIT-01.
- **Complexity**: M

### AIT-03 Natural-language expense proposal endpoint

- **Priority**: P0
- **User story**: As a user, I want to type an expense in plain language and get a proposed entry so that I do not have to fill in every field.
- **Acceptance criteria**:
  - AC1: `POST /api/v1/transactions/proposals` with a description returns a proposed amount, date, description, and category.
  - AC2: The endpoint does not save anything to the database.
  - AC3: If the AI is unavailable, times out, or returns invalid output, the endpoint returns a response indicating manual entry is required, with no proposal.
  - AC4: Input text is limited to 500 characters.
- **Definition of Done**: Integration test with a fake AI client covers success and fallback.
- **Dependencies**: AIT-02, TXN-01.
- **Complexity**: M

### AIT-04 Review and confirm proposal in UI

- **Priority**: P0
- **User story**: As a user, I want to review and edit an AI proposal before saving so that I stay in control of my records.
- **Acceptance criteria**:
  - AC1: The UI shows the proposal with editable amount, date, description, and category.
  - AC2: Nothing is saved until the user clicks Confirm.
  - AC3: A category of `Uncategorized` prompts the user to choose one before confirming.
  - AC4: If no proposal is returned, the UI points to the manual form.
- **Definition of Done**: Manual test of confirm, edit, and fallback paths.
- **Dependencies**: AIT-03, TXN-03.
- **Complexity**: M

### AIT-05 Audit logging of AI requests

- **Priority**: P1
- **User story**: As a maintainer, I want a record of each AI request outcome so that I can see failures and fallbacks.
- **Acceptance criteria**:
  - AC1: Each AI call logs feature, UTC timestamp, outcome (success, invalid output, timeout, unavailable), and duration.
  - AC2: Logs do not contain the full prompt or full transaction descriptions.
  - AC3: Failed and fallback outcomes are logged at Warning level using structured message templates.
- **Definition of Done**: Log entries verified in a local run; no sensitive text in sample logs.
- **Dependencies**: AIT-01, AIT-03.
- **Complexity**: S

---

## Epic 4: Dashboard

### DSH-01 Monthly totals and category breakdown API

- **Priority**: P0
- **User story**: As a user, I want to see total spending for a month and per category so that I know where my money goes.
- **Acceptance criteria**:
  - AC1: `GET /api/v1/dashboard/monthly?year=&month=` returns the monthly total and category totals.
  - AC2: Category totals sum to the monthly total.
  - AC3: A month with no transactions returns zero and an empty breakdown, not an error.
  - AC4: Totals are computed in code using `decimal`; no AI is involved.
  - AC5: Query uses `AsNoTracking()` and a projection.
- **Definition of Done**: Unit tests for totals and edge cases (empty month, one transaction, boundary dates); integration test for the endpoint.
- **Dependencies**: TXN-02.
- **Complexity**: M

### DSH-02 Dashboard UI

- **Priority**: P0
- **User story**: As a user, I want a dashboard page with a month selector so that I can review spending for any month.
- **Acceptance criteria**:
  - AC1: The page shows the monthly total and a category breakdown table or simple bar list.
  - AC2: Changing the month reloads the figures.
  - AC3: The figures update after a new expense is saved.
- **Definition of Done**: Manual test with at least two months of sample data.
- **Dependencies**: DSH-01, FND-05.
- **Complexity**: S

---

## Epic 5: AI Finance Assistant

### ASK-01 Deterministic spending query

- **Priority**: P0
- **User story**: As a user, I want to filter spending by category and month so that I get exact totals for specific questions.
- **Acceptance criteria**:
  - AC1: A query accepts an optional category and a required month and returns the matching total.
  - AC2: The result equals the dashboard figure for the same category and month.
  - AC3: An unknown category returns a validation error.
- **Definition of Done**: Unit tests for matching, no-match, and unknown category; integration test.
- **Dependencies**: DSH-01.
- **Complexity**: S

### ASK-02 Natural-language question endpoint

- **Priority**: P0
- **User story**: As a user, I want to ask a question such as "How much did I spend on food last month?" so that I get a direct answer.
- **Acceptance criteria**:
  - AC1: `POST /api/v1/assistant/questions` maps supported questions (category and/or month) to an ASK-01 query and returns the answer.
  - AC2: The numeric answer is computed by ASK-01; the AI supplies only the filter fields.
  - AC3: Unsupported questions return a message listing what can be asked, with no fabricated figure.
  - AC4: If the AI is unavailable, the endpoint returns a message directing the user to the structured form (ASK-03).
- **Definition of Done**: Integration test with a fake AI client covers supported, unsupported, and unavailable cases.
- **Dependencies**: ASK-01, AIT-01, AIT-02.
- **Complexity**: L

### ASK-03 Question UI with structured fallback

- **Priority**: P0
- **User story**: As a user, I want a question box and a category and month form so that I can get answers even when the AI is down.
- **Acceptance criteria**:
  - AC1: The UI submits free text to ASK-02 and displays the answer.
  - AC2: A category and month form calls ASK-01 directly and shows the same answer format.
  - AC3: Unsupported question messages are displayed as text, not as errors.
- **Definition of Done**: Manual test of AI path and form path.
- **Dependencies**: ASK-02, ASK-01, FND-05.
- **Complexity**: S

---

## Epic 6: AI Monthly Insights

### INS-01 Insight figures builder

- **Priority**: P0
- **User story**: As a user, I want the insight to be based on exact figures so that the summary is reliable.
- **Acceptance criteria**:
  - AC1: For a selected month, the builder computes the monthly total, category totals, largest category, and prior-month total.
  - AC2: A month with no transactions is flagged as having no data.
  - AC3: All figures are computed in code with `decimal`.
- **Definition of Done**: Unit tests for normal month, no data, and first month with no prior month.
- **Dependencies**: DSH-01.
- **Complexity**: S

### INS-02 AI monthly insight generation

- **Priority**: P0
- **User story**: As a user, I want a short AI summary of my month so that I can understand trends quickly.
- **Acceptance criteria**:
  - AC1: `GET /api/v1/insights/monthly?year=&month=` sends only the INS-01 figures to the AI and returns the summary text.
  - AC2: The prompt contains no user identifiers or full transaction descriptions.
  - AC3: A month with no data returns a no-data message without calling the AI.
  - AC4: If the AI fails, the response includes the figures with an "insight unavailable" message and returns 200.
  - AC5: Summary text is checked for length (at most 500 characters) before returning.
- **Definition of Done**: Unit tests with a fake AI client for success, no-data, and failure; prompt reviewed by a human.
- **Dependencies**: INS-01, AIT-01, AIT-02.
- **Complexity**: M

### INS-03 Insight display on dashboard

- **Priority**: P0
- **User story**: As a user, I want to see the insight next to my dashboard figures so that I read them together.
- **Acceptance criteria**:
  - AC1: The dashboard shows the insight text for the selected month.
  - AC2: When the insight is unavailable, the dashboard figures still display with a short message.
- **Definition of Done**: Manual test of success and unavailable states.
- **Dependencies**: INS-02, DSH-02.
- **Complexity**: S

---

## Epic 7: Testing

### TST-01 Unit tests for domain and application rules

- **Priority**: P0
- **User story**: As a developer, I want unit tests for validation and totals so that business rules are verified without a database.
- **Acceptance criteria**:
  - AC1: Tests cover TXN-01 validation, DSH-01 totals, ASK-01 filters, and INS-01 figures.
  - AC2: Tests use xUnit and FluentAssertions and follow `Method_Scenario_ExpectedResult` naming.
  - AC3: Tests do not use a database or network.
- **Definition of Done**: All unit tests pass locally and in CI.
- **Dependencies**: TXN-01, DSH-01, ASK-01, INS-01.
- **Complexity**: M

### TST-02 Fake AI client and AI behavior tests

- **Priority**: P0
- **User story**: As a developer, I want the AI paths tested with fakes so that fallbacks are verified without calling Gemini.
- **Acceptance criteria**:
  - AC1: A fake `IAiCategorizationClient` supports success, invalid output, timeout, and unavailable responses.
  - AC2: Tests verify AIT-02 validation and the fallback behavior in AIT-03, ASK-02, and INS-02.
  - AC3: No test makes a real Gemini call.
- **Definition of Done**: Tests pass in CI without an API key.
- **Dependencies**: AIT-02, AIT-03, ASK-02, INS-02.
- **Complexity**: S

### TST-03 Integration tests for P0 endpoints

- **Priority**: P0
- **User story**: As a developer, I want integration tests for each P0 endpoint so that the full stack is verified.
- **Acceptance criteria**:
  - AC1: Tests use `WebApplicationFactory` and a SQL Server instance (or Testcontainers).
  - AC2: Tests cover create, read, dashboard, question, and insight endpoints.
  - AC3: Tests needing external services are marked with a trait so they can be skipped in CI.
- **Definition of Done**: Tests pass locally and in CI, or are skipped with the trait where the external service is required.
- **Dependencies**: TXN-02, DSH-01, ASK-02, INS-02.
- **Complexity**: M

---

## Epic 8: Security

### SEC-01 API input validation

- **Priority**: P0
- **User story**: As a user, I want my input checked at the API boundary so that malformed data never reaches storage or AI.
- **Acceptance criteria**:
  - AC1: Every P0 endpoint validates required fields, string lengths, amount ranges, date ranges, and enum values.
  - AC2: Question and proposal text is limited in length.
- **Definition of Done**: Validation tests for each endpoint.
- **Dependencies**: TXN-01, AIT-03, ASK-02.
- **Complexity**: S

### SEC-02 HTTPS and secure defaults

- **Priority**: P0
- **User story**: As a user, I want the app served over HTTPS with safe defaults so that my data is protected in transit.
- **Acceptance criteria**:
  - AC1: HTTPS redirection is enabled.
  - AC2: CORS allows only explicitly configured origins.
  - AC3: No certificate or TLS checks are disabled.
- **Definition of Done**: Reviewer confirms settings; manual test over HTTPS.
- **Dependencies**: FND-01.
- **Complexity**: S

### SEC-03 Sign-in and sign-out with hashed passwords

- **Priority**: P1
- **User story**: As a user, I want to sign in before using the app so that my expenses are not open to anyone with the URL.
- **Acceptance criteria**:
  - AC1: Unauthenticated requests to transaction, dashboard, assistant, and insight endpoints return 401.
  - AC2: Sign-in with valid credentials succeeds; sign-out ends the session.
  - AC3: Invalid credentials return a generic error that does not reveal whether the username exists.
  - AC4: Passwords are stored only as salted hashes.
- **Definition of Done**: Authentication design approved by a human (it is a security decision); tests cover the acceptance criteria.
- **Dependencies**: FND-03, SEC-02.
- **Complexity**: L

### SEC-04 Per-user data scoping

- **Priority**: P1
- **User story**: As a user, I want my transactions visible only to me so that other users cannot see my spending.
- **Acceptance criteria**:
  - AC1: Transactions store the owning user ID.
  - AC2: All queries are filtered by the signed-in user.
  - AC3: A request for another user's transaction returns 404.
- **Definition of Done**: Integration test with two users confirms isolation.
- **Dependencies**: SEC-03.
- **Complexity**: S

### SEC-05 Secret handling check

- **Priority**: P0
- **User story**: As a team member, I want secrets kept out of the repository so that the Gemini key and database credentials stay private.
- **Acceptance criteria**:
  - AC1: No real key or connection string appears in tracked files.
  - AC2: Logs and error responses do not contain secrets (verified by test or review).
  - AC3: Local secrets use `dotnet user-secrets`; CI uses GitHub secrets.
- **Definition of Done**: Repository scan performed and documented in the PR.
- **Dependencies**: FND-02.
- **Complexity**: S

---

## Epic 9: CI

### CI-01 Build and test workflow

- **Priority**: P0
- **User story**: As a team member, I want a GitHub Actions workflow that builds and tests on each push so that regressions are caught early.
- **Acceptance criteria**:
  - AC1: Workflow runs on push and pull request to `main`.
  - AC2: Workflow restores, builds, and runs unit tests; integration tests run or are skipped per trait.
  - AC3: Workflow fails when a build or test fails.
  - AC4: Workflow uses read-only repository permissions.
- **Definition of Done**: A green run on `main`; reviewer approves workflow permissions.
- **Dependencies**: TST-01, FND-01.
- **Complexity**: S

### CI-02 Format check in CI

- **Priority**: P0
- **User story**: As a team member, I want formatting checked in CI so that code style stays consistent.
- **Acceptance criteria**:
  - AC1: `dotnet format --verify-no-changes` runs in the workflow.
  - AC2: A formatting violation fails the build.
- **Definition of Done**: Workflow shows a passing format step.
- **Dependencies**: CI-01.
- **Complexity**: S

---

## Recommended Implementation Order

Target: P0 first, P1 only with remaining budget. Stop and cut P1 items if the schedule slips.

| Step | Stories | Purpose |
|------|---------|---------|
| 1 | FND-01, FND-02, SEC-05 | Solution, configuration, secret placeholders |
| 2 | FND-03, TXN-01, TXN-02, TST-01 (transaction parts) | Store and validate expenses |
| 3 | TXN-03, FND-05, CI-01, CI-02 | First usable UI and CI |
| 4 | DSH-01, DSH-02, ASK-01 | Deterministic totals and filters |
| 5 | AIT-01, AIT-02, TST-02 | AI client, validation, fakes |
| 6 | AIT-03, AIT-04 | Natural-language entry with review |
| 7 | ASK-02, ASK-03 | Natural-language spending questions |
| 8 | INS-01, INS-02, INS-03 | Monthly AI insight |
| 9 | TST-03, SEC-01, SEC-02 | Integration tests and boundary security |
| 10 | FND-04, AIT-05 | P1: error handling and AI audit logging |
| 11 | SEC-03, SEC-04 | P1: authentication and per-user scoping |

Rough effort (from the S, M, L ranges in the table above):

- **P0**: 27 stories (15 S, 11 M, 1 L) is about 41 to 80 hours, roughly 53 at the midpoint. This exceeds the 25-hour budget.
- **P1**: 4 stories (3 S, 1 L as SEC-03) is about 8 to 14 hours.

Before starting, the team must either reduce P0 scope (for example, simplify ASK-02, merge TST-02 into TST-01, or drop the separate CI-02 format step) or approve a larger budget. SEC-03 (L) is the largest P1 risk and should be cut first.
