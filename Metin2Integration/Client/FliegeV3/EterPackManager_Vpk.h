#pragma once

//
// EterPackManager_Vpk.h — FliegeV3 Profile
//
// Drop-in replacement for EterPackManager.h that adds native VPK support
// to the FliegeV3 binary-src architecture.
//
// Key differences from the 40250 profile:
//   - No HybridCrypt methods (WriteHybridCryptPackInfo, RetrieveHybridCryptPackKeys,
//     RetrieveHybridCryptPackSDB) — FliegeV3 does not use the HybridCrypt system
//   - No DecryptPackIV (FliegeV3 uses per-pack DecryptIndexFile instead)
//   - Uses boost::unordered_map to match FliegeV3's CEterFileDict (boost::unordered_multimap)
//   - ENABLE_LOCAL_FILE_LOADING conditional preserved in isExist
//
// Usage: Replace #include "EterPackManager.h" with this file, or merge
// the additions into your existing EterPackManager.h.
//
// Reference: Original FliegeV3 EterPackManager.h (82 lines)
//

#include <windows.h>
#include <map>
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
    typedef std::unordered_map<std::string, CEterPack*, stringhash> TEterPackMap;

    // NEW: VPK pack storage
    typedef std::list<CVpkPack*> TVpkPackList;
    typedef std::unordered_map<std::string, CVpkPack*, stringhash> TVpkPackMap;

public:
    CEterPackManager();
    virtual ~CEterPackManager();

    void SetCacheMode();
    void SetRelativePathMode();

    void LoadStaticCache(const char* c_szFileName);

    void SetSearchMode(bool bPackFirst);
    int GetSearchMode();

    bool Get(CMappedFile & rMappedFile, const char * c_szFileName, LPCVOID * pData);
    bool GetFromPack(CMappedFile & rMappedFile, const char * c_szFileName, LPCVOID * pData);
    bool GetFromFile(CMappedFile & rMappedFile, const char * c_szFileName, LPCVOID * pData);
    bool isExist(const char * c_szFileName);
    bool isExistInPack(const char * c_szFileName);

    // Original EPK registration (unchanged)
    bool RegisterPack(const char * c_szName, const char * c_szDirectory, const BYTE* c_pbIV = NULL);
    void RegisterRootPack(const char * c_szName);
    bool RegisterPackWhenPackMaking(const char * c_szName, const char * c_szDirectory, CEterPack* pPack);

    // NEW: VPK-specific registration
    bool RegisterVpkPack(const char* c_szName, const char* c_szDirectory);

    // NEW: Auto-detect registration — checks for .vpk first, falls back to .eix/.epk
    bool RegisterPackAuto(const char* c_szName, const char* c_szDirectory, const BYTE* c_pbIV = NULL);

    // NEW: Set the global VPK passphrase (used for all encrypted VPK packs)
    void SetVpkPassphrase(const char* passphrase);

    const char * GetRootPackFileName();

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

    std::unordered_map<DWORD, SCache> m_kMap_dwNameKey_kCache;

    CRITICAL_SECTION        m_csFinder;

    // NEW: VPK state
    TVpkPackList            m_VpkPackList;
    TVpkPackMap             m_VpkPackMap;
    std::string             m_strVpkPassphrase;
};
