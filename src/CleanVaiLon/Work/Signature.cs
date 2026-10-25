// Kiểm tra chữ ký số Authenticode như Get-AuthenticodeSignature: chữ ký nhúng trong tệp hoặc trong catalog của Windows.
// Không kiểm tra thu hồi chứng chỉ và chỉ dùng bộ nhớ đệm, nên không bao giờ kết nối mạng.
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CleanVaiLon
{
    public static class Signature
    {
        static readonly Guid ActionGenericVerifyV2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WINTRUST_FILE_INFO { public int cbStruct; public string pcwszFilePath; public IntPtr hFile; public IntPtr pgKnownSubject; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WINTRUST_CATALOG_INFO
        {
            public int cbStruct; public int dwCatalogVersion; public string pcwszCatalogFilePath; public string pcwszMemberTag;
            public string pcwszMemberFilePath; public IntPtr hMemberFile; public IntPtr pbCalculatedFileHash; public int cbCalculatedFileHash;
            public IntPtr pcCatalogContext; public IntPtr hCatAdmin;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WINTRUST_DATA
        {
            public int cbStruct; public IntPtr pPolicyCallbackData; public IntPtr pSIPClientData; public int dwUIChoice; public int fdwRevocationChecks;
            public int dwUnionChoice; public IntPtr pInfo; public int dwStateAction; public IntPtr hWVTStateData; public IntPtr pwszURLReference;
            public int dwProvFlags; public int dwUIContext; public IntPtr pSignatureSettings;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct CATALOG_INFO { public int cbStruct; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string wszCatalogFile; }

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode)] static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, ref WINTRUST_DATA data);
        [DllImport("wintrust.dll", SetLastError = true)] static extern bool CryptCATAdminAcquireContext(out IntPtr h, IntPtr subsystem, int flags);
        [DllImport("wintrust.dll")] static extern bool CryptCATAdminReleaseContext(IntPtr h, int flags);
        [DllImport("wintrust.dll", SetLastError = true)] static extern bool CryptCATAdminCalcHashFromFileHandle(IntPtr file, ref int size, byte[] hash, int flags);
        [DllImport("wintrust.dll")] static extern IntPtr CryptCATAdminEnumCatalogFromHash(IntPtr h, byte[] hash, int size, int flags, IntPtr prev);
        [DllImport("wintrust.dll")] static extern bool CryptCATAdminReleaseCatalogContext(IntPtr h, IntPtr cat, int flags);
        [DllImport("wintrust.dll", CharSet = CharSet.Unicode)] static extern bool CryptCATCatalogInfoFromContext(IntPtr cat, ref CATALOG_INFO info, int flags);

        const int WTD_UI_NONE = 2, WTD_REVOKE_NONE = 0, WTD_CHOICE_FILE = 1, WTD_CHOICE_CATALOG = 2;
        const int WTD_STATEACTION_VERIFY = 1, WTD_STATEACTION_CLOSE = 2;
        const int WTD_CACHE_ONLY_URL_RETRIEVAL = 0x1000, WTD_REVOCATION_CHECK_NONE = 0x10;
        const int TRUST_E_NOSIGNATURE = unchecked((int)0x800B0100), TRUST_E_BAD_DIGEST = unchecked((int)0x80096010), CRYPT_E_BAD_MSG = unchecked((int)0x8009200D);
        const int TRUST_E_PROVIDER_UNKNOWN = unchecked((int)0x800B0001), TRUST_E_EXPLICIT_DISTRUST = unchecked((int)0x800B0111), NTE_BAD_ALGID = unchecked((int)0x80090008);
        const int TRUST_E_SUBJECT_FORM_UNKNOWN = unchecked((int)0x800B0003), CERT_E_UNTRUSTEDROOT = unchecked((int)0x800B0109), CERT_E_CHAINING = unchecked((int)0x800B010A);

        static int Verify(int choice, IntPtr info)
        {
            var guid = ActionGenericVerifyV2;
            var d = new WINTRUST_DATA
            {
                cbStruct = Marshal.SizeOf(typeof(WINTRUST_DATA)), dwUIChoice = WTD_UI_NONE, fdwRevocationChecks = WTD_REVOKE_NONE,
                dwUnionChoice = choice, pInfo = info, dwStateAction = WTD_STATEACTION_VERIFY,
                dwProvFlags = WTD_CACHE_ONLY_URL_RETRIEVAL | WTD_REVOCATION_CHECK_NONE,
            };
            int r = WinVerifyTrust(IntPtr.Zero, ref guid, ref d);
            d.dwStateAction = WTD_STATEACTION_CLOSE;
            WinVerifyTrust(IntPtr.Zero, ref guid, ref d);
            return r;
        }

        static int VerifyEmbedded(string path)
        {
            var fi = new WINTRUST_FILE_INFO { cbStruct = Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)), pcwszFilePath = path };
            IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf(fi));
            try { Marshal.StructureToPtr(fi, p, false); return Verify(WTD_CHOICE_FILE, p); }
            finally { Marshal.DestroyStructure(p, typeof(WINTRUST_FILE_INFO)); Marshal.FreeHGlobal(p); }
        }

        // Tìm tệp trong catalog của Windows; trả về null nếu không có trong catalog nào.
        static int? VerifyCatalog(string path)
        {
            if (!CryptCATAdminAcquireContext(out IntPtr admin, IntPtr.Zero, 0)) return null;
            try
            {
                byte[] hash;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    int size = 0;
                    var h = fs.SafeFileHandle.DangerousGetHandle();
                    CryptCATAdminCalcHashFromFileHandle(h, ref size, null, 0);
                    if (size <= 0) return null;
                    hash = new byte[size];
                    if (!CryptCATAdminCalcHashFromFileHandle(h, ref size, hash, 0)) return null;
                }
                IntPtr cat = CryptCATAdminEnumCatalogFromHash(admin, hash, hash.Length, 0, IntPtr.Zero);
                if (cat == IntPtr.Zero) return null;
                try
                {
                    var ci = new CATALOG_INFO { cbStruct = Marshal.SizeOf(typeof(CATALOG_INFO)) };
                    if (!CryptCATCatalogInfoFromContext(cat, ref ci, 0)) return null;
                    string tag = BitConverter.ToString(hash).Replace("-", "");
                    IntPtr hashPtr = Marshal.AllocHGlobal(hash.Length);
                    Marshal.Copy(hash, 0, hashPtr, hash.Length);
                    var info = new WINTRUST_CATALOG_INFO
                    {
                        cbStruct = Marshal.SizeOf(typeof(WINTRUST_CATALOG_INFO)), pcwszCatalogFilePath = ci.wszCatalogFile,
                        pcwszMemberTag = tag, pcwszMemberFilePath = path, pbCalculatedFileHash = hashPtr, cbCalculatedFileHash = hash.Length, hCatAdmin = admin,
                    };
                    IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf(info));
                    try { Marshal.StructureToPtr(info, p, false); return Verify(WTD_CHOICE_CATALOG, p); }
                    finally { Marshal.DestroyStructure(p, typeof(WINTRUST_CATALOG_INFO)); Marshal.FreeHGlobal(p); Marshal.FreeHGlobal(hashPtr); }
                }
                finally { CryptCATAdminReleaseCatalogContext(admin, cat, 0); }
            }
            catch { return null; }
            finally { CryptCATAdminReleaseContext(admin, 0); }
        }

        // Trạng thái như SignatureStatus của PowerShell: Valid, NotSigned, HashMismatch, NotTrusted, NotSupportedFileFormat, Incompatible, UnknownError.
        public static string Status(string path)
        {
            if (!File.Exists(path)) return "UnknownError";
            int r = VerifyEmbedded(path);
            if (r == TRUST_E_NOSIGNATURE || r == TRUST_E_SUBJECT_FORM_UNKNOWN || r == TRUST_E_PROVIDER_UNKNOWN)
            {
                var c = VerifyCatalog(path);
                if (c.HasValue) r = c.Value;
            }
            switch (r)
            {
                case 0: return "Valid";
                case NTE_BAD_ALGID: return "Incompatible";
                case TRUST_E_NOSIGNATURE: return "NotSigned";
                case TRUST_E_BAD_DIGEST: case CRYPT_E_BAD_MSG: return "HashMismatch";
                case TRUST_E_PROVIDER_UNKNOWN: case TRUST_E_SUBJECT_FORM_UNKNOWN: return "NotSupportedFileFormat";
                case TRUST_E_EXPLICIT_DISTRUST: case CERT_E_UNTRUSTEDROOT: case CERT_E_CHAINING: return "NotTrusted";
                default: return "UnknownError";
            }
        }
    }
}
