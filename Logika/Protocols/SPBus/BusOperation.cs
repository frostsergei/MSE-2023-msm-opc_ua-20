﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Logika.Comms.Protocols.SPBus
{
    public enum BusOperation : byte
    {
        ReadParam = 0x1D,           //чтение параметра
        WriteParam = 0x03,          //запись параметра (или ответ прибора на запрос чтения параметра)
        ReadIdxParam = 0x0C,        //чтение параметра с индексацией
        WriteIdxParam = 0x14,       //запись параметра с индексацией (или ответ прибора на запрос чтения параметра с индексацией)

        ReadArchive = 0x0E,         //запрос чтения архива

        ReadTableRow = 0x18,        //чтение 'среза' архива 
        ReadTableDescriptor = 0x19, //чтение описания архива срезов

        HangupModem = 0x82,         //завершение модемной сессии

        //приходящие только от прибора
        ArchiveData = 0x16,         //ответ прибора на запрос чтения архива
        TableRow = 0x20,            //ответ прибора на запрос среза
        TableDescriptor = 0x21,     //описание архива срезов
        
        WriteResult = 0x7F,         //подтверждение операции записи параметра
    }

}
