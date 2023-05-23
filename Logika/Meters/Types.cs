using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Data;
using System.Reflection;
//using System.ServiceModel;
using System.Runtime.Serialization;

namespace Logika.Meters
{

    public enum MeasureKind
    {
        [Description("тепло/вода")]
        T,
        [Description("газ")]
        G,
        [Description("электроэнергия")]
        E
    }

    [TypeConverter(typeof(Logika.Utils.EnumDescriptionConverter))]
    public enum BusProtocolType
    {        
        [Description("СПСеть")]
        SPbus = 6,
        [Description("M4")]
        RSbus = 4,
    }

    public enum TagKind : int
    {
        [Description("?")]
        Undefined = 0,
        [Description("настроечные")]
        Parameter = 1,
        [Description("информационные")]
        Info = 2,
        [Description("текущие")]
        Realtime = 3,
        [Description("тотальные")]
        TotalCtr = 4,        
    }


    public enum ArchiveTimingType
    {
        None,           //не архив (набор тэгов)
        Synchronous,    //интервальный архив
        Asynchronous,   //сервисный архив
    }

    public class ArchiveType
    {        
        protected ArchiveTimingType Timing;
        
        public string Name { get; }
        public string Acronym { get; } //буква соответствующая архиву, совпадает с обозначениями архивов табло у x4
        public string Description { get; }
        
        public TimeSpan Interval { get; }   //примерный интервал архивирования для интервальных архивов (для приблизительных расчётов прогресса чтения)

        static Dictionary<string, ArchiveType> atDict = new Dictionary<string, ArchiveType>(StringComparer.OrdinalIgnoreCase);
        public static ArchiveType[] All {
            get {
                return atDict.Values.ToArray();
            }
        }
        private ArchiveType(string name, string description, ArchiveTimingType timingType, string acronym, TimeSpan intvSpan)
        {            
            Name = name;
            Description = description;
            Timing = timingType;
            Acronym = acronym;
            Interval = intvSpan;

            atDict.Add(Name, this);
        }
        public bool VariableInterval { get; private set; } = false;

        public static readonly ArchiveType Hour = new ArchiveType("Hour", "часовой архив", ArchiveTimingType.Synchronous, "Час", new TimeSpan(1, 0, 0));
        public static readonly ArchiveType Day = new ArchiveType("Day", "суточный архив", ArchiveTimingType.Synchronous, "Сут", new TimeSpan(1, 0, 0, 0));
        public static readonly ArchiveType Decade = new ArchiveType("Decade", "декадный архив", ArchiveTimingType.Synchronous, "Дек", new TimeSpan(10, 0, 0, 0));
        public static readonly ArchiveType Month = new ArchiveType("Month", "месячный архив", ArchiveTimingType.Synchronous, "Мес", new TimeSpan(30, 0, 0, 0));
        
        public static readonly ArchiveType ParamsLog = new ArchiveType("ParamsLog", "изменения БД", ArchiveTimingType.Asynchronous, "Изм", TimeSpan.Zero);
        public static readonly ArchiveType PowerLog = new ArchiveType("PowerLog", "перерывы питания", ArchiveTimingType.Asynchronous, "Пит", TimeSpan.Zero);
        public static readonly ArchiveType ErrorsLog = new ArchiveType("ErrorsLog", "нештатные", ArchiveTimingType.Asynchronous, "НСа", TimeSpan.Zero);

        //контрольный архив: интервал 1 сутки, только в тепловых приборах M4        
        public static readonly ArchiveType Control = new ArchiveType("Control", "контрольный архив", ArchiveTimingType.Synchronous, "Контр", new TimeSpan(1, 0, 0, 0));

        //специфика СПЕ542,543
        public static readonly ArchiveType Minute = new ArchiveType("Minute", "минутный архив", ArchiveTimingType.Synchronous, "Мин", TimeSpan.Zero) { VariableInterval = true };
        public static readonly ArchiveType HalfHour = new ArchiveType("HalfHour", "[полу]часовой архив", ArchiveTimingType.Synchronous, "ПЧас", TimeSpan.Zero) { VariableInterval = true };

