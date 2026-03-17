#pragma once

//
// EterPackManager_Vpk.h - MartySama 5.8 Profile
//
// Drop-in replacement for EterPackManager.h that adds native VPK support
// to the MartySama 5.8 architecture.
//
// Key differences from the 40250 profile:
//   - Uses boost::unordered_map (MartySama uses Boost throughout)
//   - Uses #pragma once (MartySama header style)
//   - Includes HybridCrypt methods (same as MartySama's original)
//   - Include paths match MartySama's directory layout (../eterBase/)
//
// Key differences from the FliegeV3 profile:
//   - Has HybridCrypt support (WriteHybridCryptPackInfo, RetrieveHybridCryptPackKeys,
//     RetrieveHybridCryptPackSDB)
//   - Has DecryptPackIV
//   - Uses boost::unordered_map (not std::unordered_map)
//
// Usage: Replace #include "EterPackManager.h" with this file, or merge
// the additions into your existing EterPackManager.h.
//
// Reference: Original MartySama 5.8 EterPackManager.h (93 lines)
//

#include <windows.h>
#include <boost/unordered_map.hpp>
#include "../eterBase/Singleton.h"
#include "../eterBase/Stl.h"

#include "EterPack.h"
#include "VpkLoader.h"      // <-- NEW: CVpkPack

class CEterPackManager : public CSingleton<CEterPackManager>
{
public:
    struct SCache
    {
        BYTE* m_abBufData;
        DWORD m_dwBufSize;
    };

public:
    enum ESearchModes
    {
        SEARCH_FILE_FIRST,
        SEARCH_PACK_FIRST
    };

    typedef std::list<CEterPack*> TEterPackList;
    typedef boost::unordered_map<std::string, CEterPack*, stringhash> TEterPackMap;

    // NEW: VPK pack storage (uses boost::unordered_map to match MartySama conventions)
    typedef std::list<CVpkPack*> TVpkPackList;
    typedef boost::unordered_map<std::string, CVpkPack*, stringhash> TVpkPackMap;

public:
    CEterPackManager();
    virtual ~CEterPackManager();

    void SetCacheMode();
    void SetRelativePathMode();

    void LoadStaticCache(const char* c_szFileName);

    void SetSearchMode(bool bPackFirst);
    int GetSearchMode();

    //THEMIDA
    bool Get(CMappedFile & rMappedFile, const char * c_szFileName, LPCVOID * pData);

    //THEMIDA
    bool GetFromPack(CMappedFile & rMappedFile, const char * c_szFileName, LPCVOID * pData);

    //THEMIDA
    bool GetFromFile(CMappedFile & rMappedFile, const char * c_szFileName, LPCVOID * pData);
    bool isExist(const char * c_szFileName);
    bool isExistInPack(const char * c_szFileName);

    // Original EPK registration (unchanged)
    bool RegisterPack(const char * c_szName, const char * c_szDirectory, const BYTE* c_pbIV = NULL);
    void RegisterRootPack(const char * c_szName);
    bool RegisterPackWhenPackMaking(const char * c_szName, const char * c_szDirectory, CEterPack* pPack);

    // NEW: VPK-specific registration
    bool RegisterVpkPack(const char* c_szName, const char* c_szDirectory);

    // NEW: Auto-detect registration - checks for .vpk first, falls back to .eix/.epk
    bool RegisterPackAuto(const char* c_szName, const char* c_szDirectory, const BYTE* c_pbIV = NULL);

    // NEW: Set the global VPK passphrase (used for all encrypted VPK packs)
    void SetVpkPassphrase(const char* passphrase);

    bool DecryptPackIV(DWORD key);

    const char * GetRootPackFileName();

    //for hybridcrypt
    //THEMIDA
    void WriteHybridCryptPackInfo(const char* pFileName);

    //THEMIDA
    void RetrieveHybridCryptPackKeys(const BYTE* pStream);
    //THEMIDA
    void RetrieveHybridCryptPackSDB(const BYTE* pStream);

public:
    void ArrangeMemoryMappedPack();

protected:
    int ConvertFileName(const char * c_szFileName, std::string & rstrFileName);
    bool CompareName(const char * c_szDirectoryName, DWORD iLength, const char * c_szFileName);

    CEterPack* FindPack(const char* c_szPathName);

    SCache* __FindCache(DWORD dwFileNameHash);
    void    __ClearCacheMap();

protected:
    bool                    m_bTryRelativePath;
    bool                    m_isCacheMode;
    int                     m_iSearchMode;

    CEterFileDict           m_FileDict;
    CEterPack               m_RootPack;
    TEterPackList           m_PackList;
    TEterPackMap            m_PackMap;
    TEterPackMap            m_DirPackMap;

    boost::unordered_map<DWORD, SCache> m_kMap_dwNameKey_kCache;

    CRITICAL_SECTION        m_csFinder;

    // NEW: VPK state
    TVpkPackList            m_VpkPackList;
    TVpkPackMap             m_VpkPackMap;
    std::string             m_strVpkPassphrase;
};
