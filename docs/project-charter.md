# FinAI Project Charter

## 1. Project Name

FinAI: an AI-powered personal finance assistant (demonstration project).

## 2. Problem Statement

Recording personal expenses is tedious. Users must manually pick categories for each transaction, and few get a quick view of where their money goes or can ask simple questions about their spending. This project also serves as a one-week vehicle to show how an AI-driven software development lifecycle works in practice.

## 3. Vision

A small, readable application in which a user can record expenses in plain language, have AI assist with categorization, and see a simple view of their spending. The project itself demonstrates how GitHub Copilot can assist across the full lifecycle.

## 4. Objectives

- Deliver a working end-to-end MVP within one week.
- Demonstrate GitHub Copilot support for requirements, planning, architecture, development, testing, security, documentation, and CI.
- Integrate Google's Gemini API (free tier) for application-level AI features with safe fallbacks.
- Keep the codebase simple, testable, and easy to explain.

## 5. Target User

- Primary: the project team and reviewers evaluating the AI-DLC approach.
- Secondary: a single individual using the app to track personal expenses manually or with AI-assisted categorization.

## 6. MVP Scope

A user can:
- Register, log in, and log out securely.
- Store each user's expenses separately.
- Require authentication for expense, dashboard, and AI-related endpoints.
- Prevent users from accessing another user's financial data.
- Add an expense (amount, date, description, category).
- Enter a natural-language expense description, and have AI extract and categorize the transaction details, which the user can review before saving.
- View a basic spending dashboard (totals and breakdown by category).
- Ask simple natural-language questions about spending (for example, "How much did I spend on food last month?").
- Generate simple monthly spending insights.

Technical scope: ASP.NET Core Web API (.NET 8), SQL Server with Entity Framework Core, a simple web UI, xUnit tests with FluentAssertions, and GitHub Actions CI.

## 7. Out of Scope

- Connecting to real bank accounts or importing bank feeds.
- Regulated financial advice or financial planning features (budgets, forecasts, investments, tax).
- Processing real payments or money movement.
- Becoming a production banking or multi-user commercial platform.
- Complex financial planning or reporting.
- Advanced identity features: social login, multi-factor authentication (MFA), and roles.
- Docker containers, microservices, or other infrastructure beyond what the MVP needs.

## 8. Success Criteria

- All MVP capabilities in section 6 work end to end from the web UI through the API to SQL Server.
- Registration, login, and logout work with ASP.NET Core Identity; all expense, dashboard, and AI endpoints reject unauthenticated requests.
- A user cannot read or change another user's expenses, dashboard figures, AI answers, or insights (verified by tests).
- AI categorization works with the Gemini free tier, and the app remains usable with manual entry when the AI service is unavailable.
- Unit and integration tests pass in GitHub Actions CI.
- Copilot-generated artifacts (instructions, charter, code, tests, documentation) are traceable in the repository.
- The team can explain, for each lifecycle stage, what Copilot did and what humans reviewed.

## 9. Technical Constraints

- Use ASP.NET Core Identity for authentication, with user and expense data stored in SQL Server. Protect API endpoints and enforce user-level data isolation.
- .NET 8, C#, ASP.NET Core Web API.
- SQL Server with Entity Framework Core (code-first migrations).
- Google Gemini API, free tier only. Model name and API key come from configuration.
- Money stored as `decimal`; dates and times stored in UTC.
- No Docker, no microservices.
- Secrets are never committed; they are provided via user secrets, environment variables, or CI secrets.
- Layered structure: Api, Application, Domain, Infrastructure, Web, and test projects, as defined in `.github/copilot-instructions.md`.

## 10. Assumptions

- Developers have the .NET 8 SDK, a SQL Server instance, and a Gemini API key.
- Gemini free-tier quotas are sufficient for demonstration use.
- Expense data is entered by hand or via AI-assisted text input; no external data sources are needed.
- Multiple registered users can use the application, with each user's financial data isolated.
- The one-week timeline is fixed; scope is reduced before timeline is extended.

