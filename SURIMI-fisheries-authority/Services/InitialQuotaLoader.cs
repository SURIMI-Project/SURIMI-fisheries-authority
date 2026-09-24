using Eii.BlobStore;
using Grpc.Core;
using SURIMI_fisheries_authority.Models;
using System.Globalization;

namespace SURIMI_fisheries_authority.Services
{
    /// <summary>
    /// Loads the initial Total Allowable Catch (TAC) configuration for a scenario from a narrow-format CSV file:
    /// comma-delimited, dot as decimal separator, one row per species with the columns
    /// "species_code,life_stage,TAC".
    /// </summary>
    public class InitialQuotaLoader
    {
        private const char Delimiter = ',';
        private const int ColumnCount = 3;
        private readonly IBlobStore _blobStore;
        private readonly ILogger<InitialQuotaLoader> _logger;

        public InitialQuotaLoader(IBlobStore blobStore, ILogger<InitialQuotaLoader> logger)
        {
            _blobStore = blobStore;
            _logger = logger;
        }

        public async Task<InitialQuotaMap> LoadAsync(string scenarioName, CancellationToken cancellationToken = default)
        {
            string fileNamePath = $"{scenarioName}/{scenarioName}_initial_quota.csv";
            if (!await _blobStore.ExistsAsync(fileNamePath, PathType.Input, cancellationToken))
            {
                throw new RpcException(new Status(StatusCode.Internal, $"Couldn't find {fileNamePath}"));
            }

            var text = await _blobStore.ReadAllTextAsync(fileNamePath, PathType.Input, cancellationToken);
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            if (lines.Length == 0)
            {
                throw new InvalidDataException($"Initial quota file {fileNamePath} is empty");
            }

            ValidateHeader(lines[0], fileNamePath);
            var map = new InitialQuotaMap();

            for (int iLine = 1; iLine < lines.Length; iLine++)
            {
                if (string.IsNullOrWhiteSpace(lines[iLine]))
                {
                    continue;
                }

                ParseRow(lines[iLine], iLine + 1, map, fileNamePath);
            }

            _logger.LogInformation("Loaded {SpeciesCount} species initial quotas for scenario {ScenarioName}", map.SpeciesCount, scenarioName);
            return map;
        }

        private static void ValidateHeader(string headerLine, string filePath)
        {
            var cells = headerLine.Split(Delimiter);
            if (cells.Length < ColumnCount
                || !string.Equals(cells[0].Trim(), "species_code", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(cells[1].Trim(), "life_stage", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(cells[2].Trim(), "TAC", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Initial quota file {filePath} has an invalid header; expected 'species_code,life_stage,TAC'");
            }
        }

        private static void ParseRow(string line, int lineNumber, InitialQuotaMap map, string filePath)
        {
            var cells = line.Split(Delimiter);
            if (cells.Length < ColumnCount)
            {
                throw new InvalidDataException($"Line {lineNumber} of {filePath} has {cells.Length} columns; expected at least {ColumnCount}");
            }

            var speciesCode = cells[0].Trim();
            var lifeStage = cells[1].Trim();
            if (string.IsNullOrEmpty(speciesCode))
            {
                throw new InvalidDataException($"Line {lineNumber} of {filePath} has an empty species_code");
            }

            var trimmedTac = cells[2].Trim();
            if (!float.TryParse(trimmedTac, NumberStyles.Float, CultureInfo.InvariantCulture, out float tac))
            {
                throw new InvalidDataException($"Invalid TAC value '{trimmedTac}' for species ({speciesCode}, {lifeStage}) on line {lineNumber} of {filePath}");
            }

            if (!map.Add(new SpeciesKey(speciesCode, lifeStage), tac))
            {
                throw new InvalidDataException($"Duplicate species ({speciesCode}, {lifeStage}) on line {lineNumber} of {filePath}");
            }
        }
    }
}
