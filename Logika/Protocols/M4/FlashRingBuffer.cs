using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Logika.Meters;

namespace Logika.Comms.Protocols.M4
{
    public class FlashArray
    {
        protected const int PAGE_SIZE = Meters.Logika4L.FLASH_PAGE_SIZE;
        protected byte[] flash;
        protected bool[] page_map;
        protected readonly int page_0_number;

        readonly int firstElementOffset;

        protected readonly int dataAddr;
        public readonly int elementCount;
        public readonly int elementSize;

        public readonly M4Protocol.MeterInstance mtrInstance;

        public FlashArray(M4Protocol.MeterInstance meterInstance, int DataAddr, int ElementCount, int ElementSize)
        {
            mtrInstance = meterInstance;

            dataAddr = DataAddr;
            elementCount = ElementCount;
            elementSize = ElementSize;

            int startPage = StartPage(0);
            page_0_number = startPage;

            int endPage = EndPage(elementCount - 1);
            int pageCount = endPage - startPage + 1;

            flash = new byte[pageCount * PAGE_SIZE];
            page_map = new bool[pageCount];

            firstElementOffset = dataAddr - startPage * PAGE_SIZE;            
        }

        public void GetElement(int index, out byte [] buffer, out int offset)
        {
            {
                if (!ElementAvailable(index)) {
                    
                    //DataServer.Instance.Log(LogLevel.Trace, Facilities.Counter, this.counter.NamePath, string.Format("cache miss for element {0}", index));
                    
                    UpdateElementExplicit(index);
                }
                buffer = flash;
                offset = firstElementOffset + index * elementSize;
            }
        }

        protected int StartPage(int elementIndex)
        {
            return (dataAddr + elementIndex * elementSize) / PAGE_SIZE;
        }

        protected int EndPage(int elementIndex)
        {
            return (dataAddr + (elementIndex + 1) * elementSize - 1) / PAGE_SIZE;
        }

        protected bool ElementAvailable(int index)
        {                        
            int sp = StartPage(index);
            int ep = EndPage(index);
            for (int p = sp; p <= ep; p++)
                if (!page_map[p - page_0_number])
                    return false;
            return true;
        }

        internal void InvalidateElement(int index)
        {
            int sp = StartPage(index);
            int ep = EndPage(index);
            for (int p = sp; p <= ep; p++)
                page_map[p - page_0_number] = false;                        
        }

        protected void UpdatePages(int startPage, int endPage)
        {
            int pageCount = endPage - startPage + 1;
            if (pageCount > 1) {
                //DataServer.Instance.Log(LogLevel.Trace, Facilities.Counter, counter.NamePath, string.Format("reading pages {0} .. {1}", startPage, endPage));                
            } else {
                //DataServer.Instance.Log(LogLevel.Trace, Facilities.Counter, counter.NamePath, string.Format("reading page {0}", startPage));
            }

            byte[] rbuf = mtrInstance.proto.ReadFlashPages(mtrInstance.mtr as Logika4L, mtrInstance.nt, startPage, pageCount);

            int rPage = startPage - page_0_number;
            rbuf.CopyTo(flash, rPage * PAGE_SIZE);
            for (int v = 0; v < pageCount; v++)
                page_map[rPage + v] = true;
        }

        /// <summary>
        /// indexes better should be sorted for correct gouping
        /// </summary>
        /// <param name="indexes"></param>
        /// <param name="startFrom"></param>
        /// <returns></returns>
        public void UpdateElements(List<FRBIndex> indexes)
        {
            int fsp = -1;
            int fep = -1;

            int i = 0;
            for (; i < indexes.Count; i++) {

                int esp = StartPage(indexes[i].idx);    //element start page 
                int eep = EndPage(indexes[i].idx);      //element end page 
                for (int p = esp; p <= eep; p++) {
                    if (!page_map[p - page_0_number]) {
                        if (!ExtendPageRange(p, ref fsp, ref fep))   //cannot extend page range with this var
                            goto update;
                        
                    } else if (fep != -1 && fsp != -1)
                        goto update; //found flash page which not needs update, and have interval to update
                }
            }
        update:
            if (fsp != -1 && fep != -1) {
                UpdatePages(fsp, fep);
                indexes.RemoveRange(0, i);
            } else {
                indexes.Clear();    //no elements need an update
            }
        }

        protected void UpdateElementExplicit(int element)
        {
            UpdatePages(StartPage(element), EndPage(element));
        }

