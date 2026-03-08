namespace TCTVocabulary.Models
{
    public class ProfileViewModel
    {
        public UpdateProfileViewModel UpdateProfile { get; set; } = new UpdateProfileViewModel();
        public ChangePasswordViewModel ChangePassword { get; set; } = new ChangePasswordViewModel();
        public string ActiveTab { get; set; } = "profile";
    }
}
