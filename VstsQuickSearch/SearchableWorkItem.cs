﻿using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using System.Collections.Generic;
using System.Linq;

namespace VstsQuickSearch
{
    public class SearchableWorkItem
    {
        public SearchableWorkItem(WorkItem workItem, List<WorkItemHistory> history)
        {
            this.history = history;
            fields = workItem.Fields.ToDictionary(x => x.Key, x => x.Value.ToString());
            fields.TryAdd("System.Id", workItem.Id.ToString());

            Id = workItem.Id ?? -1;

            stringsToSearch = fields.Values;
            if (history != null)
                stringsToSearch = stringsToSearch.Concat(history.Select(x => x.Value));
        }

        public bool MatchesSearchQuery(SearchQuery query)
        {
            return query.Matches(stringsToSearch);
        }

        public override string ToString()
        {
            string title = "[No Title]";
            fields.TryGetValue("System.Title", out title);

            string id = "-";
            fields.TryGetValue("System.Id", out id);

            return string.Format("{0} - {1}", id, title);
        }

        public string this[string name]
        {
            get
            {
                string value;
                if (fields.TryGetValue(name, out value))
                    return value;
                else
                    return "-";
            }
        }

        public int Id { get; private set; }
        private Dictionary<string, string> fields;
        private List<WorkItemHistory> history;
        private IEnumerable<string> stringsToSearch;
    }
}
