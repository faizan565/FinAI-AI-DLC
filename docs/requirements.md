# FinAI MVP Requirements

| Item | Value |
|------|-------|
| Source | `docs/project-charter.md` (approved) |
| Budget | Approximately 25 hours |
| Scope | MVP only (P0 and P1 as defined below) |

This document separates **requirements** (what the system must do or meet) from **implementation decisions** (how the team chooses to build it). Implementation decisions are listed in section 4 and are not requirements. They may change through ADRs in `docs/adr/` without changing this document.

## 1. Priority Definitions

- **P0**: Required for the MVP to be considered complete. Must be delivered first.
- **P1**: Delivered only if it does not put the 25-hour budget at risk. Cut first if time runs short.

## 2. Functional Requirements

### 2.0 Allowed Categories (shared reference)

This list is the single source of allowed categories. FR-01, FR-03, FR-04, FR-05, FR-06, and the AI design must all use it.

- Food, Transport, Housing, Utilities, Entertainment, Health, Shopping, Other, Uncategorized
- Matching is case-insensitive and maps to the canonical name above. Any other value is rejected for user input, and mapped to `Uncategorized` for AI output.
- `Uncategorized` is a valid stored value, but a transaction in that category must be re-categorized by the user before it is confirmed from an AI proposal.

### FR-01 Add Expense

- **Priority**: P0
- **Description**: The user can enter a new expense with an amount, a transaction date, a description, and a category.
- **Acceptance criteria**:
  - AC1: A valid expense (positive amount, date, non-empty description, allowed category) is accepted and saved.
  - AC2: A missing amount, missing description, or missing date is rejected with a validation message and nothing is saved.
  - AC3: An amount of zero or less is rejected.
  - AC4: A date more than one year in the future is rejected.
  - AC5: A description longer than the configured maximum length (for example 200 characters) is rejected.
  - AC6: On success, the user sees confirmation and the expense appears in the dashboard totals.
  - AC7: An amount with more than two decimal places is rejected with a validation message; it is never silently rounded.
  - AC8: The date is a calendar date entered by the user. It is stored as entered and is not shifted by time zone conversion.
- **Dependencies**: FR-02 (storage), FR-03 (category list used by the form, optional when AI is unavailable).

### FR-02 Store Transaction

- **Priority**: P0
- **Description**: Accepted expenses are persisted so they survive application restarts and can be used by the dashboard, spending questions, and insights.
- **Acceptance criteria**:
  - AC1: A saved transaction retains its amount, date, description, and category exactly as submitted after validation.
  - AC2: Each transaction has a unique identifier assigned by the system.
  - AC3: Stored amounts keep two decimal places of precision.
  - AC4: Transactions are retrievable after the application restarts.
  - AC5: If the save fails, the user receives an error message and no partial record is stored.
- **Dependencies**: FR-07 (owner identity).

### FR-03

- **Priority**: P0
- **Description**: The user can type a natural-language expense (for example, "Lunch with Sam 14.50 yesterday"). The AI proposes an amount, date, description, and category. The user reviews and confirms the proposal before it is saved.
- **Acceptance criteria**:
  - AC1: Given a clear description, the system returns a proposal with an amount, a date, a description, and a category from the allowed list.
  - AC2: The proposal is shown to the user for review; nothing is saved until the user confirms.
  - AC3: If the AI returns a category outside the allowed list, the category is set to `Uncategorized` and the user is told to choose one.
  - AC4: If the AI returns an invalid amount or unparseable output, no proposal is shown and the user is directed to manual entry (FR-01).
  - AC5: If the AI service is unavailable or times out within the configured limit, the user is directed to manual entry and the application remains usable.
  - AC6: The user can edit any proposed field before confirming.
  - AC7: A missing or invalid amount or description is never silently filled in or saved. The field is shown as missing or invalid, and the user must enter a valid value before confirming. A missing or unparseable date is replaced by today's date only as a visibly marked, unconfirmed default. The user must review or change it before saving.
  - AC8: An AI proposal with an amount that has more than two decimal places, a date that is not an exact `yyyy-MM-dd` value, or a date outside the allowed range, is flagged for correction and is not saved until the user fixes it.
- **Dependencies**: FR-01, FR-02, NFR-AI-01, NFR-AI-02.

### FR-04 Basic Spending Dashboard

- **Priority**: P0
- **Description**: The user sees a simple view of spending: total spending for a selected month and a breakdown by category.
- **Acceptance criteria**:
  - AC1: The dashboard shows the total of all transactions for a selected calendar month.
  - AC2: The dashboard shows the total per category for that month, and the category totals sum to the monthly total.
  - AC3: A month with no transactions shows a total of zero and an empty breakdown, not an error.
  - AC4: Totals are computed by deterministic code, not by AI.
  - AC5: Totals update after a new expense is saved.
