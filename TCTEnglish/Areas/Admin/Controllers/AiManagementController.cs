using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Services.AI.Internal;
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
    private readonly ILogger<AiManagementController> _logger;

    public AiManagementController(
        MlNetIntentClassifierAssetResolver assetResolver,
        MlNetIntentDatasetLoader datasetLoader,
        IMlNetTrainerService trainerService,
        MlNetAiQueryClassifier classifier,
        ILogger<AiManagementController> logger)
    {
        _assetResolver = assetResolver;
        _datasetLoader = datasetLoader;
        _trainerService = trainerService;
        _classifier = classifier;
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
}
