using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
        public bool DownloadComments { get; set; } = false;
#endregion

        private void CancelRunningSearch()
        {
            searchCancellation?.Cancel();
            searchTask?.Wait();

            searchCancellation?.Dispose();
            searchCancellation = null;
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private async Task<bool> EnsureConnection()
        {
            try
            {
                await connection.Connect(inputServerAdress.Text, inputProjectName.Text);
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

                try
                {
                    QueryHierachyItems.Clear();
                    var queries = await connection.ListQueries();
                    foreach (var query in queries)
                        QueryHierachyItems.Add(query);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message, "Failed to retrieve queries!");
                    return;
                }
            }
            finally
            {
                senderUi.IsEnabled = true;
            }
        }

        private async void DownloadWorkItems(object sender, RoutedEventArgs e)
        {
            var senderUi = (UIElement)sender;
            senderUi.IsEnabled = false;
            try
            {
                if (await EnsureConnection() == false)
                    return;

                QueryHierarchyItem selectedQuery = listQueries.SelectedItem as QueryHierarchyItem;
                if (selectedQuery == null || !(selectedQuery.HasChildren ?? true))
                {
                    MessageBox.Show("You need to select a Query first!", "Failed to download Work Items!");
                    return;
                }

                await workItemDatabase.DownloadData(connection, selectedQuery.Id, DownloadComments);

                labelLastUpdated.Content = DateTime.Now.ToString("HH:mm");
                labelNumDownloadedWI.Content = workItemDatabase.NumWorkItems.ToString();
                UpdateSearchBox(inputSearchText.Text);
            }
            finally
            {
                senderUi.IsEnabled = true;
            }
        }

        private void QuerySelected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            QueryHierarchyItem selectedQuery = listQueries.SelectedItem as QueryHierarchyItem;
            buttonDownloadWorkItems.IsEnabled = (selectedQuery != null && !(selectedQuery.HasChildren ?? false));
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
                    searchResult = searchResult.OrderBy(x => x.Item.Id);
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
            System.Diagnostics.Process.Start(connection.GetWorkItemUrl(item.Item.Id ?? 0));
        }
    }
}
