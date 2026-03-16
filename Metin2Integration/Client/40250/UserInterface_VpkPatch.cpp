//
// UserInterface_VpkPatch.cpp
//
// Shows the exact changes needed in UserInterface.cpp to load VPK packs.
// Based on the original 40250 source/UserInterface/UserInterface.cpp.
//
// Original pack loading code (lines ~220-238):
//
//     CEterPackManager::Instance().SetCacheMode();
//     CEterPackManager::Instance().SetSearchMode(bPackFirst);
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
//     CEterPackManager::Instance().SetCacheMode();
//     CEterPackManager::Instance().SetSearchMode(bPackFirst);
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
//   2. Change RegisterPack() to RegisterPackAuto()
//
// The RegisterRootPack() call already handles VPK detection internally
// (see EterPackManager_Vpk.cpp).
//
