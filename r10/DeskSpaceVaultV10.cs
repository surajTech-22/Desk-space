using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DeskSpace;

// The vault NEVER writes secrets to workspace.json, notes or logs.
// AES-256-GCM, fresh salt and nonce on every save, master-password PBKDF2.
// The key lives only as long as this unlocked vault object is alive.
public sealed class DeskSpaceVaultV10 : IDisposable
{
    public sealed class Secret
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Label { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Website { get; set; } = "";
    }
    private sealed class Envelope
    {
        public int Format { get; set; } = 1;
        public int Iterations { get; set; } = 600000;
        public string Salt { get; set; } = "";
        public string Nonce { get; set; } = "";
        public string Ciphertext { get; set; } = "";
        public string Tag { get; set; } = "";
    }
    private readonly string path = Path.Combine(WorkspaceStore.Root, "password-vault-v10.enc");
    private byte[]? key;
    private byte[]? salt;
    private List<Secret> entries = new();
    private bool disposed;
    public bool IsUnlocked => key != null && !disposed;
    public bool IsInitialized => File.Exists(path);
    public IReadOnlyList<Secret> Entries => IsUnlocked ? entries.AsReadOnly() : Array.Empty<Secret>();

    public void Unlock(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            // Existing vaults are allowed shorter legacy passwords only if the
            // file already exists, without weakening the new-vault requirement.
            if (!IsInitialized || string.IsNullOrEmpty(password))
                throw new ArgumentException("Use a master password with at least 12 characters.");
        }
        if (disposed) throw new ObjectDisposedException(nameof(DeskSpaceVaultV10));
        byte[]? derived = null;
        try
        {
            if (!IsInitialized)
            {
                salt = RandomNumberGenerator.GetBytes(16);
                derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600000, HashAlgorithmName.SHA256, 32);
                entries = new List<Secret>();
                key = derived; derived = null;
                Save();
                return;
            }
            var doc = JsonSerializer.Deserialize<Envelope>(File.ReadAllText(path))
                ?? throw new CryptographicException("Invalid vault header.");
            if (doc.Format != 1 || doc.Iterations < 200000 || doc.Iterations > 3000000)
                throw new CryptographicException("Unsupported vault format.");
            var localSalt = Convert.FromBase64String(doc.Salt);
            var nonce = Convert.FromBase64String(doc.Nonce);
            var ciphertext = Convert.FromBase64String(doc.Ciphertext);
            var tag = Convert.FromBase64String(doc.Tag);
            if (nonce.Length != 12 || tag.Length != 16 || localSalt.Length != 16 || ciphertext.Length > 16_000_000)
                throw new CryptographicException("Invalid vault encryption parameters.");
            derived = Rfc2898DeriveBytes.Pbkdf2(password, localSalt, doc.Iterations, HashAlgorithmName.SHA256, 32);
            var plain = new byte[ciphertext.Length];
            try
            {
                using (var aes = new AesGcm(derived, 16))
                    aes.Decrypt(nonce, ciphertext, tag, plain);
                entries = JsonSerializer.Deserialize<List<Secret>>(plain) ?? new();
                salt = localSalt;
                key = derived; derived = null;
            }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        finally { if (derived != null) CryptographicOperations.ZeroMemory(derived); }
    }

    public void Upsert(Secret secret)
    {
        RequireUnlocked();
        if (string.IsNullOrWhiteSpace(secret.Label)) throw new ArgumentException("A title is required.");
        var idx = entries.FindIndex(s=>s.Id==secret.Id);
        if (idx < 0) entries.Add(secret); else entries[idx] = secret;
        Save();
    }
    public void Delete(Guid id)
    {
        RequireUnlocked();
        entries.RemoveAll(x=>x.Id==id);
        Save();
    }
    private void Save()
    {
        RequireUnlocked();
        Directory.CreateDirectory(WorkspaceStore.Root);
        var json = JsonSerializer.SerializeToUtf8Bytes(entries);
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var ciphertext = new byte[json.Length];
            var tag = new byte[16];
            using (var aes = new AesGcm(key!, 16)) aes.Encrypt(nonce, json, ciphertext, tag);
            var doc = new Envelope
            {
                Salt = Convert.ToBase64String(salt!),
                Nonce = Convert.ToBase64String(nonce),
                Tag = Convert.ToBase64String(tag),
                Ciphertext = Convert.ToBase64String(ciphertext)
            };
            var tmp = path + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonSerializer.Serialize(doc),new UTF8Encoding(false));
                File.Move(tmp, path, true);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
        finally { CryptographicOperations.ZeroMemory(json); }
    }
    private void RequireUnlocked()
    {
        if (!IsUnlocked) throw new InvalidOperationException("Unlock the password vault first.");
    }
    public void Lock()
    {
        if (key != null) { CryptographicOperations.ZeroMemory(key); key = null; }
        salt = null;
        entries.Clear();
    }
    public void Dispose() { Lock(); disposed = true; GC.SuppressFinalize(this); }
}
