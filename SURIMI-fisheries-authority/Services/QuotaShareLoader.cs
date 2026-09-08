using Eii.BlobStore;
using Grpc.Core;
using SURIMI_fisheries_authority.Models;
using System.Globalization;

namespace SURIMI_fisheries_authority.Services
{
    /// <summary>
    /// Loads the fleet quota share configuration for a scenario from a wide-format CSV file:
    /// comma-delimited, dot as decimal separator, one column per fleet named "GEAR|COUNTRY".
    /// </summary>
    public class QuotaShareLoader
    {
        private const char Delimiter = ',';
        private const float ShareSumTolerance = 1e-4f;
        private readonly IBlobStore _blobStore;
        private readonly ILogger<QuotaShareLoader> _logger;


        public QuotaShareLoader(IBlobStore blobStore, ILogger<QuotaShareLoader> logger)
        {
            _blobStore = blobStore;
            _logger = logger;
        }

        public async Task<FleetQuotaShareMap> LoadAsync(string scenarioName, CancellationToken cancellationToken)
        {
            string fileNamePath = $"{scenarioName}/{scenarioName}_quotashare.csv";

            if (!await _blobStore.ExistsAsync(fileNamePath, PathType.Input, cancellationToken))
            {
                throw new RpcException(new Status(StatusCode.Internal, $"Couldn't find {fileNamePath}"));
            }

            var text = await _blobStore.ReadAllTextAsync(fileNamePath, PathType.Input, cancellationToken);
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            if (lines.Length == 0)
            {
                throw new InvalidDataException($"Quota share file {fileNamePath} is empty");
            }

            var fleets = ParseHeader(lines[0], fileNamePath, out int fleetColumnCount);
            var map = new FleetQuotaShareMap(fleets);

            for (int iLine = 1; iLine < lines.Length; iLine++)
            {
                if (string.IsNullOrWhiteSpace(lines[iLine]))
                {
                    continue;
                }

                ParseRow(lines[iLine], iLine + 1, fleets, fleetColumnCount, map, fileNamePath);
            }
            _logger.LogInformation("Loaded {SpeciesCount} species quota shares for scenario {ScenarioName}", map.SpeciesCount, scenarioName);
            return map;
        }

        private static IReadOnlyList<FleetKey> ParseHeader(string headerLine, string filePath, out int fleetColumnCount)
        {
            var cells = headerLine.Split(Delimiter);
            if (cells.Length < 3
                || !string.Equals(cells[0].Trim(), "species_code", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(cells[1].Trim(), "life_stage", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Quota share file {filePath} has an invalid header; expected 'species_code,life_stage,GEAR|COUNTRY,...'");
            }

            fleetColumnCount = cells.Length - 2;

            var fleets = new List<FleetKey>(fleetColumnCount);
            for (int iFleet = 0; iFleet < fleetColumnCount; iFleet++)
            {
                var parts = cells[iFleet + 2].Split('|');
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                {
                    throw new InvalidDataException($"Invalid fleet column '{cells[iFleet + 2]}' in header of {filePath}; expected 'GEAR|COUNTRY'");
                }

                var fleet = new FleetKey(parts[0].Trim(), parts[1].Trim());
                if (fleets.Contains(fleet))
                {
                    throw new InvalidDataException($"Duplicate fleet column ({fleet.GearCode}, {fleet.CountryCode}) in header of {filePath}");
                }
                fleets.Add(fleet);
            }

            return fleets;
        }

        private static void ParseRow(string line, int lineNumber, IReadOnlyList<FleetKey> fleets, int fleetColumnCount, FleetQuotaShareMap map, string filePath)
        {
            var cells = line.Split(Delimiter);
            if (cells.Length < 2 + fleetColumnCount)
            {
                throw new InvalidDataException($"Line {lineNumber} of {filePath} has {cells.Length} columns; expected at least {2 + fleetColumnCount}");
            }

            var speciesCode = cells[0].Trim();
            var lifeStage = cells[1].Trim();
            if (string.IsNullOrEmpty(speciesCode))
            {
                throw new InvalidDataException($"Line {lineNumber} of {filePath} has an empty species_code");
            }

            var shares = new List<(FleetKey Fleet, float Share)>();
            for (int iFleet = 0; iFleet < fleetColumnCount; iFleet++)
            {
                var cell = cells[iFleet + 2].Trim();
                if (string.IsNullOrEmpty(cell))
                {
                    continue;
                }

                if (!float.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out float share))
                {
                    throw new InvalidDataException($"Invalid share value '{cell}' for species ({speciesCode}, {lifeStage}) on line {lineNumber} of {filePath}");
                }

                shares.Add((fleets[iFleet], share));
            }

            float sum = shares.Sum(s => s.Share);
            if (Math.Abs(sum - 1f) > ShareSumTolerance)
            {
                throw new InvalidDataException($"Shares for species ({speciesCode}, {lifeStage}) on line {lineNumber} of {filePath} sum to {sum.ToString(CultureInfo.InvariantCulture)}; expected 1");
            }

            if (!map.Add(new SpeciesKey(speciesCode, lifeStage), shares))
            {
                throw new InvalidDataException($"Duplicate species ({speciesCode}, {lifeStage}) on line {lineNumber} of {filePath}");
            }
        }
    }
}
