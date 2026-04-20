namespace TCTEnglish.Services.AI.Internal;

public interface IMlNetTrainerService
{
    Task<MlNetTrainingResult> TrainAndSaveModelAsync(CancellationToken ct);
}
