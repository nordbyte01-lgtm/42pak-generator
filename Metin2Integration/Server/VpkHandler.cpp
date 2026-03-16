#include "VpkHandler.h"

#include <fstream>
#include <cstring>
#include <algorithm>

#include <openssl/evp.h>
#include <openssl/hmac.h>

#include "blake3.h"

#include <lz4.h>
#include <zstd.h>
#include <brotli/decode.h>

struct TVpkHeader
{
    unsigned short Version;
    int            EntryCount;
    long long      EntryTableOffset;
    int            EntryTableSize;
    bool           IsEncrypted;
    int            CompressionLevel;
    unsigned char  CompressionAlgorithm;
    bool           FileNamesMangled;
    long long      CreatedAtUtcTicks;
    unsigned char  Salt[32];
    char           Author[64];
    char           Comment[128];
};

struct TVpkEntry
{
    std::string   StoredName;
    std::string   FileName;
    long long     OriginalSize;
    long long     StoredSize;
    long long     DataOffset;
    std::vector<unsigned char> ContentHash;
    bool          IsCompressed;
    bool          IsEncrypted;
    std::vector<unsigned char> Nonce;
    std::vector<unsigned char> AuthTag;
};

namespace
{
    bool AesGcmDecrypt(const unsigned char* ct, int ctLen,
                        const unsigned char* key,
                        const unsigned char* nonce,
                        const unsigned char* tag,
                        unsigned char* pt)
    {
        EVP_CIPHER_CTX* ctx = EVP_CIPHER_CTX_new();
        if (!ctx) return false;

        bool ok = false;
        do
        {
            if (EVP_DecryptInit_ex(ctx, EVP_aes_256_gcm(), nullptr, nullptr, nullptr) != 1) break;
            if (EVP_CIPHER_CTX_ctrl(ctx, EVP_CTRL_GCM_SET_IVLEN, 12, nullptr) != 1) break;
            if (EVP_DecryptInit_ex(ctx, nullptr, nullptr, key, nonce) != 1) break;

            int outLen = 0;
            if (EVP_DecryptUpdate(ctx, pt, &outLen, ct, ctLen) != 1) break;
            if (EVP_CIPHER_CTX_ctrl(ctx, EVP_CTRL_GCM_SET_TAG, 16,
                                     const_cast<unsigned char*>(tag)) != 1) break;

            int finalLen = 0;
            if (EVP_DecryptFinal_ex(ctx, pt + outLen, &finalLen) != 1) break;
            ok = true;
        } while (false);

        EVP_CIPHER_CTX_free(ctx);
        return ok;
    }

    bool Pbkdf2Sha512(const char* pass, int passLen,
                       const unsigned char* salt, int saltLen,
                       unsigned char* out, int outLen)
    {
        return PKCS5_PBKDF2_HMAC(pass, passLen, salt, saltLen,
                                  200000, EVP_sha512(), outLen, out) == 1;
    }

    bool HmacSha256Stream(std::ifstream& stream, long long length,
                           const unsigned char* key, int keyLen,
                           unsigned char* outHmac)
    {
        HMAC_CTX* ctx = HMAC_CTX_new();
        if (!ctx) return false;

        HMAC_Init_ex(ctx, key, keyLen, EVP_sha256(), nullptr);

        unsigned char buf[65536];
        long long remaining = length;
        while (remaining > 0)
        {
            int toRead = (remaining > 65536) ? 65536 : static_cast<int>(remaining);
            stream.read(reinterpret_cast<char*>(buf), toRead);
            int got = static_cast<int>(stream.gcount());
            if (got <= 0) break;
            HMAC_Update(ctx, buf, got);
            remaining -= got;
        }

        unsigned int hmacLen = 32;
        HMAC_Final(ctx, outHmac, &hmacLen);
        HMAC_CTX_free(ctx);
        return (remaining == 0);
    }

    void Blake3(const unsigned char* data, int len, unsigned char* out)
    {
        blake3_hasher h;
        blake3_hasher_init(&h);
        blake3_hasher_update(&h, data, len);
        blake3_hasher_finalize(&h, out, BLAKE3_OUT_LEN);
    }

