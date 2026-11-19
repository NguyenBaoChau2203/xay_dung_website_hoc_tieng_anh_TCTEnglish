using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TCTEnglish.Services.Listening
{
    /// <summary>
    /// The word-form rule of the Listening page: a word and its simple de-inflections. This is an exact mirror of
    /// <c>baseForms</c> in wwwroot/js/listening-core.js; both are checked against Fixtures/listening-base-forms.json.
    /// </summary>
    public static class ListeningWordForms
    {
        private static readonly Regex PossessiveEnding = new("'s$", RegexOptions.Compiled);
        private static readonly Regex IesEnding = new("ies$", RegexOptions.Compiled);
        private static readonly Regex EsEnding = new("(?:ch|sh|ss|x|z|o)es$", RegexOptions.Compiled);
        private static readonly Regex SEnding = new("[^s']s$", RegexOptions.Compiled);
        private static readonly Regex IedEnding = new("ied$", RegexOptions.Compiled);
        private static readonly Regex EdEnding = new("ed$", RegexOptions.Compiled);
        private static readonly Regex IngEnding = new("ing$", RegexOptions.Compiled);
        private static readonly Regex DoubledConsonant = new("([bdgmnprt])\\1$", RegexOptions.Compiled);

        /// <summary>Lower-case, NFC, curly apostrophes turned into a straight one.</summary>
        public static string Key(string? word)
            => (word ?? string.Empty).Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant()
                .Replace('‘', '\'').Replace('’', '\'').Replace('ʼ', '\'');

        /// <summary>The word itself first, then its de-inflections: unique, at most six.</summary>
        public static IReadOnlyList<string> BaseForms(string? input)
        {
            var key = Key(input);
            var forms = new List<string>();

            void Add(string form)
            {
                if (form.Length >= 2 && !string.IsNullOrWhiteSpace(form) && !forms.Contains(form)) forms.Add(form);
            }

            void Doubled(string stem)
            {
                if (DoubledConsonant.IsMatch(stem)) Add(stem[..^1]);
            }

            Add(key);
            var w = key;
            if (key.Length > 3 && PossessiveEnding.IsMatch(key))
            {
                w = key[..^2];
                Add(w);
            }

            var n = w.Length;
            if (n > 4 && IesEnding.IsMatch(w)) Add(w[..^3] + "y");
            if (n > 4 && EsEnding.IsMatch(w)) Add(w[..^2]);
            if (n > 3 && SEnding.IsMatch(w)) Add(w[..^1]);
            if (n > 4 && IedEnding.IsMatch(w)) Add(w[..^3] + "y");
            if (n > 4 && EdEnding.IsMatch(w))
            {
                var stem = w[..^2];
                Add(stem);
                Add(w[..^1]);
                Doubled(stem);
            }

            if (n > 5 && IngEnding.IsMatch(w))
            {
                var stem = w[..^3];
                Add(stem);
                Add(stem + "e");
                Doubled(stem);
            }

            return forms.Count > 6 ? forms.GetRange(0, 6) : forms;
        }

        /// <summary>True when the two words share a base form (the same test the page uses to flag lesson vocabulary).</summary>
        public static bool SameWord(string? a, string? b)
        {
            var left = BaseForms(a);
            var right = BaseForms(b);
            foreach (var form in left)
            {
                if (right.Contains(form)) return true;
            }

            return false;
        }
    }
}
