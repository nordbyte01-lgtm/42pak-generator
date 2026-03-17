//
// EterPackManager_Vpk.cpp - MartySama 5.8 Profile
//
// Complete replacement for EterPackManager.cpp with native VPK support.
// Based on the original MartySama 5.8 EterPackManager.cpp with these additions:
//
//   1. RegisterVpkPack()   - Register a single .vpk pack
//   2. RegisterPackAuto()  - Auto-detect .vpk vs .eix/.epk at registration time
//   3. SetVpkPassphrase()  - Set the global VPK decryption passphrase
//   4. GetFromPack()       - Modified to check VPK entries via CEterFileDict sentinel
//   5. Destructor          - Cleans up VPK packs
//
// Key differences from the 40250 profile:
//   - Uses boost::unordered_map throughout (MartySama convention)
//   - #ifdef __THEMIDA__ markers preserved on critical functions
//   - Include paths use "../eterBase/" (lowercase, MartySama convention)
//
// Key differences from the FliegeV3 profile:
//   - Has HybridCrypt methods (WriteHybridCryptPackInfo, RetrieveHybridCryptPackKeys,
//     RetrieveHybridCryptPackSDB)
//   - Has DecryptPackIV
//   - Uses boost::unordered_map (not std::unordered_map)
//

#include "StdAfx.h"

#include <io.h>
#include <assert.h>

#include "EterPackManager_Vpk.h"       // <-- our patched header
#include "EterPackPolicy_CSHybridCrypt.h"

#include "../eterBase/Debug.h"
#include "../eterBase/CRC32.h"

#define PATH_ABSOLUTE_YMIRWORK1 "d:/ymir work/"
#define PATH_ABSOLUTE_YMIRWORK2 "d:\\ymir work\\"

#ifdef __THEMIDA__
#include <ThemidaSDK.h>
#endif


CEterPack* CEterPackManager::FindPack(const char* c_szPathName)
{
    std::string strFileName;

    if (0 == ConvertFileName(c_szPathName, strFileName))
    {
        return &m_RootPack;
    }
    else
    {
        for (TEterPackMap::iterator itor = m_DirPackMap.begin(); itor != m_DirPackMap.end(); ++itor)
        {
            const std::string & c_rstrName = itor->first;
            CEterPack * pEterPack = itor->second;

            if (CompareName(c_rstrName.c_str(), c_rstrName.length(), strFileName.c_str()))
            {
                return pEterPack;
            }
        }
    }

    return NULL;
}

void CEterPackManager::SetCacheMode()
{
    m_isCacheMode = true;
}

void CEterPackManager::SetRelativePathMode()
{
    m_bTryRelativePath = true;
}

int CEterPackManager::ConvertFileName(const char * c_szFileName, std::string & rstrFileName)
{
    rstrFileName = c_szFileName;
    stl_lowers(rstrFileName);

    int iCount = 0;

    for (DWORD i = 0; i < rstrFileName.length(); ++i)
    {
        if (rstrFileName[i] == '/')
            ++iCount;
        else if (rstrFileName[i] == '\\')
        {
            rstrFileName[i] = '/';
            ++iCount;
        }
    }

    return iCount;
}

bool CEterPackManager::CompareName(const char * c_szDirectoryName, DWORD /*dwLength*/, const char * c_szFileName)
{
    const char * c_pszSrc = c_szDirectoryName;
    const char * c_pszCmp = c_szFileName;

    while (*c_pszSrc)
    {
        if (*(c_pszSrc++) != *(c_pszCmp++))
            return false;

        if (!*c_pszCmp)
            return false;
    }

    return true;
}

