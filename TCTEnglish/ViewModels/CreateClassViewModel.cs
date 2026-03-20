namespace TCTVocabulary.ViewModels
{
    public class CreateClassViewModel
    {
        public string ClassName { get; set; } = null!;
        public string? Description { get; set; }
        public string? Password { get; set; }     // mật khẩu thô
        public IFormFile? Avatar { get; set; }
    }
}
