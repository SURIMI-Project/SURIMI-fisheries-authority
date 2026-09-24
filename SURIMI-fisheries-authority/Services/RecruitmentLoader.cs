using Eii.BlobStore;
using Grpc.Core;
using SURIMI_fisheries_authority.Models;
using System.Globalization;

namespace SURIMI_fisheries_authority.Services
{
    /// <summary>
    /// Loads the stock recruitment configuration for a scenario from a narrow-format CSV file:
    /// comma-delimited, dot as decimal separator, one row per species with the columns
    /// "species_code,life_stage,RstockRatio,RHalfB0Ratio,cvRec".
    /// </summary>
    public class RecruitmentLoader
    {
        private const char Delimiter = ',';
        private const int ColumnCount = 5;
        private readonly IBlobStore _blobStore;
        private readonly ILogger<RecruitmentLoader> _logger;

        public RecruitmentLoader(IBlobStore blobStore, ILogger<RecruitmentLoader> logger)
        {
            _blobStore = blobStore;
            _logger = logger;
        }

        public async Task<StockRecruitmentMap> LoadAsync(string scenarioName, CancellationToken cancellationToken = default)
        {
            string fileNamePath = $"{scenarioName}/{scenarioName}_recruitment.csv";
            if (!await _blobStore.ExistsAsync(fileNamePath, PathType.Input, cancellationToken))
            {
                throw new RpcException(new Status(StatusCode.Internal, $"Couldn't find {fileNamePath}"));
            }

            var text = await _blobStore.ReadAllTextAsync(fileNamePath, PathType.Input, cancellationToken);
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            if (lines.Length == 0)
            {
                throw new InvalidDataException($"Recruitment file {fileNamePath} is empty");
            }

            ValidateHeader(lines[0], fileNamePath);
            var map = new StockRecruitmentMap();

            for (int iLine = 1; iLine < lines.Length; iLine++)
            {
                if (string.IsNullOrWhiteSpace(lines[iLine]))
                {
                    continue;
                }

                ParseRow(lines[iLine], iLine + 1, map, fileNamePath);
            }

            _logger.LogInformation("Loaded {SpeciesCount} species recruitment configurations for scenario {ScenarioName}", map.SpeciesCount, scenarioName);
            return map;
        }

        private static void ValidateHeader(string headerLine, string filePath)
        {
            var cells = headerLine.Split(Delimiter);
            if (cells.Length < ColumnCount
                || !string.Equals(cells[0].Trim(), "species_code", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(cells[1].Trim(), "life_stage", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(cells[2].Trim(), "RstockRatio", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(cells[3].Trim(), "RHalfB0Ratio", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(cells[4].Trim(), "cvRec", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Recruitment file {filePath} has an invalid header; expected 'species_code,life_stage,RstockRatio,RHalfB0Ratio,cvRec'");
            }
        }

        private static void ParseRow(string line, int lineNumber, StockRecruitmentMap map, string filePath)
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

            float rstockRatio = ParseValue(cells[2], "RstockRatio", speciesCode, lifeStage, lineNumber, filePath);
            float rHalfB0Ratio = ParseValue(cells[3], "RHalfB0Ratio", speciesCode, lifeStage, lineNumber, filePath);
            float cvRec = ParseValue(cells[4], "cvRec", speciesCode, lifeStage, lineNumber, filePath);

            if (!map.Add(new SpeciesKey(speciesCode, lifeStage), new StockRecruitmentConfiguration(rstockRatio, rHalfB0Ratio, cvRec)))
            {
                throw new InvalidDataException($"Duplicate species ({speciesCode}, {lifeStage}) on line {lineNumber} of {filePath}");
            }
        }

        private static float ParseValue(string cell, string columnName, string speciesCode, string lifeStage, int lineNumber, string filePath)
        {
            var trimmed = cell.Trim();
            if (!float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                throw new InvalidDataException($"Invalid {columnName} value '{trimmed}' for species ({speciesCode}, {lifeStage}) on line {lineNumber} of {filePath}");
            }

            return value;
        }
    }
}
