using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AuraChromaBridge.Tests
{
    internal static class NativeCallbacksTests
    {
        public static void AddTo(List<KeyValuePair<string, Action>> tests)
        {
            tests.Add(new KeyValuePair<string, Action>("unkept_callback_is_collected", UnkeptCallbackIsCollected));
            tests.Add(new KeyValuePair<string, Action>("kept_callback_survives_collection", KeptCallbackSurvivesCollection));
        }

        // Control: proves the collection below really collects, so the next test means something.
        private static void UnkeptCallbackIsCollected()
        {
            WeakReference w = MakeCallback(false);
            Collect();
            Check.True(!w.IsAlive, "a callback nobody keeps is collected");
        }

        // Razer's DLL holds a raw pointer to the callback; if .NET collects it, a late call crashes the process.
        private static void KeptCallbackSurvivesCollection()
        {
            WeakReference w = MakeCallback(true);
            Collect();
            Check.True(w.IsAlive, "a kept callback survives garbage collection");
        }

        // Made on a thread that then exits, so no leftover stack slot on this thread keeps the callback alive
        // (Mono scans stacks conservatively).
        private static WeakReference MakeCallback(bool keep)
        {
            WeakReference result = null;
            System.Threading.Thread t = new System.Threading.Thread(delegate() { result = Make(keep); });
            t.Start();
            t.Join();
            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference Make(bool keep)
        {
            Action<int> callback = new Holder().Run;
            if (keep) NativeCallbacks.Keep(callback);
            return new WeakReference(callback);
        }

        private static void Collect()
        {
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private sealed class Holder
        {
            public void Run(int value)
            {
            }
        }
    }
}
