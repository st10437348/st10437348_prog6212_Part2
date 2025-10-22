using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CMCS.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace CMCS.Services
{
    public class InMemoryStore
    {
        private readonly IWebHostEnvironment _env;
        private readonly byte[] _key;
        private readonly string _privateRoot;    

        private readonly bool _persistEnabled;
        private readonly bool _persistEncrypt; 
        private readonly string _persistPath;    

        private int _userCounter = 0;
        private int _lecturerCounter = 0;
        private int _claimCounter = 0;
        private int _approvalCounter = 0;
        private int _documentCounter = 0;

        private readonly object _lock = new();
        private readonly SemaphoreSlim _saveLock = new(1, 1);

        private readonly ConcurrentDictionary<string, UserAccount> _usersByName = new();
        private readonly ConcurrentDictionary<int, Lecturer> _lecturersByUserId = new();
        private readonly ConcurrentDictionary<int, Lecturer> _lecturersByLecturerId = new();
        private readonly ConcurrentDictionary<int, Claim> _claims = new();
        private readonly ConcurrentDictionary<int, List<Approval>> _approvalsByClaim = new();
        private readonly ConcurrentDictionary<int, SupportingDocument> _documents = new();
        private readonly ConcurrentDictionary<int, List<int>> _docIdsByClaim = new();

        private sealed class Snapshot
        {
            public int UserCounter { get; set; }
            public int LecturerCounter { get; set; }
            public int ClaimCounter { get; set; }
            public int ApprovalCounter { get; set; }
            public int DocumentCounter { get; set; }

            public List<UserAccount> Users { get; set; } = new();
            public List<Lecturer> Lecturers { get; set; } = new();
            public List<Claim> Claims { get; set; } = new();

            public List<Approval> Approvals { get; set; } = new();

            public List<SupportingDocument> Documents { get; set; } = new();

            public List<Link> ClaimDocs { get; set; } = new();

            public sealed class Link
            {
                public int ClaimId { get; set; }
                public int DocumentId { get; set; }
            }
        }

        public InMemoryStore(
            IWebHostEnvironment env,
            IOptions<SecurityOptions> sec,
            IOptions<PersistenceOptions>? persist = null)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));

            if (string.IsNullOrWhiteSpace(sec.Value.EncryptionKeyBase64))
                throw new InvalidOperationException("Security:EncryptionKeyBase64 missing from appsettings.");

            _key = Convert.FromBase64String(sec.Value.EncryptionKeyBase64);
            if (_key.Length != 32)
                throw new InvalidOperationException("Security: Encryption key must decode to exactly 32 bytes (AES-256).");

            _privateRoot = Path.Combine(env.ContentRootPath, sec.Value.PrivateUploadsFolder);
            Directory.CreateDirectory(_privateRoot);
            Directory.CreateDirectory(_env.WebRootPath ?? "wwwroot");

            var p = persist?.Value ?? new PersistenceOptions();
            _persistEnabled = p.Enabled;
            _persistEncrypt = p.EncryptState;
            _persistPath = Path.Combine(env.ContentRootPath, p.DataFile);
            Directory.CreateDirectory(Path.GetDirectoryName(_persistPath)!);
        }

        public async Task<UserAccount> GetOrCreateUserAsync(string username, string role)
        {
            username = username.Trim();
            var user = _usersByName.GetOrAdd(username, _ =>
            {
                var id = Interlocked.Increment(ref _userCounter);
                return new UserAccount { UserId = id, Username = username, Role = role, Password = "" };
            });
            user.Role = role;

            await SaveAsync();
            return user;
        }

        public async Task<Lecturer> GetOrCreateLecturerForUserAsync(int userId, string username, string email)
        {
            var lec = _lecturersByUserId.GetOrAdd(userId, _ =>
            {
                var id = Interlocked.Increment(ref _lecturerCounter);
                var created = new Lecturer { LecturerId = id, UserId = userId, Name = username, Email = email };
                _lecturersByLecturerId[id] = created;
                return created;
            });

            await SaveAsync();
            return lec;
        }

        public Task<Lecturer?> GetLecturerByIdAsync(int lecturerId)
        {
            _lecturersByLecturerId.TryGetValue(lecturerId, out var lec);
            return Task.FromResult(lec);
        }

        public async Task<int> CreateClaimAsync(int lecturerId, decimal hours, decimal rate, string notes)
        {
            var id = Interlocked.Increment(ref _claimCounter);
            var claim = new Claim
            {
                ClaimId = id,
                LecturerId = lecturerId,
                HoursWorked = Math.Round(hours, 2),
                HourlyRate = Math.Round(rate, 2),
                TotalAmount = Math.Round(hours * rate, 2),
                Status = "Pending",
                SubmittedAt = DateTime.UtcNow,
                Notes = notes ?? ""
            };
            _claims[id] = claim;

            await SaveAsync();
            return id;
        }

        public Task<List<Claim>> GetAllClaimsAsync()
        {
            var list = _claims.Values.OrderByDescending(c => c.SubmittedAt).ToList();
            return Task.FromResult(list);
        }

        public Task<List<Claim>> GetClaimsForLecturerAsync(int lecturerId)
        {
            var list = _claims.Values
                              .Where(c => c.LecturerId == lecturerId)
                              .OrderByDescending(c => c.SubmittedAt)
                              .ToList();
            return Task.FromResult(list);
        }

        public Task<Claim?> GetClaimAsync(int claimId)
        {
            _claims.TryGetValue(claimId, out var c);
            return Task.FromResult(c);
        }

        public async Task UpdateClaimStatusAsync(int claimId, string decision, string approverRole, string comments)
        {
            if (_claims.TryGetValue(claimId, out var claim))
            {
                claim.Status = decision;

                var apId = Interlocked.Increment(ref _approvalCounter);
                var approval = new Approval
                {
                    ApprovalId = apId,
                    ClaimId = claimId,
                    ApprovedBy = approverRole,
                    Decision = decision,
                    DecisionDate = DateTime.UtcNow,
                    Comments = comments ?? ""
                };

                var list = _approvalsByClaim.GetOrAdd(claimId, _ => new List<Approval>());
                lock (_lock) list.Add(approval);

                await SaveAsync();
            }
        }

        public Task<Approval?> GetLatestApprovalAsync(int claimId)
        {
            if (_approvalsByClaim.TryGetValue(claimId, out var list) && list.Count > 0)
            {
                var latest = list.OrderByDescending(a => a.DecisionDate).FirstOrDefault();
                return Task.FromResult(latest);
            }
            return Task.FromResult<Approval?>(null);
        }

        public async Task DeleteClaimAsync(int claimId)
        {
            _claims.TryRemove(claimId, out _);
            _approvalsByClaim.TryRemove(claimId, out _);

            if (_docIdsByClaim.TryRemove(claimId, out var docIds))
            {
                foreach (var id in docIds)
                {
                    if (_documents.TryRemove(id, out var d))
                    {
                        try { if (File.Exists(d.FilePath)) File.Delete(d.FilePath); } catch {}
                    }
                }
            }

            await SaveAsync();
        }

        private static string Sanitize(string name) =>
            Regex.Replace(name, @"[^a-zA-Z0-9\.\-_]+", "_");

        private (string dir, string encFile) BuildPrivatePath(int claimId, int docId, string safeName)
        {
            var dir = Path.Combine(_privateRoot, claimId.ToString());
            var enc = Path.Combine(dir, $"{docId}-{safeName}.enc");
            return (dir, enc);
        }

        public async Task<int> UploadDocumentAsync(int claimId, int lecturerId, string fileName, string contentType, Stream content)
        {
            var docId = Interlocked.Increment(ref _documentCounter);
            var safeName = Sanitize(fileName);

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            var ivB64 = Convert.ToBase64String(aes.IV);

            var (dir, encPath) = BuildPrivatePath(claimId, docId, safeName);
            Directory.CreateDirectory(dir);

            long encryptedBytes;
            using (var fs = File.Create(encPath))
            using (var crypto = new CryptoStream(fs, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                await content.CopyToAsync(crypto);
                await crypto.FlushAsync();
                encryptedBytes = fs.Length;
            }

            var doc = new SupportingDocument
            {
                DocumentId = docId,
                ClaimId = claimId,
                FileName = safeName,
                FileType = contentType,
                UploadedAt = DateTime.UtcNow,
                UploadedByLecturerId = lecturerId,
                FilePath = encPath,
                EncryptionIVBase64 = ivB64,
                SizeBytes = encryptedBytes
            };

            _documents[docId] = doc;
            var list = _docIdsByClaim.GetOrAdd(claimId, _ => new List<int>());
            lock (_lock) list.Add(docId);

            await SaveAsync();
            return docId;
        }

        public Task<List<SupportingDocument>> GetDocumentsForClaimAsync(int claimId)
        {
            if (_docIdsByClaim.TryGetValue(claimId, out var ids))
            {
                var docs = ids.Select(id => _documents[id]).OrderBy(d => d.DocumentId).ToList();
                return Task.FromResult(docs);
            }
            return Task.FromResult(new List<SupportingDocument>());
        }

        public Task<SupportingDocument?> GetDocumentAsync(int documentId)
        {
            _documents.TryGetValue(documentId, out var d);
            return Task.FromResult(d);
        }

        public async Task<byte[]> DecryptDocumentAsync(SupportingDocument doc)
        {
            if (!File.Exists(doc.FilePath))
                throw new FileNotFoundException("Encrypted file not found.", doc.FilePath);

            var iv = Convert.FromBase64String(doc.EncryptionIVBase64);

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var fs = File.OpenRead(doc.FilePath);
            using var crypto = new CryptoStream(fs, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var ms = new MemoryStream();
            await crypto.CopyToAsync(ms);
            return ms.ToArray();
        }

        public string GetPhysicalPathForDocument(SupportingDocument doc) => doc.FilePath;
        public async Task LoadAsync()
        {
            if (!_persistEnabled || !File.Exists(_persistPath)) return;

            byte[] bytes = await File.ReadAllBytesAsync(_persistPath);
            string json;

            if (_persistEncrypt)
            {
                if (bytes.Length < 16) return;
                using var aes = Aes.Create();
                aes.Key = _key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.IV = bytes.AsSpan(0, 16).ToArray();

                using var ms = new MemoryStream(bytes, 16, bytes.Length - 16);
                using var crypto = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using var outMs = new MemoryStream();
                await crypto.CopyToAsync(outMs);
                json = Encoding.UTF8.GetString(outMs.ToArray());
            }
            else
            {
                json = Encoding.UTF8.GetString(bytes);
            }

            Snapshot? snap;
            try
            {
                snap = JsonSerializer.Deserialize<Snapshot>(json);
            }
            catch
            {
                return;
            }
            if (snap == null) return;

            _userCounter = snap.UserCounter;
            _lecturerCounter = snap.LecturerCounter;
            _claimCounter = snap.ClaimCounter;
            _approvalCounter = snap.ApprovalCounter;
            _documentCounter = snap.DocumentCounter;

            _usersByName.Clear();
            foreach (var u in snap.Users) _usersByName[u.Username] = u;

            _lecturersByUserId.Clear();
            _lecturersByLecturerId.Clear();
            foreach (var l in snap.Lecturers)
            {
                _lecturersByLecturerId[l.LecturerId] = l;
                _lecturersByUserId[l.UserId] = l;
            }

            _claims.Clear();
            foreach (var c in snap.Claims) _claims[c.ClaimId] = c;

            _approvalsByClaim.Clear();
            foreach (var a in snap.Approvals)
            {
                var list = _approvalsByClaim.GetOrAdd(a.ClaimId, _ => new List<Approval>());
                list.Add(a);
            }

            _documents.Clear();
            foreach (var d in snap.Documents) _documents[d.DocumentId] = d;

            _docIdsByClaim.Clear();
            foreach (var link in snap.ClaimDocs)
            {
                var list = _docIdsByClaim.GetOrAdd(link.ClaimId, _ => new List<int>());
                list.Add(link.DocumentId);
            }
        }

        public Task SaveSnapshotAsync() => SaveAsync();

        private async Task SaveAsync()
        {
            if (!_persistEnabled) return;

            var snap = new Snapshot
            {
                UserCounter = _userCounter,
                LecturerCounter = _lecturerCounter,
                ClaimCounter = _claimCounter,
                ApprovalCounter = _approvalCounter,
                DocumentCounter = _documentCounter,
                Users = _usersByName.Values.ToList(),
                Lecturers = _lecturersByLecturerId.Values.ToList(),
                Claims = _claims.Values.ToList(),
                Documents = _documents.Values.ToList()
            };

            foreach (var kv in _approvalsByClaim)
                snap.Approvals.AddRange(kv.Value);

            foreach (var kv in _docIdsByClaim)
                foreach (var d in kv.Value)
                    snap.ClaimDocs.Add(new Snapshot.Link { ClaimId = kv.Key, DocumentId = d });

            var json = JsonSerializer.Serialize(snap, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            await _saveLock.WaitAsync();
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                if (_persistEncrypt)
                {
                    using var aes = Aes.Create();
                    aes.Key = _key;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.GenerateIV();

                    using var ms = new MemoryStream();
                    await ms.WriteAsync(aes.IV);
                    using (var crypto = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        await crypto.WriteAsync(bytes);
                    }
                    bytes = ms.ToArray();
                }

                await File.WriteAllBytesAsync(_persistPath, bytes);
            }
            finally
            {
                _saveLock.Release();
            }
        }
    }
}





