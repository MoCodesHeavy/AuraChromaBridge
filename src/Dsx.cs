// DSX (DualSense controller app from Steam): sets the controller light bar through DSX's local UDP "mod" port.
// Message format: {"instructions":[{"type":2,"parameters":[controllerIndex,R,G,B]}]}  (type 2 = RGBUpdate, 7 = ResetToUserSettings)

using System;
using System.Globalization;
using System.Net.Sockets;
using System.Text;

namespace AuraChromaBridge
{
    internal sealed class Dsx : IDisposable
    {
        private UdpClient _udp;
        private readonly int _port;

        public Dsx(int port)
        {
            _port = port;
            _udp = new UdpClient();
            _udp.Connect("127.0.0.1", port);
        }

        public int Port { get { return _port; } }

        /// <summary>color is 0x00BBGGRR. Returns false if the packet could not be sent.</summary>
        public bool SetColor(int color)
        {
            int r = color & 0xFF, g = (color >> 8) & 0xFF, b = (color >> 16) & 0xFF;
            return Send(string.Format(CultureInfo.InvariantCulture,
                "{{\"instructions\":[{{\"type\":2,\"parameters\":[0,{0},{1},{2}]}}]}}", r, g, b));
        }

        /// <summary>Hands the light bar back to whatever is set in DSX.</summary>
        public bool Reset()
        {
            return Send("{\"instructions\":[{\"type\":7,\"parameters\":[0]}]}");
        }

        private bool Send(string json)
        {
            try
            {
                byte[] bytes = Encoding.ASCII.GetBytes(json);
                _udp.Send(bytes, bytes.Length);
                // DSX answers every packet; drain replies so the socket buffer never fills.
                while (_udp.Available > 0)
                {
                    System.Net.IPEndPoint ep = null;
                    _udp.Receive(ref ep);
                }
                return true;
            }
            catch (Exception)
            {
                // DSX not running: Windows reports the closed port on the next call. Recreate the socket and carry on.
                try { _udp.Close(); } catch (Exception) { }
                try { _udp = new UdpClient(); _udp.Connect("127.0.0.1", _port); } catch (Exception) { }
                return false;
            }
        }

        public void Dispose()
        {
            try { _udp.Close(); } catch (Exception) { }
        }
    }
}
