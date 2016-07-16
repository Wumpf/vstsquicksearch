﻿using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using System.Collections.Generic;
using System.Linq;

namespace VstsQuickSearch
{
    public class SearchableWorkItem
    {
        public bool MatchesSearchQuery(SearchQuery query)
        {
            return WorkItem.Fields.Values.OfType<string>().Any(x => query.Matches(x)) ||
                    (History?.Any(x => query.Matches(x.Value)) ?? false);
        }

        public override string ToString()
        {
            string title = "[No Title]";
            WorkItem.Fields.TryGetValue("System.Title", out title);
            return string.Format("{0} - {1}", WorkItem.Id, title);
        }

        public string this[string name]
        {
            get
            {
                if (name == "System.Id")
                    return WorkItem.Id.ToString();
                else
                {
                    object value = null;
                    if (WorkItem.Fields.TryGetValue(name, out value) && value != null)
                        return value.ToString();
                    else
                        return "-";
                }
            }
        }

        public WorkItem WorkItem;
        public List<WorkItemHistory> History;
    }
}
