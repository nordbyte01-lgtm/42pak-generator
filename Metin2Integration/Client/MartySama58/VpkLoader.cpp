#include "StdAfx.h"
#include "VpkLoader.h"
#include "VpkCrypto.h"

#include <fstream>
#include <cstring>
#include <algorithm>

#include "../eterBase/CRC32.h"
#include "../eterBase/MappedFile.h"

CVpkPack::CVpkPack()
    : m_bKeysReady(false), m_pIndexData(nullptr)
{
    std::memset(&m_header, 0, sizeof(m_header));
    std::memset(m_aesKey, 0, sizeof(m_aesKey));
    std::memset(m_hmacKey, 0, sizeof(m_hmacKey));
}

CVpkPack::~CVpkPack()
{
    delete[] m_pIndexData;
    VpkCrypto::SecureZero(m_aesKey, sizeof(m_aesKey));
    VpkCrypto::SecureZero(m_hmacKey, sizeof(m_hmacKey));
}

bool CVpkPack::Create(CEterFileDict& rkFileDict,
                       const char* dbname,
                       const char* pathName,
                       bool /*bReadOnly*/)
{
    m_dbName = dbname;
    m_pathName = pathName;

    m_filePath = std::string(dbname) + ".vpk";

    if (!ReadHeader())
        return false;

    if (m_header.IsEncrypted)
    {
        if (m_passphrase.empty())
            return false;

        if (!DeriveKeys())
            return false;

        if (!VerifyHmac())
            return false;
    }

    if (!ReadEntryTable())
        return false;

    RegisterEntries(rkFileDict);
    return true;
}

void CVpkPack::SetPassphrase(const char* passphrase)
{
    m_passphrase = passphrase;
}

bool CVpkPack::ReadHeader()
{
    std::ifstream file(m_filePath, std::ios::binary);
    if (!file.is_open())
        return false;

    char magic[4];
    file.read(magic, 4);
    if (std::memcmp(magic, vpk::MAGIC, 4) != 0)
        return false;

    file.read(reinterpret_cast<char*>(&m_header.Version), 2);
    if (m_header.Version != vpk::FORMAT_VERSION)
        return false;

    file.read(reinterpret_cast<char*>(&m_header.EntryCount), 4);
    file.read(reinterpret_cast<char*>(&m_header.EntryTableOffset), 8);
    file.read(reinterpret_cast<char*>(&m_header.EntryTableSize), 4);

    char boolByte;
    file.read(&boolByte, 1);
    m_header.IsEncrypted = (boolByte != 0);

    file.read(reinterpret_cast<char*>(&m_header.CompressionLevel), 4);
    file.read(reinterpret_cast<char*>(&m_header.CompressionAlgorithm), 1);

    file.read(&boolByte, 1);
    m_header.FileNamesMangled = (boolByte != 0);

    file.read(reinterpret_cast<char*>(&m_header.CreatedAtUtcTicks), 8);
    file.read(reinterpret_cast<char*>(m_header.Salt), 32);
    file.read(m_header.Author, 64);
    file.read(m_header.Comment, 128);

    return file.good();
}

bool CVpkPack::DeriveKeys()
{
    unsigned char keyMaterial[64];
    bool ok = VpkCrypto::DeriveKey(
        m_passphrase.c_str(),
        m_header.Salt, vpk::SALT_LENGTH,
        keyMaterial, 64
    );

    if (!ok)
        return false;

    std::memcpy(m_aesKey, keyMaterial, 32);
    std::memcpy(m_hmacKey, keyMaterial + 32, 32);
    VpkCrypto::SecureZero(keyMaterial, sizeof(keyMaterial));
    m_bKeysReady = true;
    return true;
}

