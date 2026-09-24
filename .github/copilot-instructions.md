# Copilot Instructions — SURIMI Fisheries Authority

## Project overview
- ASP.NET Core gRPC server (`SURIMI-fisheries-authority`, C# / .NET 10) that acts as the regulatory authority in the SURIMI simulation framework.
- Communicates exclusively with the SURIMI Controller over gRPC using the shared `BSR.Surimi.Surimi-Protocol` Protobuf contract.
- References the EwECore VB.NET library (`Eii.Ecopath`, .NET Framework 4.8) for MSE stock recruitment and quota calculation logic. Do not modify EwECore code unless explicitly asked.

## Coding conventions
- Follow the existing patterns in `QuotaCalculationService`: resolve species to 1-based EwECore group indices via `SpeciesGroupMap.TryGetGroupIndex`, log a warning and skip unmapped species, keep `applied`/`skipped` counters, and finish with an information log.
- EwECore arrays are 1-based with inclusive sizing: allocate `nGroups + 1` elements and ignore index 0.
- Use structured logging message templates with PascalCase named placeholders and value arguments (e.g., `m_logger.LogInformation("Initialized simulation {SimulationId}", simulationId)`); do not use interpolated strings in log messages (CA2254). Use `m_` prefix for private instance fields, and nullable reference types (`<Nullable>enable</Nullable>`).
- Guard operational methods with an `InvalidOperationException` when the simulation is not initialised.
- Async methods that perform I/O (blob storage, loaders, service operations) accept a `CancellationToken` parameter and pass it through to all awaited calls; gRPC endpoints forward `context.CancellationToken`.
- Only add comments when they explain domain logic (e.g., discard survival, index conventions).

## Domain notes
- Catches removed from the stock = `GrossCatchBiomass - LiveDiscardsBiomass` (live discards survive; dead discards remain part of the removal).
- Yearly accumulators (`Biomass`, `CatchYearGroup`) are cleared in `GetRegulationsAsync` after quotas are calculated, so they cover exactly one regulatory year.
- `Bestimate[]` must be seeded exactly once by `QuotaCalculationService` on the first biomass update for a group, and never re-assigned afterwards: `cMSEQuotaCalculator` uses the *previous* `Bestimate` value in its assessment and then updates it itself. 
- `BhalfT`, `Rmax` and `Fish1` are one-time startup values derived from the initial (unfished) biomass B0 and the first catch disposition; they stay constant for the rest of the simulation. The `m_IsBiomassAlreadyAssigned` / `m_IsCatchYearGroupAlreadyAssigned` flags guard this one-time initialisation and are intentionally never reset.
- `CsvMseDiagnosticsRecorder` writes 4 CSV files per simulation (`biomass-monthly`, `catch-monthly`, `mse-assessment`, `tac`) uploaded under the `{simulationId}/` blob prefix with filenames prefixed by `{simulationId}_`. Rows and headers do not include a `simulation_id` column, since the id is already encoded in the directory and filename; the `simulationId` parameter on each `Record*` method is only used for internal buffering/routing, not written to the CSV content.

## Scenario configuration files
- Scenario CSV files (quota shares, recruitment) are comma-delimited with a dot as decimal separator (`CultureInfo.InvariantCulture`).
- Quota share files are wide-format: header `species_code,life_stage,GEAR|COUNTRY,...` with one column per fleet named `GEAR|COUNTRY`; an empty cell means the fleet has no share; shares per species must sum to 1 (tolerance 1e-4).
- Recruitment files are narrow-format: header `species_code,life_stage,RstockRatio,RHalfB0Ratio,cvRec`.
- Loaders validate CSV contents against the `SurimiContract` in `CreateRegulationsAsync` (species and fleets in the file must exist in the contract).

## Unit tests
- Test project: `SURIMI-fisheries-authority.Tests` (xUnit, net10.0).
- Use pinned versions for test libraries:
  - `Moq` version `4.18.0`
  - `FluentAssertions` version `6.6.0`
- **All tests must follow the Arrange-Act-Assert (AAA) pattern, with explicit `// Arrange`, `// Act`, and `// Assert` comments marking each section.** Use `// Act & Assert` for a combined section (e.g., `Assert.ThrowsAsync`).
- Name tests `MethodUnderTest_ExpectedBehaviour` (e.g., `UpdateCatchDisposition_AccumulatesAcrossMonthlyCalls`).
- Use `Moq` to mock interfaces (e.g., `IMSEStockRecruitment`, `IMSEQuotaCalculator`) and `FluentAssertions` for assertions (`.Should().Be(...)`, `.Should().ThrowAsync<...>()`).
- Use shared private helpers to build test data (e.g., `CreateInitialisedServiceAsync`, `CreateGrid`).
- Keep CSV fixtures in tests in the current loader format (comma-delimited, dot decimals, `GEAR|COUNTRY` fleet columns).
- When testing gRPC endpoints, pass a minimal `ServerCallContext` stub (see `TestServerCallContext` in `FisheriesAuthorityServiceTests`) instead of `null`, since endpoints read `context.CancellationToken`.
