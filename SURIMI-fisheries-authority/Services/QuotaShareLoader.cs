using Eii.BlobStore;
using Grpc.Core;
using SURIMI_fisheries_authority.Models;
using System.Globalization;

namespace SURIMI_fisheries_authority.Services
{
    /// <summary>
    /// Loads the fleet quota share configuration for a scenario from a wide-format CSV file:
    /// semicolon-delimited, comma as decimal separator, one column per fleet named "GEAR,COUNTRY",
    /// with a trailing "Sum" column that is ignored.
    /// </summary>
    public class QuotaShareLoader
    {
        private const string FileSuffix = "-quotashare.csv";
        private const char Delimiter = ';';
        private readonly IBlobStore _blobStore;
        private readonly ILogger<QuotaShareLoader> _logger;


        private static readonly NumberFormatInfo s_numberFormat = new() { NumberDecimalSeparator = "," };


        public QuotaShareLoader(IBlobStore blobStore, ILogger<QuotaShareLoader> logger)
        {
            _blobStore = blobStore;
            _logger = logger;
        }

        public async Task<FleetQuotaShareMap> LoadAsync(string scenarioName)
        {
            if (!await _blobStore.ExistsAsync($"{scenarioName}{FileSuffix}", PathType.Input))
            {
                throw new RpcException(new Status(StatusCode.Internal, $"Couldn't find {scenarioName}{FileSuffix}"));
            }

            var text = await _blobStore.ReadAllTextAsync($"{scenarioName}{FileSuffix}", PathType.Input);
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            if (lines.Length == 0)
            {
                throw new InvalidDataException($"Quota share file {scenarioName}{FileSuffix} is empty");
            }

            var fleets = ParseHeader(lines[0], $"{scenarioName}{FileSuffix}", out int fleetColumnCount);
            var map = new FleetQuotaShareMap(fleets);

            for (int iLine = 1; iLine < lines.Length; iLine++)
            {
                if (string.IsNullOrWhiteSpace(lines[iLine]))
                {
                    continue;
                }

                ParseRow(lines[iLine], iLine + 1, fleets, fleetColumnCount, map, $"{scenarioName}{FileSuffix}");
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
                throw new InvalidDataException($"Quota share file {filePath} has an invalid header; expected 'species_code;life_stage;GEAR,COUNTRY;...;Sum'");
            }

            // The trailing "Sum" column is not a fleet column
            fleetColumnCount = cells.Length - 2;
            if (string.Equals(cells[^1].Trim(), "Sum", StringComparison.OrdinalIgnoreCase))
            {
                fleetColumnCount--;
            }

            var fleets = new List<FleetKey>(fleetColumnCount);
            for (int iFleet = 0; iFleet < fleetColumnCount; iFleet++)
            {
                var parts = cells[iFleet + 2].Split(',');
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                {
                    throw new InvalidDataException($"Invalid fleet column '{cells[iFleet + 2]}' in header of {filePath}; expected 'GEAR,COUNTRY'");
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
            float sum = 0f;
            for (int iFleet = 0; iFleet < fleetColumnCount; iFleet++)
            {
                var cell = cells[iFleet + 2].Trim();
                if (string.IsNullOrEmpty(cell))
                {
                    continue;
                }

                if (!float.TryParse(cell, NumberStyles.Float, s_numberFormat, out float share))
                {
                    throw new InvalidDataException($"Invalid share value '{cell}' for species ({speciesCode}, {lifeStage}) on line {lineNumber} of {filePath}");
                }

                shares.Add((fleets[iFleet], share));
                sum += share;
            }

            if (sum != 1.0f)
            {
                throw new InvalidDataException($"Shares for species ({speciesCode}, {lifeStage}) on line {lineNumber} of {filePath} sum to {sum}; expected exactly 1");
            }

            if (!map.Add(new SpeciesKey(speciesCode, lifeStage), shares))
            {
                throw new InvalidDataException($"Duplicate species ({speciesCode}, {lifeStage}) on line {lineNumber} of {filePath}");
            }
        }
    }
}
