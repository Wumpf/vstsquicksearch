using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Client;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VstsQuickSearch
{
    public class ServerConnection
    {
        public class ConnectionSettings : IEquatable<ConnectionSettings>
        {
            public bool Equals(ConnectionSettings other)
            {
                return ServerInstance == other.ServerInstance && 
                        ProjectName == other.ProjectName && 
                        Collection == other.Collection;
            }

            public string ServerInstance { get; set; } = "name.visualstudio.com";
            public string ProjectName { get; set; } = "Project Name";
            public string Collection { get; set; } = "defaultcollection";
        }

        private ConnectionSettings settings;
        private VssConnection connection;

        public WorkItemTrackingHttpClient WorkItemClient { get; private set; }

        private Uri GetCollectionUri()
        {
            System.Diagnostics.Debug.Assert(settings != null);
            return new Uri("https://" + settings.ServerInstance + "/" + settings.Collection);
        }

        public string GetWorkItemUrl(int id)
        {
            System.Diagnostics.Debug.Assert(settings != null);
            return string.Format("https://{0}/{1}/_workitems?id={2}", settings.ServerInstance, settings.ProjectName, id);
        }

        public async Task Connect(ConnectionSettings settings)
        {
            if (connection != null && this.settings.Equals(settings))
                return;

            if (connection != null)
            {
                try
                {
                    connection.Disconnect();
                }
                catch { }
            }

            this.settings = settings;

            // May bring up a dialog for credentials.
            var credentials = new VssClientCredentials(true);
            credentials.PromptType = Microsoft.VisualStudio.Services.Common.CredentialPromptType.PromptIfNeeded;
            credentials.Storage = new VssClientCredentialStorage(); // Using the standard credential storage to cache credentials. I trust this is safe...
            connection = new VssConnection(GetCollectionUri(), credentials);
            
            // Create instance of WorkItemTrackingHttpClient using VssConnection
            WorkItemClient = await connection.GetClientAsync<WorkItemTrackingHttpClient>();
        }

        public async Task<List<QueryHierarchyItem>> ListQueries()
        {
            System.Diagnostics.Debug.Assert(WorkItemClient != null);

            // Get 2 levels of query hierarchy items.
            // According to this this is as deep as we can go
            // https://blog.joergbattermann.com/2016/05/05/vsts-tfs-rest-api-06-retrieving-and-querying-for-existing-work-items/
            var queryList = await WorkItemClient.GetQueriesAsync(settings.ProjectName, expand: QueryExpand.All, depth: 2, includeDeleted: false);

            foreach (var query in queryList)
                await GetSubsubsubqueries(query);

            return queryList;
        }

        private async Task GetSubsubsubqueries(QueryHierarchyItem topQuery)
        {
            if (topQuery.Children == null)
                return;


            foreach (var query in topQuery.Children)
            {
                if (query.Children == null)
                    continue;

                for (int c = 0; c < query.Children.Count; ++c)
                {
                    if ((query.Children[c].HasChildren ?? false) && (query.Children[c].Children == null || query.Children[c].Children.Count == 0))
                    {
                        query.Children[c] = await WorkItemClient.GetQueryAsync(settings.ProjectName, query.Children[c].Path, expand: QueryExpand.All, depth: 2, includeDeleted: false);
                        await GetSubsubsubqueries(query.Children[c]);
                    }
                }
            }
        }
    }
}

// Retrieve stored Queries (which you / your authenticated user can see and access), up to 2 (sub-)levels or -hierarchies deep.
// .. it appears that (currently?) you can specify a max value of '2' for the 'depth' parameter which means you might need to 
// retrieve queries deeper in the hierarchy using another approach:
// > check the corresponding QueryHierarchyItem for its .HasChildren having a Value (.HasValue==true) and that Value being 'true' BUT
// .. the .Children being 'null'. Go ahead and use that QueryHierarchyItem's .Path value for the .GetQueryAsync(projectId, queryHierarchyItemPath, ...) method
// .. to drill down further into the hierarchy