        protected static bool ExtendPageRange(int page, ref int startPage, ref int endPage)
        {
            if (startPage == -1 || endPage==-1) {
                startPage = endPage = (int)page;
                return true;
            }
            
            if (page < startPage - 1 || page > endPage + 1)
                return false;

            int n_sp = startPage;
            int n_ep = endPage;

            if (page == startPage || page == startPage - 1)
                n_sp = page;
            else if (page==endPage || page == endPage + 1)
                n_ep = page;
            else
                return false;
            
            if (n_ep-n_sp >= M4Protocol.MAX_PAGE_BLOCK)
                return false;

            startPage = n_sp;
            endPage = n_ep;
            return true;
        }

        public virtual void Reset()
        {
            Array.Clear(page_map, 0, page_map.Length);
        }
    }

    internal class FlashRingBuffer : FlashArray
    {
        int prev_idx;          //last-read ptr to last written element
        Nullable<DateTime> ts_prev_idx;       //_timestamp_of_element_, at which last ptr was pointing to at the moment of read
        DateTime prevIdx_devTime;    //_device_ time, at which prev ptr was read

        FlashArchive4 parentArchive;
        public readonly int IndexAddress;
        
        public override void Reset()        
        {
            base.Reset();
            prev_idx = -1;
            ts_prev_idx = null;
            prevIdx_devTime = DateTime.MinValue;
        }

        internal FlashRingBuffer(FlashArchive4 Parent, int IndexAddress, int DataAddress, int ElementCount, int ElementSize, GetObjectDelegate HeaderTimeGetter, GetObjectDelegate HeaderValueGetter)
            : base(Parent.mi, DataAddress, ElementCount, ElementSize)
        {
            parentArchive = Parent;
            this.IndexAddress = IndexAddress;
            
            Times = new ObjCollection<DateTime?>(this, HeaderTimeGetter);
            if (HeaderValueGetter!=null)             
                Values = new ObjCollection<string>(this, HeaderValueGetter);

            Reset();
        }
                        
        public bool GetElementIndexesInRange(DateTime initialTime, DateTime stopTime, int lastWrittenIndex, ref int restartPoint, List<FRBIndex> indexes, out double percentCompleted)
        {
            if (restartPoint < 0)    //first call - initialize inter-call pointer                                
                restartPoint = (int)lastWrittenIndex;
            
            bool finished = false;
            int readsDone = 0;
            int count = (restartPoint - lastWrittenIndex + elementCount) % elementCount;                
            if (count == 0)
                count = elementCount;
            
            int ci = restartPoint;
            for (int i = 0; i < count; i++) {   //scan all (at max) elements of ringbuffer
                ci = (restartPoint - i + elementCount) % elementCount;

                if (ElementAvailable(ci)) {
                    DateTime? t = Times[ci];
                    if (t == null) {
                        //в СПГ741(как минимум) забавный глюк - последний эл-т часовых индексов не пишется (==0x00000000)
                        if (ci == elementCount - 1) {  //поэтому не заканчиваем обработку если пустой индекс находится на границе буфера.
                            continue;
                        } else {
                            finished = true;    //encountered an uninitalized / erased element (ringbuffer start reached)
                            break;  //no more processing required 
                        }
                    }

                    if (!t.HasValue || t.Value == DateTime.MinValue) { //skip elements with invalid timestamps
                        //DataServer.Instance.Log(LogLevel.Warn, Facilities.Counter, counter.NamePath, "поврежденный заголовок архива");                        
                        continue;
                    }

                    //now, fetch elements matching our time criteria                                        
                    if (t >= initialTime && t <= stopTime)
                        indexes.Add(new FRBIndex(ci, t.Value));
                    
                    finished = t <= initialTime;
                    finished |= i == count-1;

                    if (finished)
                        break;

                } else {    //encountered cache miss - scan for update elements from [ci to endIndex]                                        
                    //не запрашиваем за один выделенный квант времени больше одной пачки страниц                    
                    if (readsDone > 0)
                        break;

                    int elements_left = count - i;

                    int fsp = -1;
                    int fep = -1;
                    for (int t = 0; t < elements_left; t++) {
                        int ti = (ci - t + elementCount) % elementCount;

                        int esp = StartPage(ti);    //element start page 
                        int eep = EndPage(ti);      //element end page 
                        //если элементы будут занимать > 2 стр -> группировка не всегда будет работать 
                        //(нужно будет менять направление цикла в зависимости от direction)
                        for (int page = eep; page >= esp; page--) {
                            if (!page_map[page - page_0_number]) {
                                if (!ExtendPageRange(page, ref fsp, ref fep))  //cannot extend page range with this var
                                    goto update;

                            } else if (fep != -1 && fsp != -1)
                                goto update; //found flash page which not needs update, and have interval to update
                        }
                    }

                update: UpdatePages(fsp, fep);
                    readsDone++;
                    i--;    //возвращаем обработку на предыдущий элемент (иначе будет пропущен стартовый)
                }
            }
            
            restartPoint = ci;

            percentCompleted = indexes.Count * 100.0 / this.elementCount;

            if (finished) {                
                indexes.Reverse();
                percentCompleted = 100;
            }
            
            return finished;
        }

