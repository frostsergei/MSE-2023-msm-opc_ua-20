using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Logika.Meters
{
    //naive and (probably) fast datatable (de)serialization

    public abstract class SerializerBase
    {
        protected Encoding textEncoding; 

        public SerializerBase(Encoding textEncoding)
        {
            this.textEncoding = textEncoding;
        }
    }

    enum SType: byte
    {
        Null,
        Boolean,
        Byte,
        Int16,
        Int32,
        String,
    }

    public class Serializer : SerializerBase
    {
        
        Stream s;
        public Serializer(Encoding textEncoding, Stream stream)
            :base(textEncoding)
        {
            s = stream;
        }
        byte[] buf = new byte[4096];

        public void PutByte(byte b)
        {
            buf[0] = b;
            s.Write(buf, 0, 1);
        }

        public void PutBool(bool b)
        {
            buf[0] = b ? (byte)1 : (byte)0;
            s.Write(buf, 0, 1);
        }

        public void PutInt32(int Int)
        {
            byte[] ba = BitConverter.GetBytes(Int);
            s.Write(ba, 0, ba.Length);
        }
        public void PutInt16(short si)
        {
            byte[] ba = BitConverter.GetBytes(si);
            s.Write(ba, 0, ba.Length);
        }

        public void PutString(string str)
        {
            PutBool(str != null);
            if (str != null)
            {
                byte[] tempByte = textEncoding.GetBytes(str);
                PutInt32(tempByte.Length);     //length in bytes
                s.Write(tempByte, 0, tempByte.Length);
            }
        }

        public void putObject(object o)
        {
            if (o == DBNull.Value) {
                PutByte((byte)SType.Null);
                return;
            }
            Type t = o.GetType();
            SType st = (SType)Enum.Parse(typeof(SType), t.Name);
            PutByte((byte)st);
            switch (st) {
                case SType.String:
                    PutString((string)o);
                    break;
                case SType.Byte:
                    PutByte((byte)o);
                    break;
                case SType.Int16:
                    PutInt16((short)o);
                    break;
                case SType.Int32:
                    PutInt32((int)o);
                    break;
                case SType.Boolean:
                    PutBool((bool)o);
                    break;
                default:
                    throw new Exception("unsupported type");
            }
        }

        public static byte[] SerializeDataTable(DataTable myDataTable, Encoding stringsEncoding)
        {
            //Get all row values as jagged object array
            object[][] tableItems = new object[myDataTable.Rows.Count][];
            for (int rowIndex = 0; rowIndex < myDataTable.Rows.Count; rowIndex++)
                tableItems[rowIndex] = myDataTable.Rows[rowIndex].ItemArray;

            //Get table schema
            //StringBuilder tableSchemaBuilder = new StringBuilder();
            //myDataTable.WriteXmlSchema(new StringWriter(tableSchemaBuilder));
            //string tableSchema = tableSchemaBuilder.ToString();

            MemoryStream ms = new MemoryStream();
            Serializer sr = new Serializer(stringsEncoding, ms);

            int colCount = myDataTable.Columns.Count;
            //sr.PutString(tableSchema);
            sr.PutInt32(colCount);
            for (int i = 0; i < colCount; i++) {
                sr.PutString(myDataTable.Columns[i].ColumnName);
                sr.PutString(myDataTable.Columns[i].DataType.ToString());
            }

            for (int i = 0; i < myDataTable.Rows.Count; i++) {
                for (int j = 0; j < colCount; j++) {
                    sr.putObject(tableItems[i][j]);
                }
            }

            return ms.ToArray();
        }

    }
    public class Deserializer : SerializerBase
    {        
        byte[] buf;
        int offset;
        public bool EOF { get { return offset >= buf.Length; } }

        public Deserializer(Encoding textEncoding, byte[] buffer, int offset)
            : base(textEncoding)
        {
            this.buf = buffer;
            this.offset = offset;
        }

        public byte GetByte()
        {
            return buf[offset++];
        }

        public bool GetBool()
        {
            return buf[offset++] != 0;
        }

        public int GetInt32()
        {
            int iv = BitConverter.ToInt32(buf, offset);
            offset += sizeof(int);
            return iv;
        }
        public short GetInt16()
        {
            short isv = BitConverter.ToInt16(buf, offset);
            offset += sizeof(short);
            return isv;
        }

        public string GetString()
        {
            bool hasValue = GetBool();
            if (hasValue)
            {
                int len = GetInt32(); //length in bytes
                string s = textEncoding.GetString(buf, offset, len); 

                offset += len;
                return s;
            }
            else
                return null;
        }

        public object GetObject()
        {
            SType st = (SType)GetByte();
            if (st == SType.Null)
                return null;
            switch (st) {
                case SType.String:
                    return GetString();
                case SType.Byte:
                    return GetByte();
                case SType.Int16:
                    return GetInt16();                    
                case SType.Int32:
                    return GetInt32();
                case SType.Boolean:
                    return GetBool();
                default:
                    throw new Exception("unsupported type");
            }
        }

        public static DataTable DeserializeDataTable(byte[] serializedTableData, int startOffset, Encoding stringsEncoding)
        {
            //Stopwatch sw0 = new Stopwatch();
            //sw0.Start();


            DataTable table = new DataTable();
            table.BeginLoadData();

            Deserializer ds = new Deserializer(stringsEncoding, serializedTableData, startOffset);
            //string tableSchema = ds.GetString();
            //table.ReadXmlSchema(new StringReader(tableSchema));   //that takes some significant time on Xamarin due to unknown reasons

            //deserialize schema            
            int colCount = ds.GetInt32();
            for (int i = 0; i < colCount; i++) {
                string colName = ds.GetString();
                Type colType = Type.GetType(ds.GetString());
                table.Columns.Add(colName, colType);
            }
            
            object[] row = new object[table.Columns.Count];

            while (!ds.EOF) {
                for (int i = 0; i < row.Length; i++)
                    row[i] = ds.GetObject();
                table.Rows.Add(row);
            }

            table.EndLoadData();

            return table;
        }

    }



}

