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

namespace VstsQuickSearch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ServerConnection connection = new ServerConnection();
        private SearchableWorkItemDatabase workItemDatabase = new SearchableWorkItemDatabase();

        private CancellationTokenSource searchCancellation;
        private Task searchTask;

        #region Data Bindings
        public ObservableCollection<QueryHierarchyItem> QueryHierachyItems { get; set; } = new ObservableCollection<QueryHierarchyItem>();
        public ObservableCollection<SearchableWorkItem> SearchResults { get; set; } = new ObservableCollection<SearchableWorkItem>();
        #endregion

        public class SettingsContainer
        {
            public ServerConnection.ConnectionSettings ConnectionSettings { get; set; } = new ServerConnection.ConnectionSettings();
            public bool DownloadComments { get; set; } = false;
            public Guid SelectedQueryGuid { get; set; } = Guid.Empty;
        }
        public SettingsContainer Settings { get; private set; }
        

        private void CancelRunningSearch()
        {
            searchCancellation?.Cancel();
            searchTask?.Wait();

            searchCancellation?.Dispose();
            searchCancellation = null;
        }

        public MainWindow()
        {
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

        private async void UpdateQueryList(object sender, RoutedEventArgs e)
        {
            var senderUi = (UIElement)sender;
            senderUi.IsEnabled = false;

            buttonDownloadWorkItems.IsEnabled = false;

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
            }
            finally
            {
                senderUi.IsEnabled = true;
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
            if(Settings.SelectedQueryGuid != Guid.Empty)
                DownloadWorkItems();
        }

        private void OnDownloadWorkItemsButtonPress(object sender, RoutedEventArgs e)
        {
            DownloadWorkItems();
        }

        private async void DownloadWorkItems()
        {
            buttonDownloadWorkItems.IsEnabled = false;
            try
            {
                if (await EnsureConnection() == false)
                    return;

                try
                {
                    await workItemDatabase.DownloadData(connection, Settings.SelectedQueryGuid, Settings.DownloadComments);
                }
                catch (Exception exp)
                {
                    MessageBox.Show(exp.Message, "Failed to download Work Items!");
                    return;
                }

                labelLastUpdated.Content = DateTime.Now.ToString("HH:mm");
                labelNumDownloadedWI.Content = workItemDatabase.NumWorkItems.ToString();

                var gridView = new GridView();
                listViewSearchResults.View = gridView;
                if (workItemDatabase.LastQueryColumnDisplay != null)
                {
                    foreach (var column in workItemDatabase.LastQueryColumnDisplay)
                    {
                        gridView.Columns.Add(new GridViewColumn
                        {
                            Header = column.Name,
                            DisplayMemberBinding = new Binding("[" + column.ReferenceName + "]")
                        });
                    }
                }

                UpdateSearchBox(inputSearchText.Text);
            }
            finally
            {
                buttonDownloadWorkItems.IsEnabled = true;
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

        private void SearchChanged(object sender, TextChangedEventArgs e)
        {
            TextBox searchBox = (TextBox)sender;
            UpdateSearchBox(searchBox.Text);
        }

        private void UpdateSearchBox(string searchText)
        {
            CancelRunningSearch();

            // Start new search.
            searchCancellation = new CancellationTokenSource();
            searchTask = Task.Factory.StartNew(() =>
            {
                try
                {
                    var searchResult = workItemDatabase.SearchInDatabase(new SearchQuery(searchText), searchCancellation.Token);
                    searchResult = searchResult.OrderBy(x => x.WorkItem.Id);
                    var searchResultList = searchResult.ToList();

                    if (!searchCancellation.Token.IsCancellationRequested)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            SearchResults.Clear();
                            foreach (var elem in searchResultList)
                                SearchResults.Add(elem);
                        }));
                    }
                }
                catch (System.OperationCanceledException) { } // Can happen, everything else is an actual error.
            });
        }

        private void OnWorkItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ListBox listBox = (ListBox)sender;
            SearchableWorkItem item = listBox.SelectedItem as SearchableWorkItem;
            if(item != null && item.WorkItem.Id.HasValue)
                System.Diagnostics.Process.Start(connection.GetWorkItemUrl(item.WorkItem.Id.Value));
        }
    }
}
