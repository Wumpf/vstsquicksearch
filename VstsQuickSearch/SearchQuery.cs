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

        public bool Matches(string stringToCheck)
        {
            if (string.IsNullOrEmpty(stringToCheck))
                return false;

            return words.All(word => stringToCheck.IndexOf(word, StringComparison.InvariantCultureIgnoreCase) >= 0);
        }
    }
}
