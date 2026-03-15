#include "StdAfx.h"
#include "VpkCrypto.h"

#include <openssl/evp.h>
#include <openssl/hmac.h>
#include <openssl/err.h>

#include "blake3.h"

#include <lz4.h>

#include <cstring>

bool VpkCrypto::AesGcmDecrypt(const unsigned char* ciphertext, int ciphertextLen,
                                const unsigned char* key,
                                const unsigned char* nonce,
                                const unsigned char* tag,
                                unsigned char* plaintext)
{
    EVP_CIPHER_CTX* ctx = EVP_CIPHER_CTX_new();
    if (!ctx)
        return false;

    bool success = false;

    do
    {
        if (EVP_DecryptInit_ex(ctx, EVP_aes_256_gcm(), nullptr, nullptr, nullptr) != 1)
            break;

        if (EVP_CIPHER_CTX_ctrl(ctx, EVP_CTRL_GCM_SET_IVLEN, 12, nullptr) != 1)
            break;

        if (EVP_DecryptInit_ex(ctx, nullptr, nullptr, key, nonce) != 1)
            break;

        int outLen = 0;
        if (EVP_DecryptUpdate(ctx, plaintext, &outLen, ciphertext, ciphertextLen) != 1)
            break;

        if (EVP_CIPHER_CTX_ctrl(ctx, EVP_CTRL_GCM_SET_TAG, 16,
                                 const_cast<unsigned char*>(tag)) != 1)
            break;

        int finalLen = 0;
        if (EVP_DecryptFinal_ex(ctx, plaintext + outLen, &finalLen) != 1)
            break;

        success = true;
    } while (false);

    EVP_CIPHER_CTX_free(ctx);
    return success;
}

bool VpkCrypto::DeriveKey(const char* passphrase,
                            const unsigned char* salt, int saltLen,
                            unsigned char* outKey, int outKeyLen)
{
    const char* prefix = "42PK-v1:";
    std::string fullPass = std::string(prefix) + passphrase;

    int result = PKCS5_PBKDF2_HMAC(
        fullPass.c_str(),
        static_cast<int>(fullPass.size()),
        salt, saltLen,
        200000,
        EVP_sha512(),
        outKeyLen,
        outKey
    );

    SecureZero(&fullPass[0], fullPass.size());

    return (result == 1);
}

bool VpkCrypto::ComputeHmacSha256(std::ifstream& stream, long long length,
                                    const unsigned char* key, int keyLen,
                                    unsigned char* outHmac)
{
    HMAC_CTX* ctx = HMAC_CTX_new();
    if (!ctx)
        return false;

    if (HMAC_Init_ex(ctx, key, keyLen, EVP_sha256(), nullptr) != 1)
    {
        HMAC_CTX_free(ctx);
        return false;
    }

    const int CHUNK_SIZE = 65536;
    unsigned char buffer[65536];
    long long remaining = length;

    while (remaining > 0)
    {
        int toRead = (remaining > CHUNK_SIZE) ? CHUNK_SIZE : static_cast<int>(remaining);
        stream.read(reinterpret_cast<char*>(buffer), toRead);
        int bytesRead = static_cast<int>(stream.gcount());

        if (bytesRead <= 0)
            break;

        HMAC_Update(ctx, buffer, bytesRead);
        remaining -= bytesRead;
    }

    unsigned int hmacLen = 32;
    HMAC_Final(ctx, outHmac, &hmacLen);
    HMAC_CTX_free(ctx);

    return (remaining == 0);
}

void VpkCrypto::Blake3Hash(const unsigned char* data, int dataLen,
                            unsigned char* outHash)
{
    blake3_hasher hasher;
    blake3_hasher_init(&hasher);
    blake3_hasher_update(&hasher, data, static_cast<size_t>(dataLen));
    blake3_hasher_finalize(&hasher, outHash, BLAKE3_OUT_LEN);
}

int VpkCrypto::Lz4Decompress(const unsigned char* src, int srcLen,
                               unsigned char* dst, int originalSize)
{
    return LZ4_decompress_safe(
        reinterpret_cast<const char*>(src),
        reinterpret_cast<char*>(dst),
        srcLen,
        originalSize
    );
}

bool VpkCrypto::ConstantTimeEquals(const unsigned char* a,
                                     const unsigned char* b, int len)
{
    int diff = 0;
    for (int i = 0; i < len; ++i)
        diff |= a[i] ^ b[i];
    return (diff == 0);
}

void VpkCrypto::SecureZero(void* ptr, size_t len)
{
#ifdef _WIN32
    SecureZeroMemory(ptr, len);
#else
    volatile unsigned char* p = static_cast<volatile unsigned char*>(ptr);
    while (len--)
        *p++ = 0;
#endif
}
