namespace TCTEnglish.Services.AI.Rag.Corpus;

internal static class CorpusFilePromotion
{
    public static async Task RunAsync(Action promote, CancellationToken ct, bool directoryMove = false)
    {
        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { promote(); return; }
            catch (IOException ex) when (OperatingSystem.IsWindows() && attempt < 7
                && ((ex.HResult & 0xffff) is 32 or 33 || (directoryMove && (ex.HResult & 0xffff) == 5)))
            {
                // Directory.Move reports an open child without delete sharing as
                // native error 5; file replacement reports 32/33. Never change ACLs.
                // Never retry provider work or delete the currently published index.
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), ct);
            }
        }
    }
}