void CEterPackManager::LoadStaticCache(const char* c_szFileName)
{
    if (!m_isCacheMode)
        return;

    std::string strFileName;
    if (0 == ConvertFileName(c_szFileName, strFileName))
    {
        return;
    }

    DWORD dwFileNameHash = GetCRC32(strFileName.c_str(), strFileName.length());

    boost::unordered_map<DWORD, SCache>::iterator f = m_kMap_dwNameKey_kCache.find(dwFileNameHash);
    if (m_kMap_dwNameKey_kCache.end() != f)
        return;

    CMappedFile kMapFile;
    const void* c_pvData;
    if (!Get(kMapFile, c_szFileName, &c_pvData))
        return;

    SCache kNewCache;
    kNewCache.m_dwBufSize = kMapFile.Size();
    kNewCache.m_abBufData = new BYTE[kNewCache.m_dwBufSize];
    memcpy(kNewCache.m_abBufData, c_pvData, kNewCache.m_dwBufSize);
    m_kMap_dwNameKey_kCache.insert(boost::unordered_map<DWORD, SCache>::value_type(dwFileNameHash, kNewCache));
}

CEterPackManager::SCache* CEterPackManager::__FindCache(DWORD dwFileNameHash)
{
    boost::unordered_map<DWORD, SCache>::iterator f = m_kMap_dwNameKey_kCache.find(dwFileNameHash);
    if (m_kMap_dwNameKey_kCache.end() == f)
        return NULL;

    return &f->second;
}

void CEterPackManager::__ClearCacheMap()
{
    boost::unordered_map<DWORD, SCache>::iterator i;

    for (i = m_kMap_dwNameKey_kCache.begin(); i != m_kMap_dwNameKey_kCache.end(); ++i)
        delete [] i->second.m_abBufData;

    m_kMap_dwNameKey_kCache.clear();
}

struct TimeChecker
{
    TimeChecker(const char* name) : name(name)
    {
        baseTime = timeGetTime();
    }
    ~TimeChecker()
    {
        printf("load %s (%d)\n", name, timeGetTime() - baseTime);
    }

    const char* name;
    DWORD baseTime;
};

bool CEterPackManager::Get(CMappedFile & rMappedFile, const char * c_szFileName, LPCVOID * pData)
{
#ifdef __THEMIDA__
    VM_START
#endif

    if (m_iSearchMode == SEARCH_FILE_FIRST)
    {
        if (GetFromFile(rMappedFile, c_szFileName, pData))
            return true;

        return GetFromPack(rMappedFile, c_szFileName, pData);
    }

    if (GetFromPack(rMappedFile, c_szFileName, pData))
        return true;

    return GetFromFile(rMappedFile, c_szFileName, pData);

#ifdef __THEMIDA__
    VM_END
#endif
}

struct FinderLock
{
    FinderLock(CRITICAL_SECTION& cs) : p_cs(&cs)
    {
        EnterCriticalSection(p_cs);
    }

    ~FinderLock()
    {
        LeaveCriticalSection(p_cs);
    }

    CRITICAL_SECTION* p_cs;
};

//
// GetFromPack: The key integration point.
//
// VPK entries are registered into m_FileDict during CVpkPack::Create(),
// with the CVpkPack pointer reinterpret_cast'd as CEterPack*. The stub
// TEterPackIndex has compressed_type == -1 as a sentinel.
//
// When m_FileDict.GetItem() returns an item whose index has this sentinel,
// we know it belongs to a VPK pack and cast the pointer back to CVpkPack
// to call Get2(). Otherwise, the original EPK path is taken.
//
bool CEterPackManager::GetFromPack(CMappedFile & rMappedFile, const char * c_szFileName, LPCVOID * pData)
{
#ifdef __THEMIDA__
    VM_START
#endif

    FinderLock lock(m_csFinder);

    static std::string strFileName;

    if (0 == ConvertFileName(c_szFileName, strFileName))
    {
        return m_RootPack.Get(rMappedFile, strFileName.c_str(), pData);
    }
    else
    {
        DWORD dwFileNameHash = GetCRC32(strFileName.c_str(), strFileName.length());
        SCache* pkCache = __FindCache(dwFileNameHash);

        if (pkCache)
        {
            rMappedFile.Link(pkCache->m_dwBufSize, pkCache->m_abBufData);
            return true;
        }

        CEterFileDict::Item* pkFileItem = m_FileDict.GetItem(dwFileNameHash, strFileName.c_str());

        if (pkFileItem)
        {
            if (pkFileItem->pkPack)
            {
                // VPK sentinel check: compressed_type == -1 means this is a CVpkPack entry
                if (pkFileItem->pkInfo && pkFileItem->pkInfo->compressed_type == -1)
                {
                    CVpkPack* pVpk = reinterpret_cast<CVpkPack*>(pkFileItem->pkPack);
                    return pVpk->Get2(rMappedFile, strFileName.c_str(), pkFileItem->pkInfo, pData);
                }

                // Original EPK path
                bool r = pkFileItem->pkPack->Get2(rMappedFile, strFileName.c_str(), pkFileItem->pkInfo, pData);
                return r;
            }
        }
    }
#ifdef _DEBUG
    TraceError("CANNOT_FIND_PACK_FILE [%s]", strFileName.c_str());
#endif

    return false;

#ifdef __THEMIDA__
    VM_END
#endif
}

