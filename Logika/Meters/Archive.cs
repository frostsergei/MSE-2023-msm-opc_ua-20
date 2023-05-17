using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Logika.Meters
{

    public abstract class Archive
    {
        public static string FLD_EXTPROP_KEY = "AfInfo";
        public readonly Meter Meter;
        public readonly ArchiveType ArchiveType;

        public Archive(Meter mtr, ArchiveType arType)
        {
            Meter = mtr;
            ArchiveType = arType;
        }
    }

    public class IntervalArchive : Archive
    {
        public readonly ArchiveFieldCollection Fields;
        public readonly DataTable Table;

        public IntervalArchive(Meter mtr, ArchiveType arType)
            :base(mtr, arType)
        {
            if (!arType.IsIntervalArchive)
                throw new ArgumentException("wrong archive type");

            Table = new DataTable(string.Format("{0}-{1}", mtr.GetType().Name, arType.ToString()));
            
            DataColumn dt_tc = Table.Columns.Add("tm", typeof(DateTime));            
            Table.PrimaryKey = new DataColumn[] { dt_tc };
             
            Fields = new ArchiveFieldCollection(this);
        }

        public IntervalArchive(Meter mtr, ArchiveType arType, DataTable template)            
            :this(mtr, arType)
        {
            if (!arType.IsIntervalArchive)
                throw new ArgumentException("wrong archive type");

            foreach (DataColumn c in template.Columns) {
                if (c.ColumnName.ToLower() != "tm") {
                    DataColumn newCol = Table.Columns.Add(c.ColumnName, c.DataType);
                    foreach (var k in c.ExtendedProperties.Keys)
                        newCol.ExtendedProperties[k] = c.ExtendedProperties[k];
                }
            }
        }

        public class ArchiveFieldCollection : ICollection<ArchiveField>, IEnumerable<ArchiveField>
        {
            IntervalArchive owner;
            internal ArchiveFieldCollection(IntervalArchive owner)
            {
                this.owner = owner;
            }

            public ArchiveField this[int index]
            {
                get { return (ArchiveField)owner.Table.Columns[index].ExtendedProperties[FLD_EXTPROP_KEY]; }
                set { owner.Table.Columns[index].ExtendedProperties[FLD_EXTPROP_KEY] = value; }
            }

            public ArchiveField this[DataColumn column]
            {
                get { return (ArchiveField)owner.Table.Columns[column.Ordinal].ExtendedProperties[FLD_EXTPROP_KEY]; }
                set { owner.Table.Columns[column.Ordinal].ExtendedProperties[FLD_EXTPROP_KEY] = value; }
            }

            //object IList.this[int index]
            //{
            //    get {
            //        return (ArchiveField)owner.Table.Columns[index].ExtendedProperties[FIELDINFO_EXTPROP_KEY];
            //    }
            //    set {
            //        if (value != null && !(value is ArchiveField))
            //            throw new ArgumentException("wrong type");
            //        owner.Table.Columns[index].ExtendedProperties[FIELDINFO_EXTPROP_KEY] = value;
            //    }
            //}

            public int Count => owner.Table.Columns.Count;

            public object SyncRoot => owner.Table.Columns;

            public bool IsSynchronized => false;

            public bool IsReadOnly => true;

            public void Add(ArchiveField item)
            {
                throw new Exception("read-only collection");
            }

            public void Clear()
            {
                throw new Exception("read-only collection");
            }

            public bool Contains(ArchiveField item)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(Array array, int index)
            {
                ArchiveField[] vta = new ArchiveField[owner.Table.Columns.Count];
                for (int i = 0; i < vta.Length; i++)
                    array.SetValue(owner.Table.Columns[i].ExtendedProperties[FLD_EXTPROP_KEY], i);
            }

            public void CopyTo(ArchiveField[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public IEnumerator<ArchiveField> GetEnumerator()
            {
                foreach (DataColumn c in owner.Table.Columns) {
                    yield return (ArchiveField)c.ExtendedProperties[FLD_EXTPROP_KEY];
                }
            }

            public bool Remove(ArchiveField item)
            {
                throw new Exception("read-only collection");
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                foreach (DataColumn c in owner.Table.Columns) {
                    yield return c.ExtendedProperties[FLD_EXTPROP_KEY];
                }
            }
        }

    }

    public class ServiceRecord
    {
        public DateTime tm;        
        public string @event;
        public string description;
        public override string ToString()
        {
            return string.Format("{0} {1} {2}", tm, @event, description);
        }
    }

    public class ServiceArchive : Archive        
    {
        public List<ServiceRecord> Records;

        public ServiceArchive(Meter m, ArchiveType art)
            :base(m, art)
        {
            if (!art.IsServiceArchive)
                throw new ArgumentException("wrong archive type");
            Records = new List<ServiceRecord>();
        }
    }

}
