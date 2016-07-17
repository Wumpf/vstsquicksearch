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
            public ConnectionSettings()
            { }

            public ConnectionSettings(ConnectionSettings settings)
            {
                ServerInstance = (string)settings.ServerInstance.Clone();
                ProjectName = (string)settings.ProjectName.Clone();
                Collection = (string)settings.Collection.Clone();
            }

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

            this.settings = new ConnectionSettings(settings);

            try
            {
                // May bring up a dialog for credentials.
                var credentials = new VssClientCredentials(true);
                credentials.PromptType = Microsoft.VisualStudio.Services.Common.CredentialPromptType.PromptIfNeeded;
                credentials.Storage = new VssClientCredentialStorage(); // Using the standard credential storage to cache credentials. I trust this is safe...
                connection = new VssConnection(GetCollectionUri(), credentials);

                // Create instance of WorkItemTrackingHttpClient using VssConnection
                WorkItemClient = await connection.GetClientAsync<WorkItemTrackingHttpClient>();
            }
            catch(Exception e)
            {
                connection = null;
                WorkItemClient = null;
                throw e;
            }
        }

        public async Task<List<QueryHierarchyItem>> ListQueries()
        {
            System.Diagnostics.Debug.Assert(WorkItemClient != null);

            // Get 2 levels of query hierarchy items.
            // According to this this is as deep as we can go
            // https://blog.joergbattermann.com/2016/05/05/vsts-tfs-rest-api-06-retrieving-and-querying-for-existing-work-items/
            var queryList = await WorkItemClient.GetQueriesAsync(settings.ProjectName, expand: QueryExpand.All, depth: 2, includeDeleted: false);

            return queryList;
        }

        /// <summary>
        /// Updates all children of a given query.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public async Task RetrieveSubqueries(QueryHierarchyItem query)
        {
            var newQueryObject = await WorkItemClient.GetQueryAsync(settings.ProjectName, query.Path, expand: QueryExpand.All, depth: 2, includeDeleted: false);

            // Shouldn't happen, but may if the user removed queries.
            while (newQueryObject.Children.Count < query.Children.Count)
                query.Children.RemoveAt(query.Children.Count - 1);

            // Overwrite child properties.
            var type = typeof(QueryHierarchyItem);
            for (int i=0; i<query.Children.Count; ++i)
            {
                foreach (var sourceProperty in type.GetProperties())
                {
                    var targetProperty = type.GetProperty(sourceProperty.Name);
                    targetProperty.SetValue(query.Children[i], sourceProperty.GetValue(newQueryObject.Children[i], null), null);
                }
            }

            // Shouldn't happen, but may if the user added queries.
            for(int i=query.Children.Count; i<newQueryObject.Children.Count; ++i)
                query.Children.Add(newQueryObject.Children[i]);
        }
    }
}