bool CVpkPack::VerifyHmac()
{
    if (!m_bKeysReady)
        return false;

    std::ifstream file(m_filePath, std::ios::binary | std::ios::ate);
    if (!file.is_open())
        return false;

    long long fileSize = file.tellg();
    long long contentSize = fileSize - vpk::HMAC_SIZE;

    unsigned char storedHmac[32];
    file.seekg(contentSize);
    file.read(reinterpret_cast<char*>(storedHmac), 32);

    file.seekg(0);
    unsigned char computedHmac[32];
    if (!VpkCrypto::ComputeHmacSha256(file, contentSize, m_hmacKey, 32, computedHmac))
        return false;

    return VpkCrypto::ConstantTimeEquals(storedHmac, computedHmac, 32);
}

bool CVpkPack::ReadEntryTable()
{
    std::ifstream file(m_filePath, std::ios::binary);
    if (!file.is_open())
        return false;

    file.seekg(m_header.EntryTableOffset);

    std::vector<unsigned char> tableData;

    if (m_header.IsEncrypted)
    {
        if (!m_bKeysReady)
            return false;

        unsigned char nonce[12], tag[16];
        file.read(reinterpret_cast<char*>(nonce), 12);
        file.read(reinterpret_cast<char*>(tag), 16);

        int encLen = m_header.EntryTableSize - vpk::NONCE_SIZE - vpk::TAG_SIZE;
        std::vector<unsigned char> encrypted(encLen);
        file.read(reinterpret_cast<char*>(encrypted.data()), encLen);

        tableData.resize(encLen);
        if (!VpkCrypto::AesGcmDecrypt(
                encrypted.data(), encLen,
                m_aesKey, nonce, tag,
                tableData.data()))
            return false;
    }
    else
    {
        tableData.resize(m_header.EntryTableSize);
        file.read(reinterpret_cast<char*>(tableData.data()), m_header.EntryTableSize);
    }

    const unsigned char* ptr = tableData.data();
    const unsigned char* end = ptr + tableData.size();

    m_entries.reserve(m_header.EntryCount);

    for (int i = 0; i < m_header.EntryCount && ptr < end; ++i)
    {
        TVpkEntry entry;

        int storedNameLen = *reinterpret_cast<const int*>(ptr); ptr += 4;
        entry.StoredName.assign(reinterpret_cast<const char*>(ptr), storedNameLen); ptr += storedNameLen;

        int fileNameLen = *reinterpret_cast<const int*>(ptr); ptr += 4;
        entry.FileName.assign(reinterpret_cast<const char*>(ptr), fileNameLen); ptr += fileNameLen;

        entry.OriginalSize = *reinterpret_cast<const long long*>(ptr); ptr += 8;
        entry.StoredSize   = *reinterpret_cast<const long long*>(ptr); ptr += 8;
        entry.DataOffset   = *reinterpret_cast<const long long*>(ptr); ptr += 8;

        int hashLen = *reinterpret_cast<const int*>(ptr); ptr += 4;
        entry.ContentHash.assign(ptr, ptr + hashLen); ptr += hashLen;

        entry.IsCompressed = (*ptr != 0); ptr += 1;
        entry.IsEncrypted  = (*ptr != 0); ptr += 1;

        int nonceLen = *reinterpret_cast<const int*>(ptr); ptr += 4;
        if (nonceLen > 0) { entry.Nonce.assign(ptr, ptr + nonceLen); ptr += nonceLen; }

        int tagLen = *reinterpret_cast<const int*>(ptr); ptr += 4;
        if (tagLen > 0) { entry.AuthTag.assign(ptr, ptr + tagLen); ptr += tagLen; }

        m_entries.push_back(std::move(entry));
    }

    // Build CRC32 lookup map (same hashing as CEterFileDict / CEterPackManager)
    for (int i = 0; i < static_cast<int>(m_entries.size()); ++i)
    {
        std::string lower = m_entries[i].FileName;
        std::transform(lower.begin(), lower.end(), lower.begin(), ::tolower);
        std::replace(lower.begin(), lower.end(), '\\', '/');

        DWORD crc = GetCRC32(lower.c_str(), lower.size());
        m_entryMap[crc] = i;
    }

    return true;
}