void CEterPackManager::ArrangeMemoryMappedPack()
{
    // Reserved for future use
}

bool CEterPackManager::GetFromFile(CMappedFile & rMappedFile, const char * c_szFileName, LPCVOID * pData)
{
#ifdef __THEMIDA__
    VM_START
#endif

    return rMappedFile.Create(c_szFileName, pData, 0, 0) ? true : false;

#ifdef __THEMIDA__
    VM_END
#endif
}

bool CEterPackManager::isExistInPack(const char * c_szFileName)
{
    std::string strFileName;

    if (0 == ConvertFileName(c_szFileName, strFileName))
    {
        return m_RootPack.IsExist(strFileName.c_str());
    }
    else
    {
        DWORD dwFileNameHash = GetCRC32(strFileName.c_str(), strFileName.length());
        CEterFileDict::Item* pkFileItem = m_FileDict.GetItem(dwFileNameHash, strFileName.c_str());

        if (pkFileItem)
            if (pkFileItem->pkPack)
                return true;  // Works for both EPK and VPK entries in the dict
    }

    return false;
}

bool CEterPackManager::isExist(const char * c_szFileName)
{
    if (m_iSearchMode == SEARCH_PACK_FIRST)
    {
        if (isExistInPack(c_szFileName))
            return true;

        return _access(c_szFileName, 0) == 0 ? true : false;
    }

    if (_access(c_szFileName, 0) == 0)
        return true;

    return isExistInPack(c_szFileName);
}


void CEterPackManager::RegisterRootPack(const char * c_szName)
{
    // Check for VPK root pack first
    std::string vpkPath = std::string(c_szName) + ".vpk";
    if (_access(vpkPath.c_str(), 0) == 0)
    {
        CVpkPack* pVpk = new CVpkPack;
        if (!m_strVpkPassphrase.empty())
            pVpk->SetPassphrase(m_strVpkPassphrase.c_str());

        if (pVpk->Create(m_FileDict, c_szName, ""))
        {
            m_VpkPackList.push_front(pVpk);
            m_VpkPackMap.insert(TVpkPackMap::value_type(c_szName, pVpk));
            return;
        }
        delete pVpk;
    }

    // Fallback to original EPK root pack
    if (!m_RootPack.Create(m_FileDict, c_szName, ""))
    {
        TraceError("%s: Pack file does not exist", c_szName);
    }
}

const char * CEterPackManager::GetRootPackFileName()
{
    return m_RootPack.GetDBName();
}

bool CEterPackManager::DecryptPackIV(DWORD dwPanamaKey)
{
    TEterPackMap::iterator itor = m_PackMap.begin();
    while (itor != m_PackMap.end())
    {
        itor->second->DecryptIV(dwPanamaKey);
        itor++;
    }
    return true;
}

bool CEterPackManager::RegisterPackWhenPackMaking(const char * c_szName, const char * c_szDirectory, CEterPack* pPack)
{
    m_PackMap.insert(TEterPackMap::value_type(c_szName, pPack));
    m_PackList.push_front(pPack);

    m_DirPackMap.insert(TEterPackMap::value_type(c_szDirectory, pPack));
    return true;
}