    bool ConstTimeEq(const unsigned char* a, const unsigned char* b, int n)
    {
        int d = 0;
        for (int i = 0; i < n; i++) d |= a[i] ^ b[i];
        return d == 0;
    }

    void SecureZero(void* p, size_t n)
    {
#ifdef _WIN32
        SecureZeroMemory(p, n);
#else
        volatile unsigned char* v = static_cast<volatile unsigned char*>(p);
        while (n--) *v++ = 0;
#endif
    }
}

CVpkHandler::CVpkHandler()
    : m_bOpen(false), m_pHeader(nullptr), m_bKeysReady(false)
{
    std::memset(m_aesKey, 0, sizeof(m_aesKey));
    std::memset(m_hmacKey, 0, sizeof(m_hmacKey));
}

CVpkHandler::~CVpkHandler()
{
    Close();
}

void CVpkHandler::Close()
{
    for (auto* e : m_entries) delete e;
    m_entries.clear();
    m_nameMap.clear();
    delete m_pHeader;
    m_pHeader = nullptr;
    m_bOpen = false;
    SecureZero(m_aesKey, sizeof(m_aesKey));
    SecureZero(m_hmacKey, sizeof(m_hmacKey));
    m_bKeysReady = false;
}

bool CVpkHandler::Open(const char* filePath, const char* passphrase)
{
    Close();
    m_filePath = filePath;

    m_pHeader = new TVpkHeader;
    std::memset(m_pHeader, 0, sizeof(TVpkHeader));

    if (!ReadHeader())
        return false;

    if (m_pHeader->IsEncrypted)
    {
        if (!passphrase || !passphrase[0])
            return false;

        if (!DeriveKeys(passphrase))
            return false;

        if (!VerifyHmac())
            return false;
    }

    if (!ReadEntryTable())
        return false;

    m_bOpen = true;
    return true;
}

bool CVpkHandler::ReadHeader()
{
    std::ifstream f(m_filePath, std::ios::binary);
    if (!f.is_open()) return false;

    char magic[4];
    f.read(magic, 4);
    if (std::memcmp(magic, vpk::MAGIC, 4) != 0) return false;

    f.read(reinterpret_cast<char*>(&m_pHeader->Version), 2);
    if (m_pHeader->Version != vpk::FORMAT_VERSION) return false;

    f.read(reinterpret_cast<char*>(&m_pHeader->EntryCount), 4);
    f.read(reinterpret_cast<char*>(&m_pHeader->EntryTableOffset), 8);
    f.read(reinterpret_cast<char*>(&m_pHeader->EntryTableSize), 4);

    char b;
    f.read(&b, 1); m_pHeader->IsEncrypted = (b != 0);
    f.read(reinterpret_cast<char*>(&m_pHeader->CompressionLevel), 4);
    f.read(reinterpret_cast<char*>(&m_pHeader->CompressionAlgorithm), 1);
    f.read(&b, 1); m_pHeader->FileNamesMangled = (b != 0);
    f.read(reinterpret_cast<char*>(&m_pHeader->CreatedAtUtcTicks), 8);
    f.read(reinterpret_cast<char*>(m_pHeader->Salt), 32);
    f.read(m_pHeader->Author, 64);
    f.read(m_pHeader->Comment, 128);

    return f.good();
}

bool CVpkHandler::DeriveKeys(const char* passphrase)
{
    std::string fullPass = std::string("42PK-v1:") + passphrase;

    unsigned char keyMaterial[64];
    bool ok = Pbkdf2Sha512(fullPass.c_str(), static_cast<int>(fullPass.size()),
                            m_pHeader->Salt, vpk::SALT_LENGTH, keyMaterial, 64);

    SecureZero(&fullPass[0], fullPass.size());

    if (!ok) return false;

    std::memcpy(m_aesKey, keyMaterial, 32);
    std::memcpy(m_hmacKey, keyMaterial + 32, 32);
    SecureZero(keyMaterial, sizeof(keyMaterial));
    m_bKeysReady = true;
    return true;
}

