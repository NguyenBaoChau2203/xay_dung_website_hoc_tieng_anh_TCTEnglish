namespace TCTEnglish.Services.AI.Internal;

public sealed class MlNetIntentDatasetLoader
{
    private readonly MlNetIntentClassifierAssetResolver _assetResolver;

    public MlNetIntentDatasetLoader(MlNetIntentClassifierAssetResolver assetResolver)
    {
        _assetResolver = assetResolver;
    }

    public async Task<IReadOnlyList<MlNetIntentDatasetExample>> LoadSeedDatasetAsync(CancellationToken ct)
    {
        var snapshot = _assetResolver.ResolveSnapshot();
        if (!snapshot.SeedDatasetExists)
        {
            throw new FileNotFoundException(
                "ML.NET seed dataset was not found.",
                snapshot.SeedDatasetAbsolutePath);
        }

        var lines = await File.ReadAllLinesAsync(snapshot.SeedDatasetAbsolutePath, ct);
        return ParseLines(lines, snapshot.SeedDatasetAbsolutePath);
    }

    public static IReadOnlyList<MlNetIntentDatasetExample> ParseLines(IEnumerable<string> lines, string sourceName)
    {
        var rows = new List<MlNetIntentDatasetExample>();
        var lineNumber = 0;

        foreach (var rawLine in lines)
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            if (lineNumber == 1 && line.Equals("Text,Label", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separatorIndex = line.LastIndexOf(',');
            if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
            {
                throw new FormatException($"Invalid CSV row at line {lineNumber} in '{sourceName}'. Expected 'Text,Label'.");
            }

            var text = line[..separatorIndex].Trim();
            var label = line[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new FormatException($"Empty Text value at line {lineNumber} in '{sourceName}'.");
            }

            if (!Enum.TryParse<UserIntent>(label, ignoreCase: true, out var intent))
            {
                throw new FormatException($"Unknown label '{label}' at line {lineNumber} in '{sourceName}'.");
            }

            rows.Add(new MlNetIntentDatasetExample(text, intent));
        }

        return rows;
    }
}
