using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VstsQuickSearch
{
    public struct SearchQuery
    {
        private string[] words;

        public bool IsEmpty { get { return words.Length == 0; } }

        public SearchQuery(string text)
        {
            // todo, something more elaborate
            words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public bool Matches(IEnumerable<string> stringsToCheck)
        {
            return words.All(word => stringsToCheck.Any(stringToCheck => stringToCheck.IndexOf(word, StringComparison.InvariantCultureIgnoreCase) >= 0));
        }
    }
}
