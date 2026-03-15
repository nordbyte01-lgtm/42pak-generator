#pragma once

#include <string>
#include <vector>
#include <unordered_map>
#include <functional>

namespace vpk
{
    static const char MAGIC[4] = { '4', '2', 'P', 'K' };
    static const unsigned short FORMAT_VERSION = 2;
    static const int HEADER_SIZE = 512;
    static const int HMAC_SIZE = 32;
    static const int SALT_LENGTH = 32;
    static const int NONCE_SIZE = 12;
    static const int TAG_SIZE = 16;
    static const int KEY_SIZE = 32;
}

struct TVpkHeader;
struct TVpkEntry;

struct VpkValidationResult
{
    bool     IsValid;
    int      TotalEntries;
    int      ValidEntries;
    std::vector<std::string> Errors;
};

class CVpkHandler
{
public:
    CVpkHandler();
    ~CVpkHandler();

    bool Open(const char* filePath, const char* passphrase = nullptr);

    void Close();

    bool FileExists(const char* fileName) const;

    bool ReadFile(const char* fileName, std::vector<unsigned char>& outData);

    bool ReadFileAsString(const char* fileName, std::string& outStr);

    void GetFileList(std::vector<std::string>& outNames) const;

    int GetEntryCount() const;

    VpkValidationResult Validate();

    void ForEachEntry(std::function<void(const char*, long long, bool, bool)> callback) const;

    bool IsOpen() const { return m_bOpen; }
    const char* GetFilePath() const { return m_filePath.c_str(); }

private:
    bool ReadHeader();
    bool VerifyHmac();
    bool ReadEntryTable();
    bool DeriveKeys(const char* passphrase);
    bool DecryptAndDecompress(const TVpkEntry& entry,
                               std::vector<unsigned char>& outData);

    std::string m_filePath;
    bool m_bOpen;

    TVpkHeader* m_pHeader;
    std::vector<TVpkEntry*> m_entries;

    std::unordered_map<std::string, int> m_nameMap;

    // Derived key material
    unsigned char m_aesKey[32];
    unsigned char m_hmacKey[32];
    bool m_bKeysReady;
};