//
// RegisterEntries: Populate the shared CEterFileDict so that
// CEterPackManager::GetFromPack can resolve filenames to this pack via
// the global hash lookup - exactly like CEterPack::__BuildIndex does.
//
// We create stub TEterPackIndex records that the dict stores alongside
// regular EPK entries.  Get2() knows to handle them via the VPK path.
//
// MartySama 5.8: CEterFileDict uses boost::unordered_multimap<DWORD, Item>.
// InsertItem() works identically to the 40250 version.
//
void CVpkPack::RegisterEntries(CEterFileDict& rkFileDict)
{
    if (m_entries.empty())
        return;

    m_pIndexData = new TEterPackIndex[m_entries.size()];
    std::memset(m_pIndexData, 0, sizeof(TEterPackIndex) * m_entries.size());

    for (int i = 0; i < static_cast<int>(m_entries.size()); ++i)
    {
        TEterPackIndex& idx = m_pIndexData[i];

        // Normalize filename: lowercase, forward slashes
        std::string lower = m_entries[i].FileName;
        std::transform(lower.begin(), lower.end(), lower.begin(), ::tolower);
        std::replace(lower.begin(), lower.end(), '\\', '/');

        // Truncate to FILENAME_MAX_LEN to fit the fixed-size field
        size_t copyLen = (lower.size() < FILENAME_MAX_LEN) ? lower.size() : FILENAME_MAX_LEN;
        std::memcpy(idx.filename, lower.c_str(), copyLen);
        idx.filename[copyLen] = '\0';

        idx.filename_crc = GetCRC32(lower.c_str(), lower.size());
        idx.id = i;
        idx.real_data_size = static_cast<long>(m_entries[i].OriginalSize);
        idx.data_size = static_cast<long>(m_entries[i].StoredSize);
        idx.data_position = 0;        // not used - VPK uses its own offset
        idx.data_crc = 0;             // not used - VPK uses BLAKE3
        idx.compressed_type = -1;     // sentinel: signals Get2 that this is a VPK entry

        // Insert into the shared file dict (boost::unordered_multimap).
        // The dict stores a pointer to this CVpkPack (cast to CEterPack*)
        // and the stub index.  When GetFromPack calls pkPack->Get2(),
        // it dispatches to our Get2.
        rkFileDict.InsertItem(reinterpret_cast<CEterPack*>(this), &idx);
    }
}

//
// Get2: Called by CEterPackManager::GetFromPack via the CEterFileDict lookup.
// Matches CEterPack::Get2 signature exactly.
//
// The index parameter is a stub with id == entry index into m_entries.
// We decrypt + decompress the data from the .vpk file, then hand ownership
// to CMappedFile via AppendDataBlock so it manages the lifetime.
//
bool CVpkPack::Get2(CMappedFile& mappedFile, const char* /*filename*/,
                     TEterPackIndex* index, LPCVOID* data)
{
    if (!index || index->id < 0 || index->id >= static_cast<long>(m_entries.size()))
        return false;

    const TVpkEntry& entry = m_entries[index->id];

    unsigned char* outData = nullptr;
    int outSize = 0;

    if (!DecryptAndDecompress(entry, &outData, &outSize))
        return false;

    // Hand the buffer to CMappedFile via AppendDataBlock. This copies the
    // data into a CMappedFile-owned allocation and returns a pointer to it.
    // CMappedFile::~CMappedFile frees the block automatically.
    *data = mappedFile.AppendDataBlock(outData, static_cast<DWORD>(outSize));

    delete[] outData;
    return true;
}

//
// Get: Convenience wrapper for direct use (not through CEterFileDict).
// Also uses AppendDataBlock for proper memory management.
//
bool CVpkPack::Get(CMappedFile& mappedFile, const char* filename, LPCVOID* data)
{
    std::string normalized(filename);
    std::transform(normalized.begin(), normalized.end(), normalized.begin(), ::tolower);
    std::replace(normalized.begin(), normalized.end(), '\\', '/');

    DWORD crc = GetCRC32(normalized.c_str(), normalized.size());
    auto it = m_entryMap.find(crc);
    if (it == m_entryMap.end())
        return false;

    const TVpkEntry& entry = m_entries[it->second];

    unsigned char* outData = nullptr;
    int outSize = 0;

    if (!DecryptAndDecompress(entry, &outData, &outSize))
        return false;

    *data = mappedFile.AppendDataBlock(outData, static_cast<DWORD>(outSize));

    delete[] outData;
    return true;
}

