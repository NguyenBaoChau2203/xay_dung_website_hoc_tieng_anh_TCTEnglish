
using System.Collections.Generic;
using TCTVocabulary.Models;
namespace TCTVocabulary.Models.ViewModels
{
    public class FolderDetailViewModel
    {
        public Folder Folder { get; set; } = null!;
        public List<Set> Sets { get; set; } = new();
    }
}