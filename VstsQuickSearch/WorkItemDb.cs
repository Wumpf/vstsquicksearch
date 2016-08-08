using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VstsQuickSearch
{
    public class SearchableWorkItemDatabase : INotifyPropertyChanged
    {
        private List<SearchableWorkItem> itemDatabase = new List<SearchableWorkItem>();

        public event PropertyChangedEventHandler PropertyChanged;

        public int NumWorkItems { get { return itemDatabase.Count; } }

        public List<WorkItemFieldReference> LastQueryColumnDisplay { get; private set; }

        public IEnumerable<SearchableWorkItem> SearchInDatabase(SearchQuery search)
        {
            lock (itemDatabase)
            {
                if (search.IsEmpty)
                    return itemDatabase;
                else
                    return itemDatabase.AsParallel().Where(x => x.MatchesSearchQuery(search));
            }
        }

        public async Task DownloadData(ServerConnection connection, Guid queryId, bool downloadComments, Action<float> progressCallback)
        {
            // run the 'REST Sample' query
            WorkItemQueryResult result = await connection.WorkItemClient.QueryByIdAsync(queryId);
            LastQueryColumnDisplay = result.Columns.ToList();

            List<SearchableWorkItem> newDatabase = new List<SearchableWorkItem>();

            const int batchSize = 100;

            int totalNumWorkItems = (result.WorkItems?.Count() ?? 0) + (result.WorkItemRelations?.Count() ?? 0);

            if (totalNumWorkItems != 0)
            {
                IEnumerable<int> workItemIds = Enumerable.Empty<int>();
                if (result.WorkItems != null)
                    workItemIds = result.WorkItems.Select(x => x.Id);
                if (result.WorkItemRelations != null)
                    workItemIds = workItemIds.Concat(result.WorkItemRelations.Where(x => x.Target != null).Select(x => x.Target.Id));

                List<int> workItemIdsList = workItemIds.ToList(); // To ensure the Linq expression is not unnecessarily often evaluated;

                int skip = 0;
                IEnumerable<int> workItemIdBatch;
                do
                {
                    workItemIdBatch = workItemIdsList.Skip(skip).Take(batchSize);
                    if (workItemIdBatch.Any())
                    {
                        // get details for each work item in the batch
                        List<WorkItem> workItems = await connection.WorkItemClient.GetWorkItemsAsync(workItemIdBatch);
                        foreach (WorkItem workItem in workItems)
                        {
                            newDatabase.Add(new SearchableWorkItem
                            {
                                WorkItem = workItem,
                                History = downloadComments ? (await connection.WorkItemClient.GetHistoryAsync(workItem.Id.Value)) : null
                            });
                            progressCallback((float)newDatabase.Count / totalNumWorkItems);
                        }
                    }
                    skip += batchSize;
                }
                while (workItemIdBatch.Count() == batchSize);
            }

            lock (itemDatabase)
            {
                itemDatabase = newDatabase;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NumWorkItems)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastQueryColumnDisplay)));
        }
    }
}
