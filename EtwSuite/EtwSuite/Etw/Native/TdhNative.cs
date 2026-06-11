using System.Runtime.InteropServices;

namespace EtwSuite.Etw.Native;

internal static class TdhNative
{
    internal const uint ErrorSuccess = 0;
    internal const uint ErrorInsufficientBuffer = 122;

    [DllImport("tdh.dll", ExactSpelling = true)]
    internal static extern uint TdhEnumerateProviders(
        IntPtr providerEnumerationInfo,
        ref uint bufferSize);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct ProviderEnumerationInfoHeader
    {
        public readonly uint NumberOfProviders;
        public readonly uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct TraceProviderInfo
    {
        public readonly Guid ProviderGuid;
        public readonly uint SchemaSource;
        public readonly uint ProviderNameOffset;
    }
}
