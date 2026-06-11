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