- **Dependencies**: FR-02.

### FR-05 Basic Natural-Language Spending Question

- **Priority**: P0
- **Description**: The user can ask a simple question about their spending in plain language, for example "How much did I spend on food last month?".
- **Acceptance criteria**:
  - AC1: For a supported question (a category and/or a time period), the answer shows a total computed from stored transactions.
  - AC2: The numeric value in the answer matches the value computed by the dashboard logic for the same filter.
  - AC3: If the question cannot be mapped to a supported filter, the user receives a message stating what is supported, without a fabricated answer.
  - AC4: If the AI service is unavailable, the user can still select a category and period from the form to get the same answer.
  - AC5: Supported questions are limited to an optional category from section 2.0 and a single calendar month (year and month). Any other filter is unsupported under AC3.
  - AC6: The AI only maps the question text to these filters. All numeric results are calculated by deterministic code using the same logic as FR-04.
- **Dependencies**: FR-02, FR-04, NFR-AI-01, NFR-AI-02.

### FR-06 AI Monthly Spending Insight

- **Priority**: P0
- **Description**: For a selected month, the system generates a short summary of spending (for example, the largest category and change from the previous month if data exists).
- **Acceptance criteria**:
  - AC1: The insight is generated from figures computed by deterministic code (monthly total, category totals, prior-month total).
  - AC2: The insight contains no figures other than those supplied to the AI.
  - AC3: A month with no transactions produces a message that no data exists, without calling the AI.
  - AC4: If the AI service fails, the dashboard figures are still shown with a message that the insight is unavailable.
  - AC5: The month-over-month percentage change is calculated by code as (current total minus prior total) divided by prior total, rounded to one decimal place.
  - AC6: If the prior month's total is zero or there is no prior-month data, no percentage is shown. The insight states the absolute change instead, or that no comparison is available.
- **Dependencies**: FR-02, FR-04, NFR-AI-01, NFR-AI-02.

### FR-07 Basic Authentication

- **Priority**: P0
- **Description**: Users register, sign in, and sign out with ASP.NET Core Identity backed by SQL Server. All expense, dashboard, spending-question, and insight data is private to its owner.
- **Acceptance criteria**:
  - AC1: A user can register an account with a unique username and password.
  - AC2: A registered user can sign in with a valid username and password and sign out.
  - AC3: Invalid credentials return a generic error that does not reveal whether the username exists.
  - AC4: Unauthenticated requests to all expense, dashboard, spending-question, and insight endpoints return 401.
  - AC5: The server derives the owner from the authenticated principal. A client-supplied UserId is never used for ownership or filtering and is ignored or rejected.
  - AC6: Every transaction belongs to exactly one owner. A user cannot read, create under another identity, modify, or delete another user's transaction; such requests return 404.
  - AC7: Dashboard, spending-question, and insight results include only transactions owned by the authenticated user.
- **Dependencies**: FR-02, NFR-SEC-05, NFR-SEC-06.

### FR-08 Basic Error Handling (P1)

- **Priority**: P1
- **Description**: Errors are reported to the user in a consistent, safe way.
- **Acceptance criteria**:
  - AC1: Validation failures return a 400-class response describing the invalid fields.
  - AC2: Unexpected failures return a 500-class response with a generic message and a correlation identifier.
  - AC3: No stack traces, SQL details, or internal type names appear in responses.
  - AC4: The UI shows a readable message for each error response.
- **Dependencies**: FR-01, FR-03, FR-05, FR-06.

### FR-09 Audit Logging of AI Requests (P1)

- **Priority**: P1
- **Description**: Each AI request is recorded so that usage, failures, and fallbacks can be reviewed.
- **Acceptance criteria**:
  - AC1: Each AI call records the feature (categorization, question, insight), timestamp (UTC), outcome (success, invalid output, timeout, unavailable), and duration.
  - AC2: The log does not store the full prompt or full transaction descriptions.
  - AC3: Failed and fallback outcomes are logged at Warning level.
- **Dependencies**: FR-03, FR-05, FR-06.

## 3. Non-Functional Requirements

### 3.1 Security

