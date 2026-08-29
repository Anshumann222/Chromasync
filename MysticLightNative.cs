using System.Runtime.InteropServices;

namespace ChromaSync;

/// <summary>
/// Direct P/Invoke bindings to MysticLight_SDK.dll, matching the exported
/// MLAPI_* functions documented in MSI's Mystic Light SDK.
///
/// NOTE: MLAPI_GetDeviceInfo returns two SAFEARRAYs of BSTR (device type
/// names, and led counts — both as strings). The SafeArray/BSTR marshaling
/// below follows the standard .NET pattern for that; this is the one part
/// of the app I couldn't compile-test end-to-end (no Windows box in this
/// environment), so if you hit a MarshalDirectiveException on first run,
/// that's the first place to look — see the README for reference wrappers.
/// </summary>
internal static class MysticLightNative
{
    private const string DllName = "MysticLight_SDK.dll";

    [DllImport(DllName, EntryPoint = "MLAPI_Initialize", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Initialize();

    [DllImport(DllName, EntryPoint = "MLAPI_Release", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Release();

    [DllImport(DllName, EntryPoint = "MLAPI_GetErrorMessage", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetErrorMessage(int errorCode, [MarshalAs(UnmanagedType.BStr)] out string desc);

    [DllImport(DllName, EntryPoint = "MLAPI_GetDeviceInfo", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetDeviceInfo(
        [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out string[] devType,
        [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out string[] ledCount);

    [DllImport(DllName, EntryPoint = "MLAPI_GetLedInfo", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetLedInfo(
        [MarshalAs(UnmanagedType.BStr)] string type,
        int index,
        [MarshalAs(UnmanagedType.BStr)] out string name,
        [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out string[] ledStyles);

    [DllImport(DllName, EntryPoint = "MLAPI_GetLedName", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetLedName(
        [MarshalAs(UnmanagedType.BStr)] string type,
        [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out string[] ledName);

    [DllImport(DllName, EntryPoint = "MLAPI_GetLedColor", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetLedColor(
        [MarshalAs(UnmanagedType.BStr)] string type,
        int index, out int r, out int g, out int b);

    [DllImport(DllName, EntryPoint = "MLAPI_SetLedColor", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SetLedColor(
        [MarshalAs(UnmanagedType.BStr)] string type,
        int index, int r, int g, int b);

    [DllImport(DllName, EntryPoint = "MLAPI_SetLedStyle", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SetLedStyle(
        [MarshalAs(UnmanagedType.BStr)] string type,
        int index,
        [MarshalAs(UnmanagedType.BStr)] string style);

    [DllImport(DllName, EntryPoint = "MLAPI_SetLedColorsSync", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SetLedColorsSync(
        [MarshalAs(UnmanagedType.BStr)] string type,
        [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] ref string[] ledName,
        int[] r, int[] g, int[] b);
}
