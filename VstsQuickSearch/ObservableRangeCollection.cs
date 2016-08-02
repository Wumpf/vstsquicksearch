using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;

namespace VstsQuickSearch
{
    public class ObservableReplacableCollection<T> : ObservableCollection<T>
    {
        // See http://stackoverflow.com/questions/3300845/observablecollection-calling-oncollectionchanged-with-multiple-new-items
        // I improved a bit on the solution by not needing to suppress notifications through direct list manipulation.
        // Also, my implementation fixes a bug with "Count" updates not beeing fired.

        public override event NotifyCollectionChangedEventHandler CollectionChanged;
        protected override event PropertyChangedEventHandler PropertyChanged;

        public void ReplaceItems(IEnumerable<T> newCollection)
        {
            if (newCollection == null)
                throw new ArgumentNullException("collection");

            Items.Clear();
            foreach (var i in newCollection)
                Items.Add(i);

            OnCollectionChangedMultiItem(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newCollection));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Count"));
        }

        protected virtual void OnCollectionChangedMultiItem(NotifyCollectionChangedEventArgs e)
        {
            NotifyCollectionChangedEventHandler handlers = this.CollectionChanged;
            if (handlers != null)
            {
                foreach (NotifyCollectionChangedEventHandler handler in handlers.GetInvocationList())
                {
                    if (handler.Target is CollectionView)
                        ((CollectionView)handler.Target).Refresh();
                    else
                        handler(this, e);
                }
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);
            if (CollectionChanged != null)
                CollectionChanged.Invoke(this, e);
        }
    }
}
