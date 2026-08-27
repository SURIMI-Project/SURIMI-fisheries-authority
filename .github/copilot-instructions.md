# Copilot Instructions — SURIMI Fisheries Authority

## Project overview
- ASP.NET Core gRPC server (`SURIMI-fisheries-authority`, C# / .NET 10) that acts as the regulatory authority in the SURIMI simulation framework.
- Communicates exclusively with the SURIMI Controller over gRPC using the shared `BSR.Surimi.Surimi-Protocol` Protobuf contract.
- References the EwECore VB.NET library (`Eii.Ecopath`, .NET Framework 4.8) for MSE stock recruitment and quota calculation logic. Do not modify EwECore code unless explicitly asked.

## Coding conventions
- Follow the existing patterns in `QuotaCalculationService`: resolve species to 1-based EwECore group indices via `SpeciesGroupMap.TryGetGroupIndex`, log a warning and skip unmapped species, keep `applied`/`skipped` counters, and finish with an information log.
- EwECore arrays are 1-based with inclusive sizing: allocate `nGroups + 1` elements and ignore index 0.
- Use string interpolation in log messages, `m_` prefix for private instance fields, and nullable reference types (`<Nullable>enable</Nullable>`).
- Guard operational methods with an `InvalidOperationException` when the simulation is not initialised.
- Only add comments when they explain domain logic (e.g., discard survival, index conventions).

## Unit tests
- Test project: `SURIMI-fisheries-authority.Tests` (xUnit, net10.0).
- Use pinned versions for test libraries:
  - `Moq` version `4.18.0`
  - `FluentAssertions` version `6.6.0`
- **All tests must follow the Arrange-Act-Assert (AAA) pattern, with explicit `// Arrange`, `// Act`, and `// Assert` comments marking each section.** Use `// Act & Assert` for a combined section (e.g., `Assert.ThrowsAsync`).
- Name tests `MethodUnderTest_ExpectedBehaviour` (e.g., `UpdateCatchDisposition_AccumulatesAcrossMonthlyCalls`).
- Use `Moq` to mock interfaces (e.g., `IMSEStockRecruitment`, `IMSEQuotaCalculator`) and `FluentAssertions` for assertions (`.Should().Be(...)`, `.Should().ThrowAsync<...>()`).
- Use shared private helpers to build test data (e.g., `CreateInitialisedServiceAsync`, `CreateGrid`).

## Domain notes
- Catches removed from the stock = `GrossCatchBiomass - LiveDiscardsBiomass` (live discards survive; dead discards remain part of the removal).
- Yearly accumulators (`Biomass`, `CatchYearGroup`) are cleared in `GetRegulationsAsync` after quotas are calculated, so they cover exactly one regulatory year.
