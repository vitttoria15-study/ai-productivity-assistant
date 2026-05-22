# Tests Init Plan

## Objective

Set up initial backend testing infrastructure for the project.

This is an enabling step for future features:
- Todoist integration
- AI provider layer
- Ollama support
- API refactoring

---

## Proposed Structure

```text
tests/
└── backend.tests/
    ├── backend.tests.csproj
    └── SmokeTests.cs
```

---

## Implementation Steps

1. Create `tests/backend.tests` project.
2. Add xUnit test framework.
3. Add FluentAssertions.
4. Add Moq.
5. Reference backend project.
6. Add one simple smoke test.
7. Run `dotnet test`.

---

## Commands

```bash
mkdir tests
cd tests

dotnet new xunit -n backend.tests

cd backend.tests

dotnet add package FluentAssertions
dotnet add package Moq

dotnet add reference ../../backend/backend.csproj

dotnet test
```

---

## First Test

Create a simple smoke test to verify test infrastructure works.

Example:

```csharp
using FluentAssertions;

namespace backend.tests;

public class SmokeTests
{
    [Fact]
    public void TestInfrastructure_ShouldWork()
    {
        var result = 1 + 1;

        result.Should().Be(2);
    }
}
```

---

## Validation

The setup is successful if:

```bash
dotnet test
```

runs successfully with at least one passing test.

---

## Notes

This step does not test Todoist or AI providers yet.

Future test coverage will be added when those services are implemented.
