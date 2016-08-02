using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace VstsQuickSearch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ServerConnection connection = new ServerConnection();
        public SearchableWorkItemDatabase WorkItemDatabase { get; private set; } = new SearchableWorkItemDatabase();

        private bool connectionOperationInProgress = false;
        private bool searchInProgress = false;
        
        private Task searchTask;

        private DispatcherTimer autoDownloadTimer = new DispatcherTimer();

        public ObservableCollection<QueryHierarchyItem> QueryHierachyItems { get; private set; } = new ObservableCollection<QueryHierarchyItem>();
        public ObservableReplacableCollection<SearchableWorkItem> SearchResults { get; private set; } = new ObservableReplacableCollection<SearchableWorkItem>();

        public class SettingsContainer
        {
            public ServerConnection.ConnectionSettings ConnectionSettings { get; set; } = new ServerConnection.ConnectionSettings();
            public bool DownloadComments { get; set; } = false;
            public Guid SelectedQueryGuid { get; set; } = Guid.Empty;
            public bool AutoRefresh { get; set; } = false;

            public int AutoRefreshIntervalMin
            {
                get { return autoRefreshIntervalMin; }
                set
                {
                    autoRefreshIntervalMin = Math.Max(1, value);
                }
            }
            private int autoRefreshIntervalMin = 14;
        }
        public SettingsContainer Settings { get; private set; }

        public MainWindow()
        {
            Title = $"VstsQuickSearch - {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";

            autoDownloadTimer.Tick += new EventHandler(OnAutoDownload);
            LoadSettings();
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveSettings();
            base.OnClosed(e);
        }

        #region Setting (De)Serialization
        private const string settingsFileName = "settings.json";

        private void SaveSettings()
        {
            try
            {
                using (StreamWriter file = File.CreateText("settings.json"))
                {
                    JsonSerializer settingsWriter = new JsonSerializer();
                    settingsWriter.Serialize(file, Settings);
                }
            } catch { }
        }

        private void LoadSettings()
        {
            try
            {
                using (StreamReader file= File.OpenText("settings.json"))
                {
                    JsonSerializer settingsWriter = new JsonSerializer();
                    Settings = settingsWriter.Deserialize<SettingsContainer>(new JsonTextReader(file));
                }
            }
            catch { }

            if (Settings == null)
                Settings = new SettingsContainer();

            SetupAutoDownload();
        }
        #endregion

        private async Task<bool> EnsureConnection()
        {
            try
            {
                await connection.Connect(Settings.ConnectionSettings);
                return true;
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Failed to connect!");
                return false;
            }
        }

        private bool LockQueriesAndConnection(bool locked)
        {
            System.Diagnostics.Debug.Assert(Dispatcher.Thread == Thread.CurrentThread, "Connection operations should only be started and ended from the main thread!");

            if (connectionOperationInProgress && locked)
                return false;

            connectionOperationInProgress = locked;
            sectionConnect.IsEnabled = !locked;
            sectionQueries.IsEnabled = !locked;

            return true;
        }

        private async void OnUpdateQueryListButtonClick(object sender, RoutedEventArgs e)
        {
            if(!LockQueriesAndConnection(true))
                return;

            progressBar.IsIndeterminate = true;

            try
            {
                if (await EnsureConnection() == false)
                    return;

                var previouslySelectedQuery = Settings.SelectedQueryGuid;
                QueryHierachyItems.Clear();
                List<QueryHierarchyItem> queries = null;
                try
                {
                    queries = await connection.ListQueries();
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message, "Failed to retrieve queries!");
                    return;
                }

                if (queries != null)
                {
                    // Add toplevel queries.
                    foreach (var query in queries)
                        QueryHierachyItems.Add(query);

                    RestoreQuerySelection(previouslySelectedQuery);
                }
            }
            finally
            {
                LockQueriesAndConnection(false);
                progressBar.IsIndeterminate = false;
            }
        }

        private void RestoreQuerySelection(Guid previouslySelectedQuery)
        {
            // See if we can reselect the previously selected query.
            // Sadly, selecting treeview items is hard...
            if (previouslySelectedQuery != Guid.Empty)
            {
                var itemStack = FindQuery(QueryHierachyItems, previouslySelectedQuery);
                if (itemStack != null && itemStack.Count > 0)
                {
                    // It exists! But the treeview doesn't have items that are not expanded...
                    TreeViewItem currentItem = listQueries.ItemContainerGenerator.ContainerFromItem(itemStack.Pop()) as TreeViewItem;

                    while (itemStack.Count > 0)
                    {
                        if (currentItem != null && currentItem.IsExpanded == false)
                        {
                            currentItem.IsExpanded = true;
                            currentItem.UpdateLayout(); // Necessary to generate the containers.
                        }

                        currentItem = currentItem.ItemContainerGenerator.ContainerFromItem(itemStack.Pop()) as TreeViewItem;
                    }

                    if (currentItem != null)
                        currentItem.IsSelected = true;
                }
            }
        }

        static private Stack<QueryHierarchyItem> FindQuery(IEnumerable<QueryHierarchyItem> queries, Guid queryGuid)
        {
            if (queries == null)
                return null;

            foreach(var query in queries)
            {
                if (query.Id == queryGuid)
                    return new Stack<QueryHierarchyItem>(new[] { query });

                var childQueryStack = FindQuery(query.Children, queryGuid);
                if (childQueryStack != null)
                {
                    childQueryStack.Push(query);
                    return childQueryStack;
                }
            }
            return null;
        }

        private void OnQueryDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DownloadWorkItems();
        }

        private void OnDownloadWorkItemsButtonPress(object sender, RoutedEventArgs e)
        {
            DownloadWorkItems();
        }

        private async void DownloadWorkItems(bool connectionAlreadyLocked = false)
        {
            if (Settings.SelectedQueryGuid == Guid.Empty)
                return;

            if (!LockQueriesAndConnection(true))
                return;

            try
            {
                if (await EnsureConnection() == false)
                    return;

                try
                {
                    await WorkItemDatabase.DownloadData(connection, Settings.SelectedQueryGuid, Settings.DownloadComments,
                                                        progress => progressBar.Value = progress * 100);
                }
                catch (Exception exp)
                {
                    MessageBox.Show(exp.Message, "Failed to download Work Items!");
                    return;
                }

                labelLastUpdated.Text = DateTime.Now.ToString("HH:mm");

                var gridView = new GridView();
                listViewSearchResults.View = gridView;
                if (WorkItemDatabase.LastQueryColumnDisplay != null)
                {
                    foreach (var column in WorkItemDatabase.LastQueryColumnDisplay)
                    {
                        gridView.Columns.Add(new GridViewColumn
                        {
                            Header = column.Name,
                            DisplayMemberBinding = new Binding("[" + column.ReferenceName + "]")
                        });
                    }
                }

                UpdateSearchBox();
            }
            finally
            {
                LockQueriesAndConnection(false);
                progressBar.Value = 0.0;
            }
        }

        private void QuerySelected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            QueryHierarchyItem selectedQuery = listQueries.SelectedItem as QueryHierarchyItem;

            if(selectedQuery != null && !(selectedQuery.IsFolder ?? false))
            {
                buttonDownloadWorkItems.IsEnabled = true;
                Settings.SelectedQueryGuid = selectedQuery.Id;
            }
            else
            {
                buttonDownloadWorkItems.IsEnabled = false;
                Settings.SelectedQueryGuid = Guid.Empty;
            }
        }

        private async void OnQueryFolderExpanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem treeViewItem = e.OriginalSource as TreeViewItem;
            QueryHierarchyItem expandedQuery = treeViewItem?.Header as QueryHierarchyItem;

            // Make sure children are loaded.
            if (expandedQuery != null && (expandedQuery.IsFolder ?? false) && expandedQuery.Children != null)
            {
                foreach (var child in expandedQuery.Children)
                {
                    if (child != null && (child.IsFolder ?? false) && (child.HasChildren ?? false) && (child.Children == null))
                    {
                        // This may be more conservative than necessary (e.g. it could still be possible to download items with another query), but we're better on the safe side.
                        if (!LockQueriesAndConnection(true))
                            return;

                        treeViewItem.IsEnabled = false;
                        treeViewItem.IsExpanded = false; // Don't expand immediately, otherwise the TreeView will not see the new items.

                        progressBar.IsIndeterminate = true;

                        try
                        {
                            // Doing multiple queries is extremly expensive. Therefore we check if there is a child that should be expandable and if yes, do a deep query of the item we're about to expand.
                            await connection.RetrieveSubqueries(expandedQuery);
                        }
                        catch (Exception exp)
                        {
                            MessageBox.Show(exp.Message, "Failed to expand Query folder!");
                            return;
                        }
                        finally
                        {
                            treeViewItem.Header = expandedQuery;
                            treeViewItem.IsEnabled = true;
                            treeViewItem.IsExpanded = true;
                            treeViewItem.UpdateLayout();

                            LockQueriesAndConnection(false);
                            progressBar.IsIndeterminate = false;
                        }
                        break; // The only query.
                    }
                }
            }
        }

        private void SearchChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSearchBox();
        }

        private void UpdateSearchBox()
        {
            // If there is still a search task running, don't start another one.
            if (searchInProgress)
                return;

            // Start new search.
            searchInProgress = true;
            string usedSearchText = (string)inputSearchText.Text.Clone();

            searchTask = Task.Factory.StartNew(() =>
            {
                var searchResult = WorkItemDatabase.SearchInDatabase(new SearchQuery(usedSearchText));
                searchResult = searchResult.OrderBy(x => x.WorkItem.Id);
                var searchResultList = searchResult.ToList();

                // Synchronously on the mainthread.
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SearchResults.ReplaceItems(searchResultList);
                    searchInProgress = false;

                    // Searches are only started from the main thread, therefore we cannot collide with another one.
                    if (usedSearchText != inputSearchText.Text)
                        UpdateSearchBox();
                }));
            });
        }

        private void OnWorkItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ListBox listBox = (ListBox)sender;
            SearchableWorkItem item = listBox.SelectedItem as SearchableWorkItem;
            if(item != null && item.WorkItem.Id.HasValue)
                System.Diagnostics.Process.Start(connection.GetWorkItemUrl(item.WorkItem.Id.Value));
        }

        private void OnAutoDownload(object sender, EventArgs e)
        {
            if (connection.WorkItemClient == null)
                return;

            // If something else is going on right now, ignore the call.
            // Rescheduling might also be possible, but this is dangerous since it might be the call itself that keeps the connection busy.
            if (!connectionOperationInProgress)
                DownloadWorkItems();
        }

        private void SetupAutoDownload()
        {
            if (Settings.AutoRefresh)
            {
                autoDownloadTimer.IsEnabled = false;
                autoDownloadTimer.Interval = new TimeSpan(days: 0, hours: 0, minutes: Settings.AutoRefreshIntervalMin, seconds: 0);
                autoDownloadTimer.Start();
                autoDownloadTimer.IsEnabled = true;
            }
            else
            {
                autoDownloadTimer.IsEnabled = false;
                autoDownloadTimer.Stop();
            }
        }

        private void OnAutoRefreshTimerChanges(object sender, EventArgs e)
        {
            SetupAutoDownload();
        }
    }
}
