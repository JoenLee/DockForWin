using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using DockForWindows.Views;

internal static class FrameOwnershipChecks
{
    private static readonly Type Frame=typeof(ControlCenterWindow).GetNestedType("SampleResult",BindingFlags.NonPublic)!;
    private static IDisposable Copy(byte[] pixels,int width=3,int height=1)=>
        (IDisposable)Activator.CreateInstance(Frame,new object[]{pixels,width,height,new Rect(7,11,width,height)})!;
    private static byte[] Pixels(IDisposable frame)=>(byte[])Frame.GetProperty("Pixels")!.GetValue(frame)!;
    private static bool Same(IDisposable frame,IDisposable? previous)=>(bool)Frame.GetMethod("SamePixels")!.Invoke(frame,new object?[]{previous})!;
    private static void Assert(bool value,string message){if(!value)throw new Exception(message);}

    internal static int Run()
    {
        try
        {
            var raw=Enumerable.Range(0,12).Select(i=>(byte)i).ToArray();
            using(var displayed=Copy(raw))
            {
                Task.Run(()=>{for(int i=0;i<10000;i++)Array.Fill(raw,(byte)i);}).GetAwaiter().GetResult();
                Assert(Pixels(displayed).AsSpan(0,12).SequenceEqual(Enumerable.Range(0,12).Select(i=>(byte)i).ToArray()),"Producer overwrote a retained frame");
                using var pending=Copy(raw);
                Assert(!ReferenceEquals(Pixels(displayed),Pixels(pending)),"Displayed and pending frames share a buffer");
                pending.Dispose();
                using var replacement=Copy(raw);
                Assert(!ReferenceEquals(Pixels(displayed),Pixels(replacement)),"Replacing a pending frame recycled the displayed buffer");
            }
            Console.WriteLine("PASS producer writes and pending replacement preserve displayed pixels");

            Array.Fill(raw,(byte)24);
            using(var displayed=Copy(raw))
            using(var same=Copy(raw))
            {
                Assert(Pixels(same).Length>12,"Fixture must exercise an oversized pooled buffer");
                Pixels(displayed)[12]=1;Pixels(same)[12]=2;
                Assert(Same(same,displayed),"Pool tail bytes caused a redundant upload");
                Assert(!Same(same,null),"First frame was treated as unchanged");
                using var resized=Copy(raw,1,3);
                Assert(!Same(resized,displayed),"Equal-length frames with different dimensions matched");
                raw[11]++;
                using var changed=Copy(raw);
                Assert(!Same(changed,displayed),"Last pixel change was missed");
                Console.WriteLine("PASS exact pixel range, first frame, resize and final pixel change");

                // A changed acquisition may be dropped before a repeated acquisition reaches the UI.
                using(var dropped=Copy(raw)){Assert(!Same(dropped,displayed),"Changed acquisition was missed");}
                using var newest=Copy(raw);
                Assert(!Same(newest,displayed),"Dropped acquisition hid a change from the displayed frame");
                Console.WriteLine("PASS change detection compares against the displayed frame after a drop");
            }

            var released=Copy(raw);released.Dispose();released.Dispose();
            Assert(Pixels(released).Length==0,"Released frame retained its lease");
            var large=new byte[512*512*4];
            using(var warmup=Copy(large,512,512)){}
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<200;i++){using var frame=Copy(large,512,512);}
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert(allocated<1024*1024,$"Frame buffers were allocated repeatedly: {allocated} bytes");
            Console.WriteLine($"PASS repeat disposal and buffer reuse; 200 x 1 MiB frames allocated {allocated} managed bytes");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);return 1;}
    }
}
