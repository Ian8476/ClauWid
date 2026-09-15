using System.Runtime.InteropServices;
using System.Windows;

namespace ClaudeUsageWidget.Presentation.Wpf.Displays;

/// <summary>
/// Pregunta a Windows por los monitores en cada llamada. No usa Screen.AllScreens de WinForms
/// porque cachea la lista hasta recibir su propio aviso, y aqui se consulta justo al conectar
/// o desconectar un monitor, cuando esa cache todavia puede describir el escritorio anterior.
/// </summary>
public sealed class Win32DisplayLayoutProvider : IDisplayLayoutProvider
{
    // ENUM_CURRENT_SETTINGS: el modo en uso, no uno de los disponibles.
    private const int CurrentSettingsMode = -1;

    // MONITORINFOF_PRIMARY.
    private const uint PrimaryMonitorFlag = 1;

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr deviceContext, ref NativeRect bounds, IntPtr data);

    public DisplayLayout GetCurrent()
    {
        var monitors = new List<DisplayMonitor>();

        EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (IntPtr monitor, IntPtr deviceContext, ref NativeRect bounds, IntPtr data) =>
            {
                if (ReadMonitor(monitor) is { } display)
                {
                    monitors.Add(display);
                }

                return true;
            },
            IntPtr.Zero);

        return new DisplayLayout(monitors);
    }

    /// <summary>
    /// El area de trabajo sale de GetMonitorInfo, en las mismas coordenadas que la ventana.
    /// La resolucion y la posicion reales salen de EnumDisplaySettings: con escalas distintas
    /// por monitor, GetMonitorInfo las devuelve virtualizadas por DPI y un monitor de
    /// 1920x1080 podria figurar como 2400x1350.
    /// </summary>
    private static DisplayMonitor? ReadMonitor(IntPtr monitor)
    {
        var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return null;
        }

        var mode = new DevMode { Size = (short)Marshal.SizeOf<DevMode>() };
        var physicalBounds = EnumDisplaySettings(info.DeviceName, CurrentSettingsMode, ref mode)
            ? new Int32Rect(mode.PositionX, mode.PositionY, mode.PelsWidth, mode.PelsHeight)
            : info.Monitor.ToInt32Rect();

        return new DisplayMonitor(
            physicalBounds,
            info.WorkArea.ToInt32Rect(),
            (info.Flags & PrimaryMonitorFlag) != 0);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr deviceContext, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplaySettingsW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNumber, ref DevMode mode);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly Int32Rect ToInt32Rect() => new(Left, Top, Right - Left, Bottom - Top);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    /// <summary>DEVMODEW en su variante de pantalla. El orden y tamano de cada campo importan.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        public short SpecVersion;
        public short DriverVersion;
        public short Size;
        public short DriverExtra;
        public int Fields;
        public int PositionX;
        public int PositionY;
        public int DisplayOrientation;
        public int DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TrueTypeOption;
        public short Collate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FormName;
        public short LogPixels;
        public int BitsPerPixel;
        public int PelsWidth;
        public int PelsHeight;
        public int DisplayFlags;
        public int DisplayFrequency;
        public int IcmMethod;
        public int IcmIntent;
        public int MediaType;
        public int DitherType;
        public int Reserved1;
        public int Reserved2;
        public int PanningWidth;
        public int PanningHeight;
    }
}