        internal void ManageOutdatedElements(bool useIndexCache, out int[] new_indexes, out int currentIndex)
        {
            List<int> outdatedList = new List<int>();

            //возможна ситуация, когда прочитанный индекс относится по нашему мнению к новому часу, а в приборе он еще не обновился,
            //плюс есть небольшая ошибка в определении разницы времени прибора и сервера
            //=> весь следующий час сервер будет использовать неправильно закешированный старый индекс.
            //поэтому: рядом с границей часа индекс читаем, используем, но не кешируем
            DateTime cdt = this.parentArchive.mi.CurrentDeviceTime;
            const int gT = 15;
            bool atGuardInterval = (cdt.Minute==59 && cdt.Second > 60 - gT) || (cdt.Minute==0 && cdt.Second < gT);
            
            if (useIndexCache && prev_idx != -1 && prevIdx_devTime != DateTime.MinValue) {
                if (prevIdx_devTime.Date == cdt.Date && prevIdx_devTime.Hour == cdt.Hour) {
                    //DataServer.Instance.Log(LogLevel.Trace, Facilities.Counter, counter.NamePath, string.Format("using cached index value {0}", prev_idx));                    
                    currentIndex = prev_idx;
                    goto exit;
                }
            }

            byte[] ibytes = parentArchive.mi.proto.ReadFlashBytes(parentArchive.mi.mtr as Logika4L, parentArchive.mi.nt, IndexAddress, sizeof(ushort));
            currentIndex = BitConverter.ToUInt16(ibytes, 0);
            //DataServer.Instance.Log(LogLevel.Trace, Facilities.Counter, counter.NamePath, string.Format("index read: {0}", currentIndex));            

            if (currentIndex >= elementCount) {                
                throw new Exception(string.Format("некорректный указатель конца архива: ({0})", currentIndex));
            }

            if (prev_idx != -1) {

                UpdateElementExplicit(prev_idx);      //чтение без кеша
                Nullable<DateTime> prev_ptr_actual_ts = Times[prev_idx];

                int st = 0;
                int cnt = 0;

                //если время элемента по запомненному указателю изменилось -
                //считаем что все кольцо буфера надо читать заново
                bool invalidateAll = ts_prev_idx != prev_ptr_actual_ts;

                if (invalidateAll) {    //invalidate all elements
                    st = 0;
                    cnt = elementCount;

                } else if (prev_idx != currentIndex) {    //if ptr shifted, invalidate only [prev .. curr] elements                    
                    st = prev_idx;
                    cnt = (currentIndex - prev_idx + elementCount) % elementCount;
                }

                if (cnt != 0) {
                    //DataServer.Instance.Log(LogLevel.Trace, Facilities.Counter, counter.NamePath, string.Format("invalidating elements {0} .. {1}", st, (st + cnt - 1) % elementCount));
                    
                    for (int i = 0; i < cnt; i++) {
                        int idx = (st + i) % elementCount;
                        InvalidateElement(idx);
                        outdatedList.Add(idx);
                    }
                }
            }

            prev_idx = currentIndex;
            if (!atGuardInterval)
                prevIdx_devTime = this.parentArchive.mi.CurrentDeviceTime;
            else
                prevIdx_devTime = DateTime.MinValue;    //не кешируем индекс, прочитанный в защитном интервале

            ts_prev_idx = Times[currentIndex];
exit:
            new_indexes = outdatedList.ToArray();
        }

        internal readonly ObjCollection<DateTime?> Times;
        internal readonly ObjCollection<string> Values;

        internal class ObjCollection<T>
        {
            FlashRingBuffer parent;
            GetObjectDelegate gobj;
            public ObjCollection(FlashRingBuffer ringBuffer, GetObjectDelegate getObjDelegate)
            {
                parent = ringBuffer;
                gobj = getObjDelegate;
            }
            public T this[int index]
            {
                get
                {
                    byte[] buf;
                    int offset;
                    parent.GetElement(index, out buf, out offset);
                    return (T)gobj(parent.parentArchive, buf, offset);
                }
            }
        }
    }

    public class FRBIndex
    {
        public readonly int idx;
        public readonly DateTime time;

        public FRBIndex(int index, DateTime time)
        {
            this.idx = index;
            this.time = time;
        }

        public override string ToString()
        {
            return string.Format("{0}: {1:dd.MM.yyyy HH:mm}", idx, time);
        }

        public static int compareByIdx(FRBIndex a, FRBIndex b)
        {
            return a.idx - b.idx;
        }
    }
}