        public static readonly ArchiveType Turn = new ArchiveType("Turn", "сменный архив", ArchiveTimingType.Asynchronous, "См", TimeSpan.Zero);
        public static readonly ArchiveType Diags = new ArchiveType("DiagsLog", "диагностические", ArchiveTimingType.Asynchronous, "ДСа", TimeSpan.Zero);   //x6x

        //static ArchiveType() {
        //    List<ArchiveType> la = new List<ArchiveType>();
            
        //    Type t = typeof(ArchiveType);
        //    FieldInfo[] fia = t.GetFields(BindingFlags.Public | BindingFlags.Static);
        //    foreach (var fi in fia) {
        //        if (fi.IsStatic && fi.FieldType == t) {
        //            ArchiveType at = (ArchiveType)fi.GetValue(null);
        //            la.Add(at);
        //            atDict.Add(at.Name, at);
        //        }
        //    }
        //    All = la.ToArray();
        //}
                               
        /// <summary>
        /// интервальный (синхронный) архив
        /// </summary>
        public bool IsIntervalArchive
        {
            get { return Timing == ArchiveTimingType.Synchronous; }
        }
        /// <summary>
        // сервисный (асинхронный) архив
        /// </summary>
        public bool IsServiceArchive
        {
            get { return Timing == ArchiveTimingType.Asynchronous; }
        }

        public override string ToString() {
            return Name;
        }


        internal static ArchiveType FromString(string archiveName)
        {
            return atDict[archiveName];
        }
    }

    //public static class Utilities
    //{
    //    public static string nullstr(object s)
    //    {
    //        return s == null ? null : s.ToString();
    //    }

    //}

    [DataContract]
    [KnownType(typeof(object[]))] //required to pass object[] as elements of DataValues
    public class VQT
    {
        [DataMember]
        public object Value;

        [DataMember]
        public int Quality;

        [DataMember]
        public DateTime Timestamp;

        public override string ToString()
        {
            return Timestamp.ToString("dd.MM.yyyy - HH:mm:ss") + " - " + (Value == null ? "[null]" : Value.ToString());
        }
    }

    [DataContract]
    public class HistoricalSeries
    {
        /// <summary>
        /// The client provided handle for this item
        /// </summary>
        [DataMember]
        public int ClientHandle;

        /// <summary>
        /// The values/qualities/timestamps for the item.
        /// </summary>
        [DataMember]
        public List<VQT> Data;
        
    }
    
    public enum ImportantTag
    {
        Ident,      //ИД или 008
        Model,      //модель (исполнение) прибора
        SerialNo,   //серийный номер платы
        //ConsConfig, //конфигурация потребителей (или СП для x4)
        //PipeConfig, //конфигурация трубопроводов 
        //IfConfig,   //конфиг интерфейса(ов)
        NetAddr,    //сетевой адрес
        RDay,       //расчетные сутки
        RHour,      //расчетный час
        EngUnits,   //единицы измерения
//        TO,         //время пуска
//        DO,         //дата пуска
        ParamsCSum, //контрольная сумма БД, рассчитанная прибором
    }

    public class VitalInfo
    {
        public string id;   //ИД прибора из настроечной БД
        public string hwRev;    //модель (x4) / 099н00 (x6)
        public string hwSerial;
        public string[] intfConfig;
        public byte? nt;
        public byte? rd;
        public byte? rh;

        //public string[] eu;             //единицы измерения (у некоторых приборов отдельно по P и Q)
        //public string pipeConf;         //конф. трубопроводов (только у x6)
        //public string[] consConf;       //конф. потребителей (у x4 - СП (+СП по ТВ2))

        public string mtrParamsHash;   //контрольная сумма БД (рассчитанная прибором)
        public TimeSpan? clockDiff;     //clockDiff = Tdevice - Tcomp.  Tdevice = Tcomp+clockDiff 
    }



    public class ColumnInfo : IEquatable<ColumnInfo>
    {        
        //public int index;
        public string name;
        public string dataType;
        //internal int? maxCharLen;            
        public bool nullable;
        
        public override string ToString()
        {
            return name;
        }

        public bool Equals(ColumnInfo other)
        {

            bool eq =
                name.Equals(other.name, StringComparison.OrdinalIgnoreCase) &&
                dataType.Equals(other.dataType, StringComparison.OrdinalIgnoreCase) &&
                nullable == other.nullable;
            return eq;
        }
    
    }
}
