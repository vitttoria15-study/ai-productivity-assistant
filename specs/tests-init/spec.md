# Tests Init Spec

## Goal

Add initial backend test infrastructure for the ASP.NET Core API.

The goal is to prepare lightweight automated testing before adding more complex integrations such as Todoist API, AI providers, and Ollama.

## Scope

Add a backend test project for:
- service-level tests
- controller-level tests where needed
- future Todoist integration tests
- future AI provider tests

## Tech Stack

- xUnit
- FluentAssertions
- Moq

## Out of Scope

- frontend tests
- end-to-end tests
- Playwright
- performance tests
- Docker test infrastructure

## Acceptance Criteria

- Test project exists under `tests/backend.tests`
- Test project references the backend project
- xUnit runs successfully
- At least one simple test exists
- Tests can be executed with `dotnet test`