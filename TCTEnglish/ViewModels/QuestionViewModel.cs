using TCTEnglish.ViewModels;

public class QuestionViewModel
{
    public int Id { get; set; }

    public string QuestionText { get; set; }

    public List<OptionViewModel> Options { get; set; }
}