using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VstsQuickSearch
{
    class SearchableWorkItemDatabase
    {
        private List<SearchableWorkItem> itemDatabase = new List<SearchableWorkItem>();

        public int NumWorkItems { get { return itemDatabase.Count; } }

        public IEnumerable<SearchableWorkItem> SearchInDatabase(SearchQuery search, CancellationToken cancellationToken)
        {
            lock (itemDatabase)
            {
                if (search.IsEmpty)
                    return itemDatabase;
                else
                    return itemDatabase.AsParallel().WithCancellation(cancellationToken).Where(x => x.MatchesSearchQuery(search));
            }
        }

        public async Task DownloadData(ServerConnection connection, Guid queryId, bool downloadComments)
        {
            // run the 'REST Sample' query
            WorkItemQueryResult result = await connection.WorkItemClient.QueryByIdAsync(queryId);

            List<SearchableWorkItem> newDatabase = new List<SearchableWorkItem>();

            int skip = 0;
            const int batchSize = 1000;
            IEnumerable<WorkItemReference> workItemRefs;
            do
            {
                workItemRefs = result.WorkItems.Skip(skip).Take(batchSize);
                if (workItemRefs.Any())
                {
                    // get details for each work item in the batch
                    List<WorkItem> workItems = await connection.WorkItemClient.GetWorkItemsAsync(workItemRefs.Select(wir => wir.Id));
                    foreach (WorkItem workItem in workItems)
                    {
                        newDatabase.Add(new SearchableWorkItem
                        {
                            Item = workItem,
                            History = downloadComments ? (await connection.WorkItemClient.GetHistoryAsync(workItem.Id.Value)) : null
                        });
                    }
                }
                skip += batchSize;
            }
            while (workItemRefs.Count() == batchSize);


            lock (itemDatabase)
            {
                itemDatabase = newDatabase;
            }
        }
    }
}
