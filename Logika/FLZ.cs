using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Logika.Comms
{
    //quick'n'dirty port of original FastLZ::descompress to C# (l0ng, 2018)

    public class FLZ
    {
        const int MAX_DISTANCE = 8191;

        public static byte[] decompress(byte[] input, int offset, int length)
        {
            if (length == 0)
                return new byte[0];

            int level = (input[0] >> 5) + 1;
            if (level != 1)
                throw new NotImplementedException("only FLZ level1 decompression supported");

            byte[] bOut = new byte[32768];

            int ip = offset;
            int ip_limit = ip + length;
            int op = 0;     
            int op_limit = bOut.Length; 
            int ctrl = (input[ip++]) & 31;
            bool loop = true;

            do {
                int @ref = op;
                int len = ctrl >> 5;
                int ofs = (ctrl & 31) << 8;

                if (ctrl >= 32) {                    
                    len--;
                    @ref -= ofs;
                    if (len == 7 - 1)
                        len += input[ip++];
                    @ref -= input[ip++];

                    if (op + len + 3 > op_limit)
                        return null;

                    if (@ref - 1 < 0)
                        return null;

                    if (ip < ip_limit)
                        ctrl = input[ip++];
                    else
                        loop = false;

                    if (@ref == op) {
                        /* optimize copy for a run */
                        byte b = bOut[@ref - 1];    
                        bOut[op++] = b;
                        bOut[op++] = b;
                        bOut[op++] = b;
                        for (; len > 0; --len)
                            bOut[op++] = b;

                    } else {

                        int p;  
                        int q;  

                        /* copy from reference */
                        @ref--;

                        bOut[op++] = bOut[@ref++]; 
                        bOut[op++] = bOut[@ref++]; 
                        bOut[op++] = bOut[@ref++]; 

                        /* copy a byte, so that now it's word aligned */
                        if ((len & 1) > 0) {
                            bOut[op++] = bOut[@ref++];
                            len--;
                        }

                        /* copy 16-bit at once */
                        q = op;    // q = (flzuint16*)op;
                        op += len;
                        p = @ref;
                        for (len >>= 1; len > 4; len -= 4) {
                            bOut[q++] = bOut[p++]; bOut[q++] = bOut[p++];//* q++ = * p++;    
                            bOut[q++] = bOut[p++]; bOut[q++] = bOut[p++];//* q++ = * p++;    
                            bOut[q++] = bOut[p++]; bOut[q++] = bOut[p++];//* q++ = * p++;    
                            bOut[q++] = bOut[p++]; bOut[q++] = bOut[p++];//* q++ = * p++;    
                        }

                        for (; len > 0; --len) {
                            //* q++ = * p++;
                            bOut[q++] = bOut[p++];
                            bOut[q++] = bOut[p++];
                        }
                    }
                } else {
                    ctrl++;

                    if (op + ctrl > op_limit)
                        return null;
                    if (ip + ctrl > ip_limit)
                        return null;


                    bOut[op++] = input[ip++];
                    for (--ctrl; ctrl > 0; ctrl--)

                        bOut[op++] = input[ip++];

                    loop = ip < ip_limit;
                    if (loop)
                        ctrl = input[ip++];
                }
            } while (loop);

            return bOut.Take(op).ToArray();            //	return op - (flzuint8*)output;
        }

#if false
        public static void _Test()
        {
            string[] files = System.IO.Directory.EnumerateFiles("c:\\logika\\prolog\\source\\").ToArray();
            foreach (string fn in files) {
                byte[] b = System.IO.File.ReadAllBytes(fn);
                if (b.Length > 32767)
                    continue;
                System.Diagnostics.Debug.Print("{0}, {1} bytes", fn, b.Length);
                Console.WriteLine("{0}, {1} bytes", fn, b.Length);

                byte[] b1;
                byte[] b2;
                FastLZ.compress(1, b, out b1);
                FastLZ.compress(2, b, out b2);

                byte[] r1;
                byte[] r2;
                FastLZ.decompress(b1, out r1);
                FastLZ.decompress(b1, out r2);
                if (!Enumerable.SequenceEqual(b, r1) || !Enumerable.SequenceEqual(b, r2))
                    throw new Exception("original decompress failed");

                bool diff_compr = !Enumerable.SequenceEqual(b1, b2);

                byte[] d1 = FLZ.decompress(b1, 0, b1.Length);
                byte[] d2 = FLZ.decompress(b2, 0, b2.Length);
                if (!Enumerable.SequenceEqual(b, d1) || !Enumerable.SequenceEqual(b, d2))
                    throw new Exception("C# decompress failed");
                System.Diagnostics.Debug.Print("decompress OK");
                Console.WriteLine("decompress OK");
            }
#endif
    }

}