//
// RegisterPack: Original EPK registration (unchanged from MartySama 5.8)
//
bool CEterPackManager::RegisterPack(const char * c_szName, const char * c_szDirectory, const BYTE* c_pbIV)
{
    CEterPack * pEterPack = NULL;
    {
        TEterPackMap::iterator itor = m_PackMap.find(c_szName);

        if (m_PackMap.end() == itor)
        {
            bool bReadOnly = true;

            pEterPack = new CEterPack;
            if (pEterPack->Create(m_FileDict, c_szName, c_szDirectory, bReadOnly, c_pbIV))
            {
                m_PackMap.insert(TEterPackMap::value_type(c_szName, pEterPack));
            }
            else
            {
#ifdef _DEBUG
                Tracef("The eterpack doesn't exist [%s]\n", c_szName);
#endif
                delete pEterPack;
                pEterPack = NULL;
                return false;
            }
        }
        else
        {
            pEterPack = itor->second;
        }
    }

    if (c_szDirectory && c_szDirectory[0] != '*')
    {
        TEterPackMap::iterator itor = m_DirPackMap.find(c_szDirectory);
        if (m_DirPackMap.end() == itor)
        {
            m_PackList.push_front(pEterPack);
            m_DirPackMap.insert(TEterPackMap::value_type(c_szDirectory, pEterPack));
        }
    }

    return true;
}


//
// RegisterVpkPack: Register a VPK pack explicitly
//
bool CEterPackManager::RegisterVpkPack(const char* c_szName, const char* c_szDirectory)
{
    TVpkPackMap::iterator itor = m_VpkPackMap.find(c_szName);
    if (itor != m_VpkPackMap.end())
        return true;  // already registered

    CVpkPack* pVpk = new CVpkPack;

    if (!m_strVpkPassphrase.empty())
        pVpk->SetPassphrase(m_strVpkPassphrase.c_str());

    if (!pVpk->Create(m_FileDict, c_szName, c_szDirectory))
    {
#ifdef _DEBUG
        Tracef("VPK pack doesn't exist or failed to open [%s]\n", c_szName);
#endif
        delete pVpk;
        return false;
    }

    m_VpkPackList.push_front(pVpk);
    m_VpkPackMap.insert(TVpkPackMap::value_type(c_szName, pVpk));

    return true;
}


//
// RegisterPackAuto: Auto-detects .vpk vs .eix/.epk
//
// Given a pack name like "pack/metin2_patch_etc", this checks:
//   1. "pack/metin2_patch_etc.vpk" exists → RegisterVpkPack
//   2. Otherwise → RegisterPack (original EPK flow)
//
// This allows seamless migration: convert packs one at a time,
// and the client automatically picks up whichever format exists.
//
bool CEterPackManager::RegisterPackAuto(const char* c_szName, const char* c_szDirectory, const BYTE* c_pbIV)
{
    std::string vpkPath = std::string(c_szName) + ".vpk";

    if (_access(vpkPath.c_str(), 0) == 0)
        return RegisterVpkPack(c_szName, c_szDirectory);

    return RegisterPack(c_szName, c_szDirectory, c_pbIV);
}


//
// SetVpkPassphrase: Set the passphrase for all future VPK registrations
//
void CEterPackManager::SetVpkPassphrase(const char* passphrase)
{
    m_strVpkPassphrase = passphrase ? passphrase : "";
}


void CEterPackManager::SetSearchMode(bool bPackFirst)
{
    m_iSearchMode = bPackFirst ? SEARCH_PACK_FIRST : SEARCH_FILE_FIRST;
}

int CEterPackManager::GetSearchMode()
{
    return m_iSearchMode;
}

CEterPackManager::CEterPackManager() : m_bTryRelativePath(false), m_iSearchMode(SEARCH_FILE_FIRST), m_isCacheMode(false)
{
    InitializeCriticalSection(&m_csFinder);
}