- **NFR-SEC-01**: All external input (amount, date, description, question text) is validated at the API boundary before use.
- **NFR-SEC-02**: Database access uses parameterized queries only.
- **NFR-SEC-03**: Error responses do not expose stack traces, SQL, or internal type names.
- **NFR-SEC-04**: Transport uses HTTPS.
- **NFR-SEC-05**: Passwords are stored only as salted hashes (ASP.NET Core Identity default), never in plain text.
- **NFR-SEC-06**: Every protected endpoint requires an authenticated principal. Owner identity comes only from that principal. Every read, create, update, and delete is filtered by owner, and cross-user access returns 404.
- **NFR-SEC-07**: Usernames are unique (case-insensitive). Password policy uses ASP.NET Core Identity defaults with a minimum length of 8. Lockout applies after 5 failed sign-in attempts for 15 minutes. Login failures return the same generic message for an unknown user, a wrong password, and a locked account.
- **NFR-SEC-08**: Auth cookies are `HttpOnly`, `Secure`, and `SameSite=Lax`. The antiforgery cookie is `HttpOnly`, `Secure`, and `SameSite=Strict`. State-changing requests require a valid antiforgery token, and the token is refreshed after sign-in and sign-out.

### 3.2 Performance

- **NFR-PERF-01**: Adding an expense (excluding AI calls) completes in under 1 second for a single user.
- **NFR-PERF-02**: The dashboard for one month loads in under 2 seconds with 5,000 stored transactions.
- **NFR-PERF-03**: Each AI request has an overall deadline of 10 seconds, including retries and backoff. Each attempt has a 3-second timeout within that deadline.

### 3.3 Reliability

- **NFR-REL-01**: The application stays usable with manual entry and dashboard views when the AI service is unavailable.
- **NFR-REL-02**: AI calls use one initial attempt and at most 2 retries (three attempts total), with backoff, only for transient failures (attempt timeout, network error, HTTP 429, HTTP 503). Retries never run past the overall deadline in NFR-PERF-03.
- **NFR-REL-03**: A failed save never leaves a partial transaction.

### 3.4 Maintainability

- **NFR-MNT-01**: Code follows `.github/copilot-instructions.md`.
- **NFR-MNT-02**: Each layer (Api, Application, Domain, Infrastructure, Web) is separated so that business logic is not in controllers or UI code.
- **NFR-MNT-03**: Each change is a single concern and is reviewed by a human before merging.
- **NFR-MNT-04**: Setup, configuration, and test instructions are documented in the README.

### 3.5 Testability

- **NFR-TST-01**: Business rules (validation, totals, filters) are testable without a database or network.
- **NFR-TST-02**: Unit tests do not call the Gemini API; AI clients are replaced by test doubles.
- **NFR-TST-03**: Integration tests cover each P0 API endpoint, including anonymous requests (401) and cross-user isolation for expenses, dashboard, questions, and insights.
- **NFR-TST-04**: All tests run in GitHub Actions CI.

### 3.6 AI Output Validation

- **NFR-AI-01**: All AI responses are parsed into strongly typed results. Unparseable responses are rejected.
- **NFR-AI-02**: Amounts must be positive decimals with at most two decimal places; dates must be exact `yyyy-MM-dd` values within the allowed range; categories must belong to the allowed list. Anything else falls back as defined in FR-03.
- **NFR-AI-03**: AI output never writes to storage without user confirmation (FR-03 AC2).
- **NFR-AI-04**: Totals and dashboard figures are never produced by AI.
- **NFR-AI-05**: The data sent to the AI includes only what the feature needs. No user identifiers are sent.

### 3.7 Secret Management

- **NFR-SCR-01**: The Gemini API key and database connection string are never committed to the repository.
- **NFR-SCR-02**: Secrets are read from user secrets (local), environment variables or CI secrets (automation), or a managed store (deployment).
- **NFR-SCR-03**: Logs and error responses never contain secrets.
- **NFR-SCR-04**: The committed configuration contains placeholders only.

## 4. Implementation Decisions (not requirements)

These reflect the architecture and tooling in `.github/copilot-instructions.md` and the charter. They may be changed through an ADR.

| Topic | Decision |
|-------|----------|
| Platform | .NET 8, C#, ASP.NET Core Web API |
| Data access | SQL Server with EF Core, code-first migrations |
| Authentication | ASP.NET Core Identity with SQL Server; no social login, MFA, or roles |
| Money type | `decimal(18,2)` |
| Validation library | FluentValidation or data annotations |
| Error format | ProblemDetails (RFC 7807) |
| AI provider | Google Gemini free tier, accessed through an interface in Application |
| AI model name | Read from configuration |
| UI | Simple static web UI calling the API |
| Testing | xUnit and FluentAssertions |
| CI | GitHub Actions |
| API routes | `/api/v1/...` |

## 5. Out of Scope

These items are excluded from this requirements set:

- Bank integrations and automatic transaction import
- Payments or money movement
- Investment tracking
- Complex budgeting or financial planning
- Mobile applications
- RAG and vector databases
- Multi-agent architecture
- Microservices and Docker
- Social login, multi-factor authentication (MFA), and roles
