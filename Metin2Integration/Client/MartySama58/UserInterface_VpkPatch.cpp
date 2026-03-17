//
// UserInterface_VpkPatch.cpp - MartySama 5.8 Profile
//
// Shows the exact changes needed in UserInterface.cpp to load VPK packs.
// Based on the original MartySama 5.8
// Binary & S-Source/Binary/Client/UserInterface/UserInterface.cpp.
//
// MartySama 5.8 uses the same pack loading pattern as 40250, with the
// same HybridCrypt key delivery system. The only difference is the
// #if defined(USE_RELATIVE_PATH) conditional and NDEBUG-based pack-first.
//
// Original pack loading code (around line 557-579):
//
//     CTextFileLoader::SetCacheMode();
//     #if defined(USE_RELATIVE_PATH)
//     CEterPackManager::Instance().SetRelativePathMode();
//     #endif
//     CEterPackManager::Instance().SetCacheMode();
//     CEterPackManager::Instance().SetSearchMode(bPackFirst);
//
//     CSoundData::SetPackMode();
//
//     std::string strPackName, strTexCachePackName;
//     for (DWORD i = 1; i < TextLoader.GetLineCount() - 1; i += 2)
//     {
//         const std::string & c_rstFolder = TextLoader.GetLineString(i);
//         const std::string & c_rstName = TextLoader.GetLineString(i + 1);
//
//         strPackName = stFolder + c_rstName;
//         strTexCachePackName = strPackName + "_texcache";
//
//         CEterPackManager::Instance().RegisterPack(strPackName.c_str(), c_rstFolder.c_str());
//         CEterPackManager::Instance().RegisterPack(strTexCachePackName.c_str(), c_rstFolder.c_str());
//     }
//
//     CEterPackManager::Instance().RegisterRootPack((stFolder + std::string("root")).c_str());
//
// ===== PATCHED VERSION BELOW =====
//
// Replace the above block with:
//
//     CTextFileLoader::SetCacheMode();
//     #if defined(USE_RELATIVE_PATH)
//     CEterPackManager::Instance().SetRelativePathMode();
//     #endif
//     CEterPackManager::Instance().SetCacheMode();
//     CEterPackManager::Instance().SetSearchMode(bPackFirst);
//
//     CSoundData::SetPackMode();
//
//     // SET VPK PASSPHRASE BEFORE REGISTERING PACKS
//     // Option 1: Hardcoded (simplest for private servers)
//     CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
//
//     // Option 2: From a config file
//     // std::string vpkPass;
//     // if (TextLoader.GetValue("VPK_PASSPHRASE", vpkPass))
//     //     CEterPackManager::Instance().SetVpkPassphrase(vpkPass.c_str());
//
//     std::string strPackName, strTexCachePackName;
//     for (DWORD i = 1; i < TextLoader.GetLineCount() - 1; i += 2)
//     {
//         const std::string & c_rstFolder = TextLoader.GetLineString(i);
//         const std::string & c_rstName = TextLoader.GetLineString(i + 1);
//
//         strPackName = stFolder + c_rstName;
//         strTexCachePackName = strPackName + "_texcache";
//
//         // RegisterPackAuto checks for .vpk first, falls back to .eix/.epk
//         CEterPackManager::Instance().RegisterPackAuto(strPackName.c_str(), c_rstFolder.c_str());
//         CEterPackManager::Instance().RegisterPackAuto(strTexCachePackName.c_str(), c_rstFolder.c_str());
//     }
//
//     CEterPackManager::Instance().RegisterRootPack((stFolder + std::string("root")).c_str());
//
// ===== END PATCH =====
//
// That's it. Two changes:
//   1. Add SetVpkPassphrase() call before the registration loop
//   2. Change RegisterPack() to RegisterPackAuto() (2 occurrences)
//
// The RegisterRootPack() call already handles VPK detection internally
// (see EterPackManager_Vpk.cpp).
//
// ===== ADDITIONAL NOTES FOR MARTYSAMA 5.8 =====
//
// MartySama 5.8 also receives HybridCrypt keys from the server in two places:
//
//   1. AccountConnector.cpp (line ~256):
//      CEterPackManager::Instance().RetrieveHybridCryptPackKeys(kPacket.m_pStream);
//
//   2. AccountConnector.cpp (line ~272):
//      CEterPackManager::Instance().RetrieveHybridCryptPackSDB(kPacket.m_pStream);
//
//   3. AccountConnector.cpp (line ~314):
//      CEterPackManager::instance().DecryptPackIV(dwPanamaKey);
//
//   4. PythonNetworkStreamPhaseHandShake.cpp (line ~180):
//      CEterPackManager::Instance().RetrieveHybridCryptPackKeys(kPacket.m_pStream);
//
//   5. PythonNetworkStreamPhaseHandShake.cpp (line ~200):
//      CEterPackManager::Instance().RetrieveHybridCryptPackSDB(kPacket.m_pStream);
//
// These do NOT need to be changed. They only affect legacy EPK packs
// (types 3, 4, 5) and are harmless when VPK packs are loaded - VPK packs
// are not registered in m_PackMap, so the HybridCrypt key loop simply
// skips them. Both systems coexist transparently.
//
