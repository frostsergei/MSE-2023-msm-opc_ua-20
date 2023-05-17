using System;

namespace Logika.Comms.Connections
{
	//---------------------------------------------------------------------------------------------
	public class OfflineConnection : Connection 
	{
        public override void Dispose(bool Disposing)
        {            
        }

        protected override void InternalOpen(out string connectDetails)
        {
            connectDetails = null;
        }
		protected override void InternalClose() 
		{            
        }

		protected override int InternalRead(byte[] buf, int Start, int MaxLength) 
		{
			return 0;
		}
		protected override void InternalWrite(byte[] buf, int Start, int nBytes) 
		{
		}

        protected override void onSetReadTimeout(int newTimeout)
        {            
        }

        public OfflineConnection(object Owner) 
		: base("", -1/*, null*/) 
		{
		}

		protected override void InternalPurgeComms(PurgeFlags what) 
		{
		}
        
        protected override bool isConflictingWith(Connection Target)
        {
            return false;
		}
	}

}
