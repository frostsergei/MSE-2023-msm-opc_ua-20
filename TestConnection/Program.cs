﻿using Logika.Meters;
using Logika.Comms.Connections;
using Logika.Comms.Protocols.M4;
using Logika.Comms.Protocols;
using System.Text;
using Logika.Comms.Protocols.SPBus;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Reflection;

void m4_protocol_test_request() {//печатает значение одного из тэгов прибора
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    TCPConnection first_level = new TCPConnection(30000, "91.209.59.238", 8002);
    M4Protocol second_level = new M4Protocol();
    second_level.connection = first_level;
    first_level.Open();
    byte[] connection_dump;
    string test_meter_model;
    string model;
    int Rate;
    Meter test_meter = Protocol.AutodetectSPT(first_level, BaudRate.Undefined, 30000, true, false, false, null, null, out connection_dump, out Rate, out model);
    Console.WriteLine(test_meter);
    DataTagDef def = test_meter.FindTag("ОБЩ", "ИД");
    DataTag request = new DataTag(def, 0);
    second_level.UpdateTags(null, M4Protocol.BROADCAST, new DataTag[] { request });
    Console.WriteLine(request);
    first_level.Close();
}

void SPBus_protocol_test_request() { //печатает значение одного из тэгов прибора
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    TCPConnection first_level = new TCPConnection(30000, "91.209.59.238", 8001);
    first_level.Open();
    SPBusProtocol second_level = new SPBusProtocol(false);
    second_level.connection = first_level;
    byte[] connection_dump;
    string test_meter_model;
    string model;
    int Rate;
    Meter test_meter = Protocol.AutodetectSPT(first_level, BaudRate.Undefined, 30000, false, true, false, 5, 0, out connection_dump, out Rate, out model);
    DataTagDef def = test_meter.FindTag("0","022н02");
    DataTag request = new DataTag(def, 0);
    second_level.UpdateTags(5, 0, new DataTag[] { request });
    Console.WriteLine(request);
    first_level.Close();
}
// m4_protocol_test_request();


var meter = Logika.Meters.Meter.SupportedMeters;
foreach (var item in meter)
{
    var tags = item.Tags;
    foreach (var tag in tags.All)
    {
        Console.WriteLine("\t{0}", tag);
    }
    Console.WriteLine(item);
}
