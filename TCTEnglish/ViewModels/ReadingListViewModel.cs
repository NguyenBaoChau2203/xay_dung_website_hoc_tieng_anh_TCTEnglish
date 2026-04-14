namespace TCTEnglish.ViewModels
{
    public class ReadingListViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; }

        public string ImageUrl { get; set; }

        public string Level { get; set; }   // A1 A2 B1 ...

        public bool IsCompleted { get; set; }

        public bool IsInProgress { get; set; }
    }
}
