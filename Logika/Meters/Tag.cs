using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Data;
using System.Reflection;
//using System.ServiceModel;
using System.Runtime.Serialization;
using System.Diagnostics;

namespace Logika.Meters
{    
    public class Tag    //base bor both data tags and archive tags
    {
        public TagDef def { get; }
        public Channel Channel { get; }

        public string Name { get { return def.Name; } }

        public string FieldName  
        {
            get {
                if (def.Meter is Logika4) {
                    if (def.ChannelDef.Prefix == "ТВ")
                        return string.Format("{0}_{1}", Channel.Name, def.Name);
                    return def.Name;

                } else if (def.Meter is Logika6) {
                    string tagName = def.Name;

                    if (Channel.No > 0 && !string.IsNullOrEmpty(def.ChannelDef.Prefix))
                        tagName += string.Format(" {0}{1:D2}", def.ChannelDef.Prefix, Channel.No);
                    return tagName;

                } else
                    throw new Exception("unsupported dev family");
            }
        }

        public int Ordinal { get { return def.Ordinal; } }
        
        public Tag(TagDef refTag, int channelNo)
        {
            def = refTag;            
            if (channelNo < def.ChannelDef.Start || channelNo >= def.ChannelDef.Start + def.ChannelDef.Count)
                throw new ArgumentOutOfRangeException("channelNo", channelNo, "некорректный номер канала");

            Channel = new Channel(refTag.ChannelDef, channelNo);
        }

        public Tag(Tag vt)
        {
            this.def = vt.def;
            this.Channel = vt.Channel;
        }

        public string Description
        {
            get { return def.Description; }
        }
        
        public virtual string Address
        {
            get { throw new NotImplementedException("Tag is abstract )"); }
        }

        public string EU { get; set; }

        public override string ToString()
        {
            string idxStr = "";
            //if (Index != -1)
            //    idxStr = string.Format("н{0:D2}", Index);
            string sChNum = Channel.No == 0 ? "" : Channel.No.ToString();
            return string.Format("{0}.{1}{2}({3})", Channel.Name, def.Ordinal, idxStr, def.Name);
        }
    }

}
