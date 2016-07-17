using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VstsQuickSearch
{
    public class ObservableReplacableCollection<T> : ObservableCollection<T>
    {
        public void ReplaceItems(IEnumerable<T> newCollection)
        {
            if (newCollection == null)
                throw new ArgumentNullException("collection");

            Items.Clear();
            foreach (var i in newCollection)
                Items.Add(i);

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
