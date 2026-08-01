using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace TiHiY.StreamControlCenter.Services;

public sealed class CredentialService
{
    private const string Prefix = "TiHiY.StreamControlMini.";
    private const string LegacyPrefix = "TiHiY.StreamControlCenter.";
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;

    public void SavePassword(string password) => SaveSecret("OBS", password);
    public string LoadPassword() => LoadSecret("OBS");
    public void DeletePassword() => DeleteSecret("OBS");

    public void SaveSecret(string key, string value) => WriteCredential(Prefix + key, value);

    public string LoadSecret(string key)
    {
        var current = ReadCredential(Prefix + key);
        if (!string.IsNullOrWhiteSpace(current)) return current;

        var legacy = ReadCredential(LegacyPrefix + key);
        if (!string.IsNullOrWhiteSpace(legacy))
        {
            try { WriteCredential(Prefix + key, legacy); } catch { }
            return legacy;
        }
        return string.Empty;
    }

    public void DeleteSecret(string key) => CredDelete(Prefix + key, CredTypeGeneric, 0);

    private static void WriteCredential(string target, string value)
    {
        var bytes = Encoding.Unicode.GetBytes(value ?? string.Empty);
        var blob = Marshal.AllocCoTaskMem(Math.Max(bytes.Length, 2));
        try
        {
            if (bytes.Length > 0) Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new NativeCredential
            {
                Type = CredTypeGeneric,
                TargetName = target,
                CredentialBlobSize = bytes.Length,
                CredentialBlob = blob,
                Persist = CredPersistLocalMachine,
                UserName = Environment.UserName
            };
            if (!CredWrite(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            for (var i = 0; i < bytes.Length; i++) Marshal.WriteByte(blob, i, 0);
            Marshal.FreeCoTaskMem(blob);
        }
    }

    private static string ReadCredential(string target)
    {
        if (!CredRead(target, CredTypeGeneric, 0, out var ptr)) return string.Empty;
        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(ptr);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize <= 0) return string.Empty;
            return Marshal.PtrToStringUni(credential.CredentialBlob, credential.CredentialBlobSize / 2) ?? string.Empty;
        }
        finally { CredFree(ptr); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref NativeCredential credential, uint flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credentialPtr);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
