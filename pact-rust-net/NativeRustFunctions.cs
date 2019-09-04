namespace PactNet.Core
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Text;
#if !USE_NETSTANDARD1
    using System.Security;
    using System.Runtime.ConstrainedExecution;
    using System.Security.Permissions;
#endif

#if !USE_NETSTANDARD1
    [SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode = true)]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
#endif
    public sealed class PortHandle : SafeHandle
    {
        private PortHandle(IntPtr invalidHandleValue, bool ownsHandle) : base(invalidHandleValue, ownsHandle)
        {

        }

        public PortHandle() : this(new IntPtr(int.MinValue), true)
        {

        }

#if !USE_NETSTANDARD1
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
#endif
        protected override bool ReleaseHandle()
        {
            NativeRustFunctions.CleanupMockServer(this.handle.ToInt32());
            return true;
        }

        public int GetPortNumber() => this.handle.ToInt32();

        public override bool IsInvalid => this.handle.ToInt32() < 0;
    }

#if !USE_NETSTANDARD1
    [SuppressUnmanagedCodeSecurity]
#endif
    public static unsafe class NativeRustFunctions
    {
        private const string RustPactSharedLibraryName = "pact_mock_server";

        [DllImport(RustPactSharedLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "create_mock_server_ffi")]
        private static extern PortHandle CreateMockServerImpl(byte* pactDescription, int port);

        public static PortHandle CreateMockServer(string pactDescription, int port = 0)
        {
            if (pactDescription == null)
                throw new ArgumentNullException(nameof(pactDescription));

            fixed (byte* p = CStyleStringFromSystemString(pactDescription))
            {
                var result = CreateMockServerImpl(p, port);
                if (result.GetPortNumber() > 0)
                    return result;
                switch (result.GetPortNumber())
                {
                    case -1:
                        Debug.Assert(false);
                        throw new Exception();
                    case -2:
                        throw new ArgumentException("The pact JSON could not be parsed", nameof(pactDescription));
                    case -3:
                        throw new Exception("mock server could not be started");
                    case -4:
                        throw new Exception("internal error");
                    default:
                        throw new Exception("unknown error happened");
                }
            }
        }

        [DllImport(RustPactSharedLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mock_server_matched_ffi")]
        public static extern int MockServerMatched(PortHandle port);

        [DllImport(RustPactSharedLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "cleanup_mock_server_ffi")]
#if !USE_NETSTANDARD1
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
#endif
        public static extern int CleanupMockServer(int port);

        [DllImport(RustPactSharedLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mock_server_mismatches_ffi")]
        private static extern byte* MockServerMismatchesImpl(PortHandle port);

        public static string MockServerMismatches(PortHandle port)
        {
            var result = MockServerMismatchesImpl(port);
            if (result == null)
            {
                throw new ArgumentException($"{nameof(MockServerMismatches)} failed on invalid port handle");
            }

            return SystemStringFromCStyleString(result);
        }

        [DllImport(RustPactSharedLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "write_pact_file_ffi")]
        private static extern int WritePactFileImpl(PortHandle port, byte* pactFilePath);

        public static int WritePactFile(PortHandle port, string pactFilePath)
        {
            fixed (byte* path = CStyleStringFromSystemString(pactFilePath))
            {
                return WritePactFileImpl(port, path);
            }
        }

        public static string SystemStringFromCStyleString(byte* input)
        {
            int length = 0;
            while (input[length] != 0)
            {
                length++;
            }

            var encoding = Encoding.UTF8;
            var charCount = encoding.GetCharCount(input, length);
            var outputString = new string('\0', charCount);
            fixed (char* output = outputString)
            {
                encoding.GetChars(input, length, output, charCount);
            }

            return outputString;
        }

        public static byte[] CStyleStringFromSystemString(string input)
        {
            var encoding = Encoding.UTF8;

            fixed (char* src = input)
            {
                var output = new byte[encoding.GetByteCount(src, input.Length) + 1];
                fixed (byte* dst = output)
                {
                    var r = encoding.GetBytes(src, input.Length, dst, output.Length);
                    Debug.Assert(r + 1 == output.Length);
                    return output;
                }
            }
        }
    }
}
