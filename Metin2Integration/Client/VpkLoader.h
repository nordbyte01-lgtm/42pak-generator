#pragma once

#include <string>
#include <vector>
#include <unordered_map>

class CEterFileDict;
class CMappedFile;

namespace vpk
{
    static const char MAGIC[4] = { '4', '2', 'P', 'K' };
    static const unsigned short FORMAT_VERSION = 2;
    static const int HEADER_SIZE = 512;
    static const int DATA_BLOCK_ALIGNMENT = 4096;
    static const int MAX_FILENAME_LEN = 512;
    static const int HMAC_SIZE = 32;
    static const int PBKDF2_ITERATIONS = 200000;
    static const int SALT_LENGTH = 32;
    static const int NONCE_SIZE = 12;   // AES-GCM nonce
    static const int TAG_SIZE = 16;     // AES-GCM auth tag
    static const int KEY_SIZE = 32;     // AES-256 key
    static const int BLAKE3_HASH_SIZE = 32;
}

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
    std::vector<unsigned char> ContentHash;   // BLAKE3, 32 bytes
    bool          IsCompressed;
    bool          IsEncrypted;
    std::vector<unsigned char> Nonce;         // 12 bytes if encrypted
    std::vector<unsigned char> AuthTag;       // 16 bytes if encrypted
};

class CVpkPack
{
public:
    CVpkPack();
    virtual ~CVpkPack();

    bool Create(CEterFileDict& rkFileDict,
                const char* dbname,
                const char* pathName,
                bool bReadOnly = true);

    void SetPassphrase(const char* passphrase);

    bool Get(CMappedFile& mappedFile, const char* filename, const void** data);

    bool IsExist(const char* filename) const;

    bool GetNames(std::vector<std::string>* retNames) const;

    const char* GetDBName() const;
    const std::string& GetPathName() const;
    const TVpkHeader& GetHeader() const;

    bool ReadEntry(const TVpkEntry& entry, unsigned char** outData, int* outSize);

private:
    bool ReadHeader();
    bool VerifyHmac();
    bool ReadEntryTable();
    bool DeriveKeys();
    void RegisterEntries(CEterFileDict& rkFileDict);

    std::string m_dbName;
    std::string m_pathName;
    std::string m_filePath;     // full path to the .vpk file

    TVpkHeader  m_header;
    std::vector<TVpkEntry> m_entries;

    std::unordered_map<unsigned int, int> m_entryMap;

    unsigned char m_aesKey[32];
    unsigned char m_hmacKey[32];
    bool m_bKeysReady;

    std::string m_passphrase;
};
