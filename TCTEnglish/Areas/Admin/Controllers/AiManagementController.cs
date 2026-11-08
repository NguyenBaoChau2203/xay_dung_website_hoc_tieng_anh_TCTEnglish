using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Internal;
using TCTEnglish.Services.AI.Rag.Corpus;
using TCTVocabulary.Areas.Admin.ViewModels;
using TCTVocabulary.Models;

namespace TCTVocabulary.Areas.Admin.Controllers;

/// <summary>
/// Admin controller for managing the ML.NET intent classifier model.
/// Provides model/dataset status overview and one-click training with hot-reload.
/// </summary>
[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class AiManagementController : Controller
{
    private readonly MlNetIntentClassifierAssetResolver _assetResolver;
    private readonly MlNetIntentDatasetLoader _datasetLoader;
    private readonly IMlNetTrainerService _trainerService;
    private readonly MlNetAiQueryClassifier _classifier;
    private readonly IRetrievalIndexService _retrievalIndex;
    private readonly IVectorIndexService _vectorIndex;
    private readonly ILogger<AiManagementController> _logger;

    public AiManagementController(
        MlNetIntentClassifierAssetResolver assetResolver,
        MlNetIntentDatasetLoader datasetLoader,
        IMlNetTrainerService trainerService,
        MlNetAiQueryClassifier classifier,
        IRetrievalIndexService retrievalIndex,
        IVectorIndexService vectorIndex,
        ILogger<AiManagementController> logger)
    {
        _assetResolver = assetResolver;
        _datasetLoader = datasetLoader;
        _trainerService = trainerService;
        _classifier = classifier;
        _retrievalIndex = retrievalIndex;
        _vectorIndex = vectorIndex;
        _logger = logger;
    }

    /// <summary>
    /// GET: /Admin/AiManagement
    /// Displays model and dataset status information.
    /// </summary>
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var snapshot = _assetResolver.ResolveSnapshot();

        var viewModel = new AiManagementViewModel
        {
            ModelExists = snapshot.ModelArtifactExists,
            ModelPath = snapshot.ModelArtifactAbsolutePath,
            DatasetExists = snapshot.SeedDatasetExists,
            DatasetPath = snapshot.SeedDatasetAbsolutePath
        };

        // Populate model file metadata when artifact exists on disk
        if (snapshot.ModelArtifactExists)
        {
            try
            {
                var modelFileInfo = new FileInfo(snapshot.ModelArtifactAbsolutePath);
                viewModel.ModelFileSizeBytes = modelFileInfo.Length;
                viewModel.ModelLastModifiedUtc = modelFileInfo.LastWriteTimeUtc;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not read model file metadata at '{ModelPath}'.",
                    snapshot.ModelArtifactAbsolutePath);
            }
        }

        // Populate dataset sample count when seed file exists
        if (snapshot.SeedDatasetExists)
        {
            try
            {
                var examples = await _datasetLoader.LoadSeedDatasetAsync(ct);
                viewModel.DatasetSampleCount = examples.Count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not load seed dataset at '{DatasetPath}'.",
                    snapshot.SeedDatasetAbsolutePath);
            }
        }

        var retrieval = _retrievalIndex.ReadStatus();
        viewModel.RetrievalIndexState = retrieval.Availability.ToString().ToLowerInvariant();
        viewModel.RetrievalSnapshotId = retrieval.SnapshotId;
        viewModel.RetrievalDocumentCount = retrieval.DocumentCount;
        viewModel.RetrievalChunkCount = retrieval.ChunkCount;
        viewModel.RetrievalPromotedAtUtc = retrieval.PromotedAtUtc;
        viewModel.RetrievalOperationMessage = TempData["RetrievalIndexResult"] as string;
        var vectors = _vectorIndex.ReadStatus();
        viewModel.VectorIndexState = vectors.State;
        viewModel.VectorIndexDetail = vectors.Detail;
        viewModel.VectorIndexId = vectors.IndexId;
        viewModel.VectorChunkCount = vectors.ChunkCount;
        viewModel.VectorModel = vectors.Model;
        viewModel.VectorDimensions = vectors.Dimensions;
        viewModel.VectorOperationMessage = TempData["VectorIndexResult"] as string;

        return View(viewModel);
    }

    /// <summary>
    /// POST: /Admin/AiManagement/TrainModel
    /// Triggers ML.NET training, hot-reloads the classifier, and redirects back with results.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TrainModel(CancellationToken ct)
    {
        try
        {
            var result = await _trainerService.TrainAndSaveModelAsync(ct);

            if (result.Success)
            {
                // Hot-reload: invalidate the cached model so next prediction uses the new artifact
                _classifier.InvalidateModel();
            }

            TempData["TrainingResult"] = JsonSerializer.Serialize(result);
        }
        catch (OperationCanceledException)
        {
            throw; // Let ASP.NET Core handle request cancellation normally
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during ML.NET training.");
            TempData["TrainingError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// POST: /Admin/AiManagement/RebuildRetrievalIndex
    /// Explicit public-corpus maintenance. dry-run and build stay lexical.
    /// embed-dry-run estimates cache and tokens. embed is the only operation that may call the embeddings API,
    /// and only inside the posted non-secret budget. A path field is ignored. This is not TrainModel.
/// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RebuildRetrievalIndex(CancellationToken ct)
    {
        try
        {
            if (!TrySelectOperation(out var operation))
            {
                TempData["RetrievalIndexResult"] = "rejected: missing-or-invalid-operation";
                return RedirectToAction(nameof(Index));
            }

            if (operation is "dry-run" or "build")
            {
                var result = operation == "dry-run"
                    ? await _retrievalIndex.DryRunAsync(ct)
                    : await _retrievalIndex.BuildAsync(requestedRoot: null, ct);
                TempData["RetrievalIndexResult"] = result.Status + ": " + result.Message
                    + $" documents={result.DocumentCount} chunks={result.ChunkCount} exclusions={result.ExclusionCount}";
                return RedirectToAction(nameof(Index));
            }

            if (operation == "embed-dry-run")
            {
                var preview = await _vectorIndex.DryRunAsync(ct);
                TempData["VectorIndexResult"] = FormatVector(preview);
                return RedirectToAction(nameof(Index));
            }

            if (!TryReadBudget(out var budget, out var budgetError) || budget is null)
            {
                TempData["VectorIndexResult"] = "rejected: " + budgetError;
                return RedirectToAction(nameof(Index));
            }

            var embedded = await _vectorIndex.BuildAsync(budget, ct);
            TempData["VectorIndexResult"] = FormatVector(embedded);
            _logger.LogInformation(
                "Admin vector maintenance {Operation} finished with {Status}.",
                operation,
                embedded.Status);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retrieval index maintenance failed before promotion.");
            TempData["RetrievalIndexResult"] = "failed: " + ex.GetType().Name;
        }

        return RedirectToAction(nameof(Index));
    }

    private static string FormatVector(VectorIndexOperationResult result)
    {
        var usage = result.PromptUsageComplete
            ? result.ActualPromptTokens.ToString(CultureInfo.InvariantCulture)
            : "unknown";
        return result.Status + ": " + result.Message
            + $" chunks={result.ChunkCount} cacheHits={result.CacheHits} cacheMisses={result.CacheMisses}"
            + $" estimatedTokens={result.EstimatedInputTokens} safeTokens={result.SafeInputTokenUpperBound}"
            + $" estimatedRequests={result.EstimatedRequests} apiCalls={result.ApiCalls} usage={usage}";
    }

    private bool TrySelectOperation(out string operation)
    {
        operation = string.Empty;
        if (!Request.HasFormContentType)
        {
            return false;
        }

        var selected = Request.Form["operation"]
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (selected.Count != 1)
        {
            return false;
        }

        if (selected[0] is "dry-run" or "build" or "embed-dry-run" or "embed")
        {
            operation = selected[0]!;
            return true;
        }

        return false;
    }

    private bool TryReadBudget(out EmbeddingBudget? budget, out string error)
    {
        budget = null;
        error = "budget";
        if (!Request.HasFormContentType || !TrySingleInt("maxRequests", out var requests) || !TrySingleInt("maxInputTokens", out var tokens))
        {
            return false;
        }

        return EmbeddingBudget.TryCreate(requests, tokens, out budget, out error);
    }

    private bool TrySingleInt(string name, out int value)
    {
        value = 0;
        var selected = Request.Form[name]
            .Select(item => item?.Trim())
            .Where(item => !string.IsNullOrEmpty(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return selected.Count == 1
            && int.TryParse(selected[0], NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
