using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logika.Utils
{
    public class SyncObservableCollection<T> : ObservableCollection<T>
    {
        public ISynchronizeInvoke SyncObject;

        delegate void CollectionChangedDelegate(NotifyCollectionChangedEventArgs e);
        CollectionChangedDelegate cdlg;
        public SyncObservableCollection()
        {
            cdlg = new CollectionChangedDelegate(base.OnCollectionChanged);
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (SyncObject != null && SyncObject.InvokeRequired) {
                SyncObject.Invoke(cdlg, new object[] { e });

            } else {
                base.OnCollectionChanged(e);

            }
        }
    }
}
