namespace TCTEnglish.Services.Reading;

// Missing and inaccessible resources deliberately share the same HTTP outcome.
public sealed class ReadingNotFoundException() : InvalidOperationException("Reading resource not found.");
