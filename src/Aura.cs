// ASUS Aura SDK v3 over COM late binding (ProgID "aura.sdk.1").
// Late binding means no ASUS type library or SDK download is needed to build this.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AuraChromaBridge
{
    /// <summary>Small helpers for calling COM objects through IDispatch.</summary>
    internal static class Com
    {
        public static object Get(object target, string name)
        {
            return Invoke(target, name, BindingFlags.GetProperty, null);
        }

        public static void Set(object target, string name, object value)
        {
            Invoke(target, name, BindingFlags.SetProperty, new object[] { value });
        }

        public static object Call(object target, string name, params object[] args)
        {
            return Invoke(target, name, BindingFlags.InvokeMethod, args);
        }

        public static object Item(object collection, int index)
        {
            return Invoke(collection, "Item", BindingFlags.InvokeMethod | BindingFlags.GetProperty, new object[] { index });
        }

        public static int Count(object collection)
        {
            return Convert.ToInt32(Get(collection, "Count"), CultureInfo.InvariantCulture);
        }

        public static void Release(object o)
        {
            if (o == null || !Marshal.IsComObject(o)) return;
            try
            {
                Marshal.ReleaseComObject(o);
            }
            catch (Exception)
            {
                // Already gone.
            }
        }

        private static object Invoke(object target, string name, BindingFlags flags, object[] args)
        {
            try
            {
                return target.GetType().InvokeMember(name, flags, null, target, args, CultureInfo.InvariantCulture);
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
                throw;
            }
        }
    }

    internal sealed class AuraDevice
    {
        public int Index;
        public object Handle;
        public string Name;
        public uint Type;
        public int Width;
        public int Height;
        public object[] Lights;
        public int[] Written;   // colour last written to each LED, -1 = unknown

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "#{0} \"{1}\" - {2} (type 0x{3:X8}), {4} LEDs ({5}x{6})",
                Index, Name, Aura.TypeName(Type), Type, Lights.Length, Width, Height);
        }
    }

    internal sealed class Aura : IDisposable
    {
        private const int DispTypeMismatch = unchecked((int)0x80020005);

        private readonly List<AuraDevice> _devices = new List<AuraDevice>();
        private object _sdk;
        private bool _colorAsUnsigned;

        public IList<AuraDevice> Devices
        {
            get { return _devices; }
        }

        public bool IsConnected
        {
            get { return _sdk != null; }
        }

        /// <summary>Creates the SDK object, takes control of the LEDs and lists every Aura device.</summary>
        public void Connect()
        {
            Type type = System.Type.GetTypeFromProgID("aura.sdk.1", false);
            if (type == null)
            {
                throw new InvalidOperationException(
                    "The ASUS Aura SDK isn't registered on this PC (COM ProgID aura.sdk.1 not found). " +
                    "It normally comes with Armoury Crate's Aura Sync.");
            }

            _sdk = Activator.CreateInstance(type);
            Com.Call(_sdk, "SwitchMode");
            Enumerate();
        }

        private void Enumerate()
        {
            _devices.Clear();
            object collection = Com.Call(_sdk, "Enumerate", 0);
            int count = Com.Count(collection);
            for (int i = 0; i < count; i++)
            {
                object handle = Com.Item(collection, i);
                AuraDevice d = new AuraDevice();
                d.Index = i;
                d.Handle = handle;
                d.Name = ReadString(handle, "Name");
                d.Type = ReadUInt(handle, "Type");
                d.Width = (int)ReadUInt(handle, "Width");
                d.Height = (int)ReadUInt(handle, "Height");

                object lights = Com.Get(handle, "Lights");
                int lightCount = Com.Count(lights);
                d.Lights = new object[lightCount];
                for (int j = 0; j < lightCount; j++) d.Lights[j] = Com.Item(lights, j);

                d.Written = new int[lightCount];
                for (int j = 0; j < lightCount; j++) d.Written[j] = -1;
                _devices.Add(d);
            }
        }

        /// <summary>Writes one colour per LED and applies it. Unchanged LEDs are skipped. Returns true if anything was sent.</summary>
        public bool Write(AuraDevice d, int[] colors)
        {
            bool changed = false;
            int n = Math.Min(colors.Length, d.Lights.Length);
            for (int j = 0; j < n; j++)
            {
                int c = colors[j] & 0xFFFFFF;
                if (d.Written[j] == c) continue;
                SetColor(d.Lights[j], c);
                d.Written[j] = c;
                changed = true;
            }
            if (changed) Com.Call(d.Handle, "Apply");
            return changed;
        }

        public bool Fill(AuraDevice d, int color)
        {
            int[] colors = new int[d.Lights.Length];
            for (int j = 0; j < colors.Length; j++) colors[j] = color;
            return Write(d, colors);
        }

        /// <summary>Forget what was written so the next Write repaints every LED.</summary>
        public void Invalidate()
        {
            foreach (AuraDevice d in _devices)
            {
                for (int j = 0; j < d.Written.Length; j++) d.Written[j] = -1;
            }
        }

        private void SetColor(object light, int bgr)
        {
            if (!_colorAsUnsigned)
            {
                try
                {
                    Com.Set(light, "Color", bgr);
                    return;
                }
                catch (COMException ex)
                {
                    if (ex.ErrorCode != DispTypeMismatch) throw;
                    _colorAsUnsigned = true;
                }
            }
            Com.Set(light, "Color", unchecked((uint)bgr));
        }

        /// <summary>Hands the LEDs back to Armoury Crate.</summary>
        public void Release()
        {
            if (_sdk == null) return;
            try
            {
                Com.Call(_sdk, "ReleaseControl", 0);
            }
            catch (Exception)
            {
                try
                {
                    Com.Call(_sdk, "ReleaseControl");
                }
                catch (Exception)
                {
                    // Older SDKs release control when the process exits.
                }
            }
            Abandon();
        }

        /// <summary>Drops all COM references without calling into the SDK (use after the service died).</summary>
        public void Abandon()
        {
            foreach (AuraDevice d in _devices)
            {
                foreach (object light in d.Lights) Com.Release(light);
                Com.Release(d.Handle);
            }
            _devices.Clear();
            Com.Release(_sdk);
            _sdk = null;
        }

        public void Dispose()
        {
            Release();
        }

        public static string TypeName(uint type)
        {
            switch (type)
            {
                case 0x00010000: return "motherboard";
                case 0x00011000: return "motherboard LED strip / ARGB header";
                case 0x00012000: return "all-in-one PC";
                case 0x00020000: return "graphics card";
                case 0x00030000: return "display";
                case 0x00040000: return "headset";
                case 0x00050000: return "microphone";
                case 0x00060000: return "external drive";
                case 0x00061000: return "external BD drive";
                case 0x00070000: return "memory (DRAM)";
                case 0x00080000: return "keyboard";
                case 0x00080001: return "keyboard (5-zone)";
                case 0x00081000: return "notebook keyboard";
                case 0x00081001: return "notebook keyboard (4-zone)";
                case 0x00090000: return "mouse";
                case 0x00091000: return "mousepad";
                case 0x000B0000: return "chassis";
                case 0x000C0000: return "projector";
                case 0x000D1000: return "water cooler";
                default: return "other";
            }
        }

        private static string ReadString(object o, string name)
        {
            try
            {
                object v = Com.Get(o, name);
                return v == null ? "" : v.ToString();
            }
            catch (Exception)
            {
                return "?";
            }
        }

        private static uint ReadUInt(object o, string name)
        {
            try
            {
                object v = Com.Get(o, name);
                return v == null ? 0u : unchecked((uint)Convert.ToInt64(v, CultureInfo.InvariantCulture));
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
