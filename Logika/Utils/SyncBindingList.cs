using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Logika.Utils
{    
    public class SyncBindingList<T> : System.ComponentModel.BindingList<T>
    {

        private System.ComponentModel.ISynchronizeInvoke _syncObject;
        //private System.Action<System.ComponentModel.ListChangedEventArgs> _FireEventAction;

        public SyncBindingList()
            : this(null)
        {
        }

        public SyncBindingList(System.ComponentModel.ISynchronizeInvoke syncObject)
        {
            _syncObject = syncObject;
            //_FireEventAction = FireEvent;            
        }

        public void ItemChanged(T sender)
        {            
            OnListChanged(new System.ComponentModel.ListChangedEventArgs(System.ComponentModel.ListChangedType.ItemChanged, this.IndexOf(sender)));
        }

        delegate void ListChangedDelegate(System.ComponentModel.ListChangedEventArgs e);

        protected override void OnListChanged(System.ComponentModel.ListChangedEventArgs args)
        {      
            if ((_syncObject != null) && (_syncObject.InvokeRequired)) {                
                _syncObject.BeginInvoke(new ListChangedDelegate(base.OnListChanged), new object[] { args });
            
            } else {                
                base.OnListChanged(args);

            }
        }
                
    }
}
