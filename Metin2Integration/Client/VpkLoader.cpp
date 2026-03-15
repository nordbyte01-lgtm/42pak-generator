#include "StdAfx.h"
#include "VpkLoader.h"
#include "VpkCrypto.h"

#include <fstream>
#include <cstring>
#include <algorithm>

#include "../EterPack/EterFileDict.h"
#include "../EterBase/MappedFile.h"
#include "../EterBase/CRC32.h"

CVpkPack::CVpkPack()
    : m_bKeysReady(false)
{
    std::memset(&m_header, 0, sizeof(m_header));
    std::memset(m_aesKey, 0, sizeof(m_aesKey));
    std::memset(m_hmacKey, 0, sizeof(m_hmacKey));
}

CVpkPack::~CVpkPack()
{
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
        {
            return false;
        }

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

    for (int i = 0; i < static_cast<int>(m_entries.size()); ++i)
    {
        std::string lower = m_entries[i].FileName;
        std::transform(lower.begin(), lower.end(), lower.begin(), ::tolower);

        unsigned int crc = GetCRC32(lower.c_str(), lower.size());
        m_entryMap[crc] = i;
    }

    return true;
}

void CVpkPack::RegisterEntries(CEterFileDict& rkFileDict)
{
}

bool CVpkPack::Get(CMappedFile& /*mappedFile*/, const char* filename, const void** data)
{
    std::string normalized(filename);
    std::transform(normalized.begin(), normalized.end(), normalized.begin(), ::tolower);
    std::replace(normalized.begin(), normalized.end(), '\\', '/');

    unsigned int crc = GetCRC32(normalized.c_str(), normalized.size());
    auto it = m_entryMap.find(crc);
    if (it == m_entryMap.end())
        return false;

    const TVpkEntry& entry = m_entries[it->second];

    unsigned char* outData = nullptr;
    int outSize = 0;
    if (!ReadEntry(entry, &outData, &outSize))
        return false;

    *data = outData;
    return true;
}

bool CVpkPack::ReadEntry(const TVpkEntry& entry,
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

    std::vector<unsigned char> decompressed;
    if (entry.IsCompressed)
    {
        if (m_header.CompressionAlgorithm != 1) // only LZ4 supported in C++ loader
            return false;

        int originalSize = *reinterpret_cast<const int*>(current);
        if (originalSize != static_cast<int>(entry.OriginalSize))
            return false;

        decompressed.resize(originalSize);
        int result = VpkCrypto::Lz4Decompress(
            current + 4, currentSize - 4,
            decompressed.data(), originalSize);

        if (result <= 0)
            return false;

        current = decompressed.data();
        currentSize = originalSize;
    }

    unsigned char hash[32];
    VpkCrypto::Blake3Hash(current, currentSize, hash);

    if (!VpkCrypto::ConstantTimeEquals(hash, entry.ContentHash.data(), 32))
        return false;

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

    unsigned int crc = GetCRC32(normalized.c_str(), normalized.size());
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