bool CVpkHandler::VerifyHmac()
{
    if (!m_bKeysReady) return false;

    std::ifstream f(m_filePath, std::ios::binary | std::ios::ate);
    if (!f.is_open()) return false;

    long long fileSize = f.tellg();
    long long contentSize = fileSize - vpk::HMAC_SIZE;

    unsigned char stored[32];
    f.seekg(contentSize);
    f.read(reinterpret_cast<char*>(stored), 32);

    f.seekg(0);
    unsigned char computed[32];
    if (!HmacSha256Stream(f, contentSize, m_hmacKey, 32, computed))
        return false;

    return ConstTimeEq(stored, computed, 32);
}

bool CVpkHandler::ReadEntryTable()
{
    std::ifstream f(m_filePath, std::ios::binary);
    if (!f.is_open()) return false;

    f.seekg(m_pHeader->EntryTableOffset);

    std::vector<unsigned char> tableData;

    if (m_pHeader->IsEncrypted)
    {
        if (!m_bKeysReady) return false;

        unsigned char nonce[12], tag[16];
        f.read(reinterpret_cast<char*>(nonce), 12);
        f.read(reinterpret_cast<char*>(tag), 16);

        int encLen = m_pHeader->EntryTableSize - 12 - 16;
        std::vector<unsigned char> encrypted(encLen);
        f.read(reinterpret_cast<char*>(encrypted.data()), encLen);

        tableData.resize(encLen);
        if (!AesGcmDecrypt(encrypted.data(), encLen,
                            m_aesKey, nonce, tag, tableData.data()))
            return false;
    }
    else
    {
        tableData.resize(m_pHeader->EntryTableSize);
        f.read(reinterpret_cast<char*>(tableData.data()), m_pHeader->EntryTableSize);
    }

    const unsigned char* ptr = tableData.data();
    const unsigned char* end = ptr + tableData.size();

    for (int i = 0; i < m_pHeader->EntryCount && ptr < end; ++i)
    {
        TVpkEntry* entry = new TVpkEntry;

        int sLen = *reinterpret_cast<const int*>(ptr); ptr += 4;
        entry->StoredName.assign(reinterpret_cast<const char*>(ptr), sLen); ptr += sLen;

        int fLen = *reinterpret_cast<const int*>(ptr); ptr += 4;
        entry->FileName.assign(reinterpret_cast<const char*>(ptr), fLen); ptr += fLen;

        entry->OriginalSize = *reinterpret_cast<const long long*>(ptr); ptr += 8;
        entry->StoredSize   = *reinterpret_cast<const long long*>(ptr); ptr += 8;
        entry->DataOffset   = *reinterpret_cast<const long long*>(ptr); ptr += 8;

        int hLen = *reinterpret_cast<const int*>(ptr); ptr += 4;
        entry->ContentHash.assign(ptr, ptr + hLen); ptr += hLen;

        entry->IsCompressed = (*ptr != 0); ptr++;
        entry->IsEncrypted  = (*ptr != 0); ptr++;

        int nLen = *reinterpret_cast<const int*>(ptr); ptr += 4;
        if (nLen > 0) { entry->Nonce.assign(ptr, ptr + nLen); ptr += nLen; }

        int tLen = *reinterpret_cast<const int*>(ptr); ptr += 4;
        if (tLen > 0) { entry->AuthTag.assign(ptr, ptr + tLen); ptr += tLen; }

        m_entries.push_back(entry);

        std::string lower = entry->FileName;
        std::transform(lower.begin(), lower.end(), lower.begin(), ::tolower);
        m_nameMap[lower] = i;
    }

    return true;
}

