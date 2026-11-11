using System.ComponentModel.DataAnnotations;

namespace TCTEnglish.ViewModels.Sets;

public sealed class SetFolderOption
{
    public int FolderId { get; set; }
    public string FolderName { get; set; } = string.Empty;
}

public sealed class SetInfoFormViewModel
{
    public int? SetId { get; set; }

    [Required(ErrorMessage = "Nhập tên bộ thẻ")]
    [StringLength(255, ErrorMessage = "Tối đa 255 ký tự")]
    public string SetName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Tối đa 500 ký tự")]
    public string? Description { get; set; }

    public string Visibility { get; set; } = "Private";

    public int? FolderId { get; set; }

    public List<SetFolderOption> AvailableFolders { get; set; } = new();

    public int WordCount { get; set; }

    public string CoverHue { get; set; } = "primary";
}

public sealed class SetCardTileViewModel
{
    public int SetId { get; set; }
    public string SetName { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public string Visibility { get; set; } = "Public";
    public bool HasShareLink { get; set; }
    public string CoverHue { get; set; } = "primary";
    public string SampleTerm { get; set; } = string.Empty;
    public int WordCount { get; set; }
    public int DueCount { get; set; }
    public int NewCount { get; set; }
    public int ProgressPercent { get; set; }

    /// <summary>own | subscribed | shared</summary>
    public string Relation { get; set; } = "own";
}

public sealed class SetIndexViewModel
{
    public string? Filter { get; set; }
    public List<SetCardTileViewModel> Own { get; set; } = new();
    public List<SetCardTileViewModel> Subscribed { get; set; } = new();
    public List<SetCardTileViewModel> Shared { get; set; } = new();
    public bool IsEmpty => Own.Count == 0 && Subscribed.Count == 0 && Shared.Count == 0;
}

public sealed class StudyStripViewModel
{
    public int MasteredPercent { get; set; }
    public int DueCount { get; set; }
    public int WordCount { get; set; }
}

public sealed class SetMoveTarget
{
    public int SetId { get; set; }
    public string SetName { get; set; } = string.Empty;
}

public sealed class SetDetailViewModel
{
    public int SetId { get; set; }
    public int UserId { get; set; }
    public string SetName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Visibility { get; set; } = "Public";
    public string CoverHue { get; set; } = "primary";
    public string SampleTerm { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public int? FolderId { get; set; }
    public string? FolderName { get; set; }
    public bool IsOwner { get; set; }
    public bool CanEdit { get; set; }
    public bool CanManage { get; set; }
    public bool IsSubscribed { get; set; }
    public bool IsStudying { get; set; }
    public bool HasShareLink { get; set; }
    public string? ShareSlug { get; set; }
    public string? ShareUrl { get; set; }
    public int WordCount { get; set; }
    public int DueCount { get; set; }
    public int MasteredPercent { get; set; }
    public bool IsPremium { get; set; }
    public List<string> Topics { get; set; } = new();
    public List<SetMoveTarget> MoveTargets { get; set; } = new();
    public StudyStripViewModel StudyStrip { get; set; } = new();
}

public sealed class SharedCardPreview
{
    public int CardId { get; set; }
    public string Term { get; set; } = string.Empty;
    public string? Phonetic { get; set; }
    public string Definition { get; set; } = string.Empty;
}

public sealed class SharedSetViewModel
{
    public int SetId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string SetName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CoverHue { get; set; } = "primary";
    public string SampleTerm { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public int WordCount { get; set; }
    public int LearnerCount { get; set; }
    public bool IsSignedIn { get; set; }
    public bool IsOwner { get; set; }
    public bool IsSubscribed { get; set; }
    public List<SharedCardPreview> Cards { get; set; } = new();
}

public sealed class CardRelatedLink
{
    public string Text { get; set; } = string.Empty;
    public int? CardId { get; set; }
}

public sealed class CardProgressViewModel
{
    public string StatusKey { get; set; } = "new";
    public string StatusLabel { get; set; } = "Mới";
    public DateTime? LastReviewedDate { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public int WrongCount { get; set; }
    public int RepetitionCount { get; set; }
    public bool IsSuspended { get; set; }
    public bool HasReviewed { get; set; }
}

public sealed class CardDetailViewModel
{
    public int CardId { get; set; }
    public int SetId { get; set; }
    public string SetName { get; set; } = string.Empty;
    public bool CanEdit { get; set; }
    public bool IsSetOwner { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string? Phonetic { get; set; }
    public string? PhoneticUk { get; set; }
    public string? MeaningEn { get; set; }
    public List<string> PartsOfSpeech { get; set; } = new();
    public string? Topic { get; set; }
    public string? Cefr { get; set; }
    public string? Emoji { get; set; }
    public string? ImageUrl { get; set; }
    public CardImageSource? ImageSource { get; set; }
    public string? PersonalNote { get; set; }
    public CardDetailsJson Details { get; set; } = new();
    public List<CardRelatedLink> RelatedSynonyms { get; set; } = new();
    public List<CardRelatedLink> RelatedAntonyms { get; set; } = new();
    public CardProgressViewModel Progress { get; set; } = new();
}

public sealed class CardFormViewModel : CardInputDto
{
    public int? CardId { get; set; }
    public int SetId { get; set; }
    public string SetName { get; set; } = string.Empty;
    public bool IsPremium { get; set; }
    public Dictionary<string, string> FieldErrors { get; set; } = new();
}

public sealed class CardImportContextViewModel
{
    public int SetId { get; set; }
    public string SetName { get; set; } = string.Empty;
    public bool IsPremium { get; set; }
    public List<string> ExistingTermsLower { get; set; } = new();
}

public sealed class CardComposeViewModel
{
    public int SetId { get; set; }
    public int UserId { get; set; }
    public string SetName { get; set; } = string.Empty;
    public bool IsPremium { get; set; }
    public string? InitialTerm { get; set; }
    public List<SetMoveTarget> Targets { get; set; } = new();
    public List<string> Topics { get; set; } = new();
}

public sealed class SetExportFile
{
    public string FileName { get; set; } = "bo-the.csv";
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public sealed class VocabEnrichResultDto
{
    public bool IsValidWord { get; set; } = true;
    public string? DidYouMean { get; set; }
    public string? Lemma { get; set; }
    public string? IpaUs { get; set; }
    public string? IpaUk { get; set; }
    public string? Cefr { get; set; }
    public List<VocabEnrichSense> Senses { get; set; } = new();
    public List<string> Synonyms { get; set; } = new();
    public List<string> Antonyms { get; set; } = new();
    public List<CardCollocation> Collocations { get; set; } = new();
    public List<CardWordFamilyItem> WordFamily { get; set; } = new();
    public CardVerbForms? VerbForms { get; set; }
    public string? Plural { get; set; }
    public string? TopicVi { get; set; }
    public string? Emoji { get; set; }
    public string? ImageQuery { get; set; }
    public string? MemoryTipVi { get; set; }
}

public sealed class VocabEnrichSense
{
    public List<string> Pos { get; set; } = new();
    public string? MeaningVi { get; set; }
    public string? MeaningEn { get; set; }
    public List<CardExample> Examples { get; set; } = new();
}
