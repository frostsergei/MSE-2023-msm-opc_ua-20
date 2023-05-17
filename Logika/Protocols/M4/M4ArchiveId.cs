namespace Logika.Comms.Protocols.M4
{
    public enum M4ArchiveId : byte  //код архива в протокольном запросе
    {
        Hour = 0,
        Day = 1,
        Dec = 2,
        Mon = 3,

        ParamsLog = 4,  //ИЗМенения БД
        PowerLog = 5,   //перерывы питания
        NSLog = 6,      //НС

        Ctrl = 7,       //контрольный архив
    }
}