bool CVpkHandler::DecryptAndDecompress(const TVpkEntry& entry,
                                        std::vector<unsigned char>& outData)
{
    std::ifstream f(m_filePath, std::ios::binary);
    if (!f.is_open()) return false;

    f.seekg(entry.DataOffset);

    std::vector<unsigned char> stored(static_cast<size_t>(entry.StoredSize));
    f.read(reinterpret_cast<char*>(stored.data()), entry.StoredSize);

    unsigned char* current = stored.data();
    int currentSize = static_cast<int>(entry.StoredSize);

    std::vector<unsigned char> decrypted;
    if (entry.IsEncrypted)
    {
        if (!m_bKeysReady) return false;

        decrypted.resize(currentSize);
        if (!AesGcmDecrypt(current, currentSize,
                            m_aesKey, entry.Nonce.data(), entry.AuthTag.data(),
                            decrypted.data()))
            return false;

        current = decrypted.data();
    }

    if (entry.IsCompressed)
    {
        int origSize = *reinterpret_cast<const int*>(current);
        if (origSize != static_cast<int>(entry.OriginalSize))
            return false;

        outData.resize(origSize);

        const unsigned char* compData = current + 4;
        int compSize = currentSize - 4;
        int result = -1;

        switch (m_pHeader->CompressionAlgorithm)
        {
        case 1: // LZ4
            result = LZ4_decompress_safe(
                reinterpret_cast<const char*>(compData),
                reinterpret_cast<char*>(outData.data()),
                compSize, origSize);
            break;

        case 2: // Zstandard
        {
            size_t zr = ZSTD_decompress(outData.data(), static_cast<size_t>(origSize),
                                         compData, static_cast<size_t>(compSize));
            result = ZSTD_isError(zr) ? -1 : static_cast<int>(zr);
            break;
        }

        case 3: // Brotli
        {
            size_t outSz = static_cast<size_t>(origSize);
            BrotliDecoderResult br = BrotliDecoderDecompress(
                static_cast<size_t>(compSize), compData, &outSz, outData.data());
            result = (br == BROTLI_DECODER_RESULT_SUCCESS) ? static_cast<int>(outSz) : -1;
            break;
        }

        default:
            return false;
        }

        if (result <= 0) return false;
        current = outData.data();
        currentSize = origSize;
    }
    else
    {
        outData.assign(current, current + currentSize);
    }

    unsigned char hash[32];
    Blake3(outData.data(), static_cast<int>(outData.size()), hash);

    if (!ConstTimeEq(hash, entry.ContentHash.data(), 32))
        return false;

    return true;
}

bool CVpkHandler::FileExists(const char* fileName) const
{
    std::string lower(fileName);
    std::transform(lower.begin(), lower.end(), lower.begin(), ::tolower);
    std::replace(lower.begin(), lower.end(), '\\', '/');
    return m_nameMap.find(lower) != m_nameMap.end();
}

bool CVpkHandler::ReadFile(const char* fileName, std::vector<unsigned char>& outData)
{
    if (!m_bOpen) return false;

    std::string lower(fileName);
    std::transform(lower.begin(), lower.end(), lower.begin(), ::tolower);
    std::replace(lower.begin(), lower.end(), '\\', '/');

    auto it = m_nameMap.find(lower);
    if (it == m_nameMap.end()) return false;

    return DecryptAndDecompress(*m_entries[it->second], outData);
}

bool CVpkHandler::ReadFileAsString(const char* fileName, std::string& outStr)
{
    std::vector<unsigned char> data;
    if (!ReadFile(fileName, data)) return false;
    outStr.assign(reinterpret_cast<const char*>(data.data()), data.size());
    return true;
}

void CVpkHandler::GetFileList(std::vector<std::string>& outNames) const
{
    outNames.reserve(m_entries.size());
    for (const auto* e : m_entries)
        outNames.push_back(e->FileName);
}

int CVpkHandler::GetEntryCount() const
{
    return static_cast<int>(m_entries.size());
}

VpkValidationResult CVpkHandler::Validate()
{
    VpkValidationResult result;
    result.IsValid = false;
    result.TotalEntries = static_cast<int>(m_entries.size());
    result.ValidEntries = 0;

    if (!m_bOpen)
    {
        result.Errors.push_back("Archive is not open.");
        return result;
    }

    for (const auto* entry : m_entries)
    {
        std::vector<unsigned char> data;
        if (DecryptAndDecompress(*entry, data))
        {
            result.ValidEntries++;
        }
        else
        {
            result.Errors.push_back("Failed: " + entry->FileName);
        }
    }

    result.IsValid = result.Errors.empty();
    return result;
}

void CVpkHandler::ForEachEntry(
    std::function<void(const char*, long long, bool, bool)> callback) const
{
    for (const auto* e : m_entries)
        callback(e->FileName.c_str(), e->OriginalSize,
                 e->IsCompressed, e->IsEncrypted);
}