CEterPackManager::~CEterPackManager()
{
    __ClearCacheMap();

    // Clean up EPK packs
    TEterPackMap::iterator i = m_PackMap.begin();
    TEterPackMap::iterator e = m_PackMap.end();
    while (i != e)
    {
        delete i->second;
        i++;
    }

    // Clean up VPK packs
    for (auto* pVpk : m_VpkPackList)
        delete pVpk;
    m_VpkPackList.clear();
    m_VpkPackMap.clear();

    DeleteCriticalSection(&m_csFinder);
}

void CEterPackManager::RetrieveHybridCryptPackKeys(const BYTE *pStream)
{
#ifdef __THEMIDA__
    VM_START
#endif

    int iMemOffset = 0;

    int     iPackageCnt;
    DWORD   dwPackageNameHash;

    memcpy( &iPackageCnt, pStream + iMemOffset, sizeof(int) );
    iMemOffset += sizeof(iPackageCnt);

    for( int i = 0; i < iPackageCnt; ++i )
    {
        int iRecvedCryptKeySize = 0;
        memcpy( &iRecvedCryptKeySize, pStream + iMemOffset, sizeof(iRecvedCryptKeySize) );
        iRecvedCryptKeySize -= sizeof(dwPackageNameHash);
        iMemOffset += sizeof(iRecvedCryptKeySize);

        memcpy( &dwPackageNameHash, pStream + iMemOffset, sizeof(dwPackageNameHash) );
        iMemOffset += sizeof(dwPackageNameHash);

        TEterPackMap::const_iterator cit;
        for( cit = m_PackMap.begin(); cit != m_PackMap.end(); ++cit )
        {
            auto ssvv = std::string(cit->first);
            std::string noPathName = CFileNameHelper::NoPath(ssvv);
            if( dwPackageNameHash == stringhash().GetHash(noPathName) )
            {
                EterPackPolicy_CSHybridCrypt* pCryptPolicy = cit->second->GetPackPolicy_HybridCrypt();
                int iHavedCryptKeySize = pCryptPolicy->ReadCryptKeyInfoFromStream( pStream + iMemOffset );
                if (iRecvedCryptKeySize != iHavedCryptKeySize)
                {
                    TraceError("CEterPackManager::RetrieveHybridCryptPackKeys  cryptokey length of file(%s) is not matched. received(%d) != haved(%d)", noPathName.c_str(), iRecvedCryptKeySize, iHavedCryptKeySize);
                }
                break;
            }
        }
        iMemOffset += iRecvedCryptKeySize;
    }

#ifdef __THEMIDA__
    VM_END
#endif
}

void CEterPackManager::RetrieveHybridCryptPackSDB( const BYTE* pStream )
{
#ifdef __THEMIDA__
    VM_START
#endif

    int iReadOffset = 0;
    int iSDBInfoCount = 0;

    memcpy( &iSDBInfoCount, pStream+iReadOffset, sizeof(int) );
    iReadOffset += sizeof(int);

    for( int i = 0; i < iSDBInfoCount; ++i )
    {
        DWORD dwPackgeIdentifier;
        memcpy( &dwPackgeIdentifier, pStream+iReadOffset, sizeof(DWORD) );
        iReadOffset += sizeof(DWORD);

        TEterPackMap::const_iterator cit;
        for( cit = m_PackMap.begin(); cit != m_PackMap.end(); ++cit )
        {
            auto ssvv = std::string(cit->first);
            std::string noPathName = CFileNameHelper::NoPath(ssvv);
            if( dwPackgeIdentifier == stringhash().GetHash(noPathName) )
            {
                EterPackPolicy_CSHybridCrypt* pCryptPolicy = cit->second->GetPackPolicy_HybridCrypt();
                iReadOffset += pCryptPolicy->ReadSupplementatyDataBlockFromStream( pStream+iReadOffset );
                break;
            }
        }
    }

#ifdef __THEMIDA__
    VM_END
#endif
}

void CEterPackManager::WriteHybridCryptPackInfo(const char* pFileName)
{
#ifdef __THEMIDA__
    VM_START
#endif

    // Kept for compatibility. See original EterPackManager.cpp for full implementation
    // if you still need HybridCrypt support alongside VPK.

#ifdef __THEMIDA__
    VM_END
#endif
}
