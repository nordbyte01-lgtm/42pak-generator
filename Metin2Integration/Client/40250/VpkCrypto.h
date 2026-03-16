#pragma once

#include <fstream>

class VpkCrypto
{
public:
    static bool AesGcmDecrypt(const unsigned char* ciphertext, int ciphertextLen,
                               const unsigned char* key,
                               const unsigned char* nonce,
                               const unsigned char* tag,
                               unsigned char* plaintext);

    static bool DeriveKey(const char* passphrase,
                           const unsigned char* salt, int saltLen,
                           unsigned char* outKey, int outKeyLen);

    static bool ComputeHmacSha256(std::ifstream& stream, long long length,
                                   const unsigned char* key, int keyLen,
                                   unsigned char* outHmac);

    static void Blake3Hash(const unsigned char* data, int dataLen,
                            unsigned char* outHash);

    static int Lz4Decompress(const unsigned char* src, int srcLen,
                              unsigned char* dst, int originalSize);

    static int ZstdDecompress(const unsigned char* src, int srcLen,
                               unsigned char* dst, int originalSize);

    static int BrotliDecompress(const unsigned char* src, int srcLen,
                                 unsigned char* dst, int originalSize);

    static bool ConstantTimeEquals(const unsigned char* a,
                                    const unsigned char* b, int len);

    static void SecureZero(void* ptr, size_t len);
};
