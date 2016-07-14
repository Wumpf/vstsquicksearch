using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VstsQuickSearch
{
    class ServerConnection
    {
        private string serverInstance;
        private string collection = "defaultcollection";
        private string projectName;

        private VssConnection connection;

        public WorkItemTrackingHttpClient WorkItemClient { get; private set; }

        private Uri GetCollectionUri()
        {
            return new Uri("https://" + serverInstance + "/" + collection);
        }

        public string GetWorkItemUrl(int id)
        {
            return string.Format("https://{0}/{1}/_workitems?id={2}", serverInstance, projectName, id);
        }

        public async Task Connect(string serverInstance, string projectName)
        {
            if (connection != null && this.serverInstance == serverInstance && this.projectName == projectName)
                return;

            if (connection != null)
            {
                try
                {
                    connection.Disconnect();
                }
                catch { }
            }

            this.serverInstance = serverInstance;
            this.projectName = projectName;

            // May bring up a dialog for credentials.
            connection = new VssConnection(GetCollectionUri(), new VssClientCredentials(true));

            // Create instance of WorkItemTrackingHttpClient using VssConnection
            WorkItemClient = await connection.GetClientAsync<WorkItemTrackingHttpClient>();
        }

        public async Task<List<QueryHierarchyItem>> ListQueries()
        {
            // Get 2 levels of query hierarchy items.
            // According to this this is as deep as we can go
            // https://blog.joergbattermann.com/2016/05/05/vsts-tfs-rest-api-06-retrieving-and-querying-for-existing-work-items/
            return await WorkItemClient.GetQueriesAsync(projectName, expand: QueryExpand.All, depth: 2, includeDeleted: false);
        }
    }
}