using System.Runtime.InteropServices;
using EtwSuite.Core;
using EtwSuite.Etw.Native;

namespace EtwSuite.Etw;

public sealed class EtwProviderEnumerationException : Exception
{
    public EtwProviderEnumerationException(uint errorCode)
        : base($"Failed to enumerate ETW providers. TDH returned Win32 error {errorCode}.")
    {
        ErrorCode = errorCode;
    }

    public uint ErrorCode { get; }
}


public sealed class EtwProviderCatalog : IEtwProviderCatalog
{
    public Task<IReadOnlyList<EtwProviderInfo>> EnumerateProvidersAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() => EnumerateProviders(cancellationToken), cancellationToken);
    }

    private static IReadOnlyList<EtwProviderInfo> EnumerateProviders(CancellationToken cancellationToken)
    {
        uint bufferSize = 0;
        uint result = TdhNative.TdhEnumerateProviders(IntPtr.Zero, ref bufferSize);
        if (result != TdhNative.ErrorInsufficientBuffer && result != TdhNative.ErrorSuccess)
        {
            throw new EtwProviderEnumerationException(result);
        }

        if (bufferSize == 0)
        {
            return Array.Empty<EtwProviderInfo>();
        }

        IntPtr buffer = Marshal.AllocHGlobal(checked((int)bufferSize));
        try
        {
            result = TdhNative.TdhEnumerateProviders(buffer, ref bufferSize);
            if (result != TdhNative.ErrorSuccess)
            {
                throw new EtwProviderEnumerationException(result);
            }

            var header = Marshal.PtrToStructure<TdhNative.ProviderEnumerationInfoHeader>(buffer);
            var providers = new List<EtwProviderInfo>(checked((int)header.NumberOfProviders));
            int headerSize = Marshal.SizeOf<TdhNative.ProviderEnumerationInfoHeader>();
            int providerInfoSize = Marshal.SizeOf<TdhNative.TraceProviderInfo>();

            for (int i = 0; i < header.NumberOfProviders; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IntPtr providerInfoAddress = IntPtr.Add(buffer, headerSize + (i * providerInfoSize));
                var providerInfo = Marshal.PtrToStructure<TdhNative.TraceProviderInfo>(providerInfoAddress);
                string name = ReadProviderName(buffer, providerInfo.ProviderNameOffset);

                providers.Add(new EtwProviderInfo(
                    name,
                    providerInfo.ProviderGuid,
                    MapSchemaSource(providerInfo.SchemaSource)));
            }

            return providers
                .OrderBy(provider => provider.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(provider => provider.Id)
                .ToArray();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string ReadProviderName(IntPtr buffer, uint providerNameOffset)
    {
        if (providerNameOffset == 0)
        {
            return "(unknown provider)";
        }

        IntPtr providerNameAddress = IntPtr.Add(buffer, checked((int)providerNameOffset));
        return Marshal.PtrToStringUni(providerNameAddress) ?? "(unknown provider)";
    }

    private static EtwProviderSchemaSource MapSchemaSource(uint schemaSource)
    {
        return Enum.IsDefined(typeof(EtwProviderSchemaSource), (int)schemaSource)
            ? (EtwProviderSchemaSource)schemaSource
            : EtwProviderSchemaSource.Unknown;
    }
}