//
// DecryptAndDecompress: Core data extraction logic.
// Supports LZ4 (1), Zstandard (2), and Brotli (3) decompression.
// Verifies BLAKE3 content hash after decompression.
// Caller must delete[] the returned outData.
//
bool CVpkPack::DecryptAndDecompress(const TVpkEntry& entry,
                                     unsigned char** outData,
                                     int* outSize)
{
    std::ifstream file(m_filePath, std::ios::binary);
    if (!file.is_open())
        return false;

    file.seekg(entry.DataOffset);

    std::vector<unsigned char> storedData(static_cast<size_t>(entry.StoredSize));
    file.read(reinterpret_cast<char*>(storedData.data()), entry.StoredSize);

    unsigned char* current = storedData.data();
    int currentSize = static_cast<int>(entry.StoredSize);

    // Step 1: Decrypt if encrypted
    std::vector<unsigned char> decrypted;
    if (entry.IsEncrypted)
    {
        if (!m_bKeysReady)
            return false;

        decrypted.resize(currentSize);
        if (!VpkCrypto::AesGcmDecrypt(
                current, currentSize,
                m_aesKey,
                entry.Nonce.data(),
                entry.AuthTag.data(),
                decrypted.data()))
            return false;

        current = decrypted.data();
    }

    // Step 2: Decompress if compressed
    std::vector<unsigned char> decompressed;
    if (entry.IsCompressed)
    {
        int originalSize = *reinterpret_cast<const int*>(current);
        if (originalSize != static_cast<int>(entry.OriginalSize))
            return false;

        decompressed.resize(originalSize);

        const unsigned char* compData = current + 4;
        int compSize = currentSize - 4;

        int result = -1;

        switch (m_header.CompressionAlgorithm)
        {
        case vpk::COMPRESS_LZ4:
            result = VpkCrypto::Lz4Decompress(compData, compSize,
                                               decompressed.data(), originalSize);
            break;

        case vpk::COMPRESS_ZSTD:
            result = VpkCrypto::ZstdDecompress(compData, compSize,
                                                decompressed.data(), originalSize);
            break;

        case vpk::COMPRESS_BROTLI:
            result = VpkCrypto::BrotliDecompress(compData, compSize,
                                                  decompressed.data(), originalSize);
            break;

        default:
            return false;
        }

        if (result <= 0)
            return false;

        current = decompressed.data();
        currentSize = originalSize;
    }

    // Step 3: Verify BLAKE3 content hash
    unsigned char hash[32];
    VpkCrypto::Blake3Hash(current, currentSize, hash);

    if (!VpkCrypto::ConstantTimeEquals(hash, entry.ContentHash.data(), 32))
        return false;

    // Step 4: Return a copy to the caller
    *outData = new unsigned char[currentSize];
    std::memcpy(*outData, current, currentSize);
    *outSize = currentSize;
    return true;
}

bool CVpkPack::IsExist(const char* filename) const
{
    std::string normalized(filename);
    std::transform(normalized.begin(), normalized.end(), normalized.begin(), ::tolower);
    std::replace(normalized.begin(), normalized.end(), '\\', '/');

    DWORD crc = GetCRC32(normalized.c_str(), normalized.size());
    return m_entryMap.find(crc) != m_entryMap.end();
}

bool CVpkPack::GetNames(std::vector<std::string>* retNames) const
{
    if (!retNames) return false;
    retNames->reserve(m_entries.size());
    for (const auto& entry : m_entries)
        retNames->push_back(entry.FileName);
    return true;
}

const char* CVpkPack::GetDBName() const
{
    return m_dbName.c_str();
}

const std::string& CVpkPack::GetPathName() const
{
    return m_pathName;
}

const TVpkHeader& CVpkPack::GetHeader() const
{
    return m_header;
}