## 11. Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Scope creep beyond the MVP | Timeline slips | Enforce the out-of-scope list; defer new ideas to a backlog |
| Gemini free-tier rate limits or outages | AI features unavailable | Timeouts, limited retries, manual-entry fallback |
| Copilot-generated code that is incorrect or insecure | Defects or vulnerabilities | Human review, tests, security rules in copilot instructions, CI checks |
| SQL Server or environment setup problems | Delayed development | Document setup in README; standard tooling only |
| Misuse of the app as real financial advice | Reputational or user harm | Clear disclaimer that FinAI is a demo and provides no financial advice |
| Accidental secret exposure | Credential compromise | User secrets, `.gitignore`, CI secret references, rotate on exposure |
| Authentication or authorization flaw exposes one user's data to another | Privacy breach | ASP.NET Core Identity defaults, user ID scoping on every query, isolation tests, human security review |
| Insecure password or session handling | Account compromise | Identity password hashing and lockout defaults, HTTPS-only cookies, no custom crypto |

## 12. AI-Specific Risks

- **Incorrect categorization or extraction**: AI may assign wrong categories or amounts. Mitigation: user reviews AI output before saving; values are validated against allowed categories.
- **Hallucinated answers to spending questions**: AI may invent numbers. Mitigation: totals and figures are computed by deterministic code from stored data; AI only phrases results.
- **Data sent to an external provider**: Expense descriptions leave the system. Mitigation: send the minimum data needed, strip identifiers, and do not log full prompts.
- **Prompt injection through user text**: Malicious descriptions could alter AI behavior. Mitigation: treat AI output as untrusted; no AI output writes directly to the database or changes totals.
- **Availability and quota limits**: Free-tier limits may block calls. Mitigation: caching of repeated descriptions, retry limits, and a functional manual path.
- **Non-deterministic output**: Results may differ between runs, making tests flaky. Mitigation: unit tests use fake AI clients; no real Gemini calls in tests.
- **Cross-user data in AI answers or insights**: A question or prompt could reach another user's data. Mitigation: AI only produces filters; the query is always scoped to the authenticated user ID, and AI prompts contain only that user's aggregated figures.

## 13. Human Responsibilities

- Define and prioritize requirements; approve scope changes.
- Make and approve architectural, security, data, AI-behavior, dependency, and CI/CD decisions.
- Review all Copilot-generated code, tests, and documentation before merging.
- Provide and protect secrets (Gemini API key, SQL connection strings).
- Verify AI categorization and insight outputs during manual testing.
- Decide on and record Architecture Decision Records in `docs/adr/`.
- Rotate any exposed secret and decide on incident response.

## 14. AI Responsibilities

GitHub Copilot (development assistant):

- Generate code, tests, and documentation that follow `.github/copilot-instructions.md`.
- Propose options and tradeoffs for architectural and security decisions instead of deciding unilaterally.
- Keep changes minimal and focused on a single concern per change.
- Flag uncertainty and ask for clarification when requirements are unclear.

FinAI application AI (Gemini):

- Suggest a category and extract transaction fields from natural-language descriptions.
- Phrase answers to spending questions and monthly insights from data supplied by deterministic code.

Both must operate within the boundaries above: AI output is untrusted, validated, and never a substitute for human review or deterministic financial calculations.

## 15. Definition of Done

A feature or MVP item is done when:

- It implements the behavior described in section 6 and does not add out-of-scope functionality.
- Code follows `.github/copilot-instructions.md` and passes `dotnet format` checks.
- Unit tests cover the success path, validation failures, and edge cases; integration tests cover API endpoints where applicable.
- All tests pass locally and in GitHub Actions CI.
- The application works with manual entry when Gemini is unavailable.
- No secrets are committed, and no personal or sample data is hard-coded.
- Authentication is enforced: unauthenticated requests to protected endpoints are rejected, and isolation tests show one user cannot access another user's data.
- Relevant documentation (README, ADRs, XML comments) is updated in the same change.
- A human has reviewed the change and approved it.
