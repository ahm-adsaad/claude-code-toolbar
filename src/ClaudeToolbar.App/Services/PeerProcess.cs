using System.Net;
using System.Runtime.InteropServices;

namespace ClaudeToolbar.App.Services;

/// <summary>Finds the process on the other end of a loopback TCP connection through the owner-pid TCP table.</summary>
public static class PeerProcess
{
    private const int AF_INET = 2;
    private const int TCP_TABLE_OWNER_PID_ALL = 5;

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint State;
        public uint LocalAddr;
        public uint LocalPort;
        public uint RemoteAddr;
        public uint RemotePort;
        public uint OwningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, [MarshalAs(UnmanagedType.Bool)] bool bOrder, int ulAf, int tableClass, uint reserved);

    /// <summary>Pid owning the connection whose local port is the client's and whose remote port is the listener's, or null.</summary>
    public static int? OwningPid(int clientPort, int listenerPort)
    {
        var size = 0;
        _ = GetExtendedTcpTable(IntPtr.Zero, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
        if (size <= 0) return null;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(buffer, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0) != 0) return null;
            var count = Marshal.ReadInt32(buffer);
            var rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            var row = buffer + 4;
            for (var i = 0; i < count; i++, row += rowSize)
            {
                var entry = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(row);
                if (Port(entry.LocalPort) == clientPort && Port(entry.RemotePort) == listenerPort)
                    return (int)entry.OwningPid;
            }
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // The table keeps ports in network byte order in the low 16 bits.
    private static int Port(uint raw) => (ushort)IPAddress.NetworkToHostOrder((short)(raw & 0xFFFF));
}
