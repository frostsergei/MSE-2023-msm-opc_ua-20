using Logika.Meters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Logika.Comms.Protocols.M4
{
    public class M4Packet
    {
        public byte NT = 0xFF;
        public bool Extended;
        public byte ID;
        public byte Attributes;
        public M4Opcode FunctionCode;
        public byte[] Data;
        public ushort Check;    //CRC16 for extended proto, CSUM8 for legacy

        public byte[] getDump()
        {
            List<byte> lb = new List<byte>();

            lb.Add(M4Protocol.FRAME_START);
            lb.Add(NT);
            if (Extended) {
                lb.Add(M4Protocol.EXT_PROTO);
                lb.Add(ID);
                lb.Add(Attributes);
                ushort payloadLen = (ushort)(1 + Data.Length);
                lb.Add((byte)(payloadLen & 0xFF));
                lb.Add((byte)(payloadLen >> 8));
            }
            lb.Add((byte)FunctionCode);
            lb.AddRange(Data);

            if (Extended) {
                lb.Add((byte)(Check >> 8));
                lb.Add((byte)(Check & 0xFF));
            } else {
                lb.Add((byte)(Check & 0xFF));
                lb.Add(M4Protocol.FRAME_END);
            }

            return lb.ToArray();
        }
    }

}