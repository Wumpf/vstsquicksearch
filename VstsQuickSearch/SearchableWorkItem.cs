using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.TeamFoundation.Discussion.Client;
using System.Threading.Tasks;
using System.Text;

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
                try
                {
                    if (name == "System.Id")
                        return WorkItem.Id.ToString();
                    else
                        return WorkItem.Fields[name]?.ToString() ?? "-";
                }
                catch
                {
                    return "-";
                }
            }
        }

        public WorkItem WorkItem;
        public List<WorkItemHistory> History;
    }
}
