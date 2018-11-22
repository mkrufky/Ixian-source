﻿using DLT.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Crypto.Engines;
using System.Security.Cryptography;
using Java.Security;
using Java.Security.Spec;
using System.Text;
using Javax.Crypto;

namespace CryptoLibs
{
    class BouncyCastleAndroid : DLT.ICryptoLib
    {
        byte[] publicKeyBytes;
        byte[] privateKeyBytes;

        // Private variables used for AES key expansion
        private int PBKDF2_iterations = 10000;
        private string AES_algorithm = "AES/CBC/PKCS7Padding";

        // Private variables used for Chacha
        private readonly int chacha_rounds = 20;


        public BouncyCastleAndroid()
        {
            publicKeyBytes = null;
            privateKeyBytes = null;
        }

        private Java.Math.BigInteger bigEndianToLittleEndian(byte[] input)
        {
            return new Java.Math.BigInteger(input.Prepend((byte)0).ToArray());
        }

        private byte[] littleEndianToBigEndian(Java.Math.BigInteger bigInt)
        {
            byte[] input = bigInt.ToByteArray();
            if (input[0] == 0x00)
            {
                return input.Skip(1).ToArray();
            }

            return input;
        }

        private byte[] rsaKeyToBytes(KeyPair rsaKey, bool includePrivateParameters)
        {
            List<byte> bytes = new List<byte>();

            KeyFactory kf = KeyFactory.GetInstance("RSA");
            // the ToByteArray() function returns big-endian bytes, we need little-endian
            if (includePrivateParameters)
            {
                RSAPrivateCrtKeySpec rsaParams = (RSAPrivateCrtKeySpec)kf.GetKeySpec(rsaKey.Private, Java.Lang.Class.FromType(typeof(RSAPrivateCrtKeySpec)));
                byte[] modulus = littleEndianToBigEndian(rsaParams.Modulus);
                bytes.AddRange(BitConverter.GetBytes(modulus.Length));
                bytes.AddRange(modulus);

                byte[] publicExponent = littleEndianToBigEndian(rsaParams.PublicExponent);
                bytes.AddRange(BitConverter.GetBytes(publicExponent.Length));
                bytes.AddRange(publicExponent);


                byte[] primeP = littleEndianToBigEndian(rsaParams.PrimeP);
                bytes.AddRange(BitConverter.GetBytes(primeP.Length));
                bytes.AddRange(primeP);

                byte[] primeQ = littleEndianToBigEndian(rsaParams.PrimeQ);
                bytes.AddRange(BitConverter.GetBytes(primeQ.Length));
                bytes.AddRange(primeQ);

                byte[] primeExponentP = littleEndianToBigEndian(rsaParams.PrimeExponentP);
                bytes.AddRange(BitConverter.GetBytes(primeExponentP.Length));
                bytes.AddRange(primeExponentP);

                byte[] primeExponentQ = littleEndianToBigEndian(rsaParams.PrimeExponentQ);
                bytes.AddRange(BitConverter.GetBytes(primeExponentQ.Length));
                bytes.AddRange(primeExponentQ);

                byte[] crtCoefficient = littleEndianToBigEndian(rsaParams.CrtCoefficient);
                bytes.AddRange(BitConverter.GetBytes(crtCoefficient.Length));
                bytes.AddRange(crtCoefficient);

                byte[] privateExponent = littleEndianToBigEndian(rsaParams.PrivateExponent);
                bytes.AddRange(BitConverter.GetBytes(privateExponent.Length));
                bytes.AddRange(privateExponent);
            }
            else
            {
                RSAPublicKeySpec rsaPubParams = (RSAPublicKeySpec)kf.GetKeySpec(rsaKey.Public, Java.Lang.Class.FromType(typeof(RSAPublicKeySpec)));
                byte[] modulus = littleEndianToBigEndian(rsaPubParams.Modulus);
                bytes.AddRange(BitConverter.GetBytes(modulus.Length));
                bytes.AddRange(modulus);

                byte[] publicExponent = littleEndianToBigEndian(rsaPubParams.PublicExponent);
                bytes.AddRange(BitConverter.GetBytes(publicExponent.Length));
                bytes.AddRange(publicExponent);
            }

            return bytes.ToArray();
        }

        private KeyPair rsaKeyFromBytes(byte [] keyBytes)
        {
            try
            {

                int offset = 0;
                int dataLen = 0;

                dataLen = BitConverter.ToInt32(keyBytes, offset);
                offset += 4;
                Java.Math.BigInteger modulus = bigEndianToLittleEndian(keyBytes.Skip(offset).Take(dataLen).ToArray());
                offset += dataLen;

                dataLen = BitConverter.ToInt32(keyBytes, offset);
                offset += 4;
                Java.Math.BigInteger exponent = bigEndianToLittleEndian(keyBytes.Skip(offset).Take(dataLen).ToArray());
                offset += dataLen;

                RSAPrivateCrtKeySpec privKeySpec = null;

                if (keyBytes.Length > offset)
                {
                    dataLen = BitConverter.ToInt32(keyBytes, offset);
                    offset += 4;
                    Java.Math.BigInteger P = bigEndianToLittleEndian(keyBytes.Skip(offset).Take(dataLen).ToArray());
                    offset += dataLen;

                    dataLen = BitConverter.ToInt32(keyBytes, offset);
                    offset += 4;
                    Java.Math.BigInteger Q = bigEndianToLittleEndian(keyBytes.Skip(offset).Take(dataLen).ToArray());
                    offset += dataLen;

                    dataLen = BitConverter.ToInt32(keyBytes, offset);
                    offset += 4;
                    Java.Math.BigInteger DP = bigEndianToLittleEndian(keyBytes.Skip(offset).Take(dataLen).ToArray());
                    offset += dataLen;

                    dataLen = BitConverter.ToInt32(keyBytes, offset);
                    offset += 4;
                    Java.Math.BigInteger DQ = bigEndianToLittleEndian(keyBytes.Skip(offset).Take(dataLen).ToArray());
                    offset += dataLen;

                    dataLen = BitConverter.ToInt32(keyBytes, offset);
                    offset += 4;
                    Java.Math.BigInteger InverseQ = bigEndianToLittleEndian(keyBytes.Skip(offset).Take(dataLen).ToArray());
                    offset += dataLen;

                    dataLen = BitConverter.ToInt32(keyBytes, offset);
                    offset += 4;
                    Java.Math.BigInteger D = bigEndianToLittleEndian(keyBytes.Skip(offset).Take(dataLen).ToArray());
                    offset += dataLen;
                    privKeySpec = new RSAPrivateCrtKeySpec(modulus, exponent, D, P, Q, DP, DQ, InverseQ);
                }

                RSAPublicKeySpec pubKeySpec = new RSAPublicKeySpec(modulus, exponent);

                KeyFactory keyFactory = KeyFactory.GetInstance("RSA");
                IPublicKey pubKey = keyFactory.GeneratePublic(pubKeySpec);
                IPrivateKey privKey = null;
                if (privKeySpec != null)
                {
                    privKey = keyFactory.GeneratePrivate(privKeySpec);
                }

                return new KeyPair(pubKey, privKey);
            }catch(Exception)
            {
                Logging.warn("An exception occured while trying to reconstruct PKI from bytes");
            }
            return null;
        }
        
        public bool testKeys(byte[] plain)
        {
            byte[] encrypted = encryptWithRSA(plain, publicKeyBytes);
            byte[] signature = getSignature(plain, privateKeyBytes);

            if (!verifySignature(plain, publicKeyBytes, signature))
            {
                Logging.warn(string.Format("Error verifying signatures while testing keys."));
                return false;
            }

            if (!decryptWithRSA(encrypted, privateKeyBytes).SequenceEqual(plain))
            {
                Logging.warn(string.Format("Error decrypting data while testing keys."));
                return false;
            }

            return true;
        }

        // Generates keys for RSA signing
        public bool generateKeys(int keySize)
        {
            KeyPair kp = null;
            try
            {
                KeyPairGenerator kpg = KeyPairGenerator.GetInstance("RSA");
                kpg.Initialize(keySize);
                kp = kpg.GenKeyPair();
                privateKeyBytes = rsaKeyToBytes(kp, true);
                publicKeyBytes = rsaKeyToBytes(kp, false);

                byte[] plain = Encoding.UTF8.GetBytes("Plain text string");
                if (!testKeys(plain))
                {
                    privateKeyBytes = null;
                    publicKeyBytes = null;
                    return false;
                }
            }
            catch (Exception e)
            {
                privateKeyBytes = null;
                publicKeyBytes = null;
                Logging.warn(string.Format("Exception while generating signature keys: {0}", e.ToString()));
                return false;
            }
            return true;
        }

        public byte[] getPublicKey()
        {
            return publicKeyBytes;
        }

        public byte[] getPrivateKey()
        {
            return privateKeyBytes;
        }
        
        public byte[] getSignature(byte[] input_data, byte[] privateKey)
        {
            try
            {
                KeyPair kp = rsaKeyFromBytes(privateKey);

                Signature sig = Signature.GetInstance("SHA512withRSA");
                sig.InitSign(kp.Private);
                sig.Update(input_data);
                byte[] signature = sig.Sign();
                return signature;
            }
            catch (Exception e)
            {
                Logging.warn(string.Format("Cannot generate signature: {0}", e.Message));
            }
            return null;
        }

        public bool verifySignature(byte[] input_data, byte[] publicKey, byte[] signature)
        {
            try
            {

                KeyPair kp = rsaKeyFromBytes(publicKey);

                Signature sig = Signature.GetInstance("SHA512withRSA");
                sig.InitVerify(kp.Public);
                sig.Update(input_data);
                return sig.Verify(signature);
            }
            catch (Exception e)
            {
                Logging.warn(string.Format("Invalid public key {0}:{1}", publicKey, e.Message));
            }
            return false;
        }

        // Encrypt data using RSA
        public byte[] encryptWithRSA(byte[] input, byte[] publicKey)
        {
            KeyPair kp = rsaKeyFromBytes(publicKey);
            Cipher cipher = Cipher.GetInstance("RSA/None/PKCS1Padding");
            cipher.Init(Javax.Crypto.CipherMode.EncryptMode, kp.Public);
            return cipher.DoFinal(input);
        }


        // Decrypt data using RSA
        public byte[] decryptWithRSA(byte[] input, byte[] privateKey)
        {
            KeyPair kp = rsaKeyFromBytes(privateKey);
            Cipher cipher = Cipher.GetInstance("RSA/None/PKCS1Padding");
            cipher.Init(Javax.Crypto.CipherMode.DecryptMode, kp.Private);
            return cipher.DoFinal(input);
        }

        // Encrypt data using AES
        public byte[] encryptDataAES(byte[] input, byte[] key)
        {
            IBufferedCipher outCipher = CipherUtilities.GetCipher(AES_algorithm);

            int blockSize = outCipher.GetBlockSize();
            // Perform key expansion
            byte[] salt = new byte[blockSize];
            using (RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider())
            {
                // Fill the array with a random value.
                rngCsp.GetBytes(salt);
            }

            ParametersWithIV withIV = new ParametersWithIV(new KeyParameter(key), salt);
            try
            {
                outCipher.Init(true, withIV);
            }
            catch (Exception e)
            {
                Logging.error(string.Format("Error initializing encryption. {0}", e.ToString()));
                return null;
            }

            List<byte> bytes = new List<byte>();
            bytes.AddRange(salt);
            bytes.AddRange(outCipher.DoFinal(input));

            return bytes.ToArray();
        }

        // Decrypt data using AES
        public byte[] decryptDataAES(byte[] input, byte [] key, int inOffset = 0)
        {

            IBufferedCipher inCipher = CipherUtilities.GetCipher(AES_algorithm);

            int blockSize = inCipher.GetBlockSize();
            // Perform key expansion
            byte[] salt = new byte[blockSize];

            for (int i = 0; i < blockSize; i++)
            {
                salt[i] = input[inOffset + i];
            }

            ParametersWithIV withIV = new ParametersWithIV(new KeyParameter(key), salt);

            try
            {
                inCipher.Init(false, withIV);
            }
            catch (Exception e)
            {
                Logging.error(string.Format("Error initializing decryption. {0}", e.ToString()));
            }

            byte[] bytes = inCipher.DoFinal(input, inOffset + blockSize, input.Length - inOffset - blockSize);

            return bytes;
        }

        private static byte[] getPbkdf2BytesFromPassphrase(string password, byte[] salt, int iterations, int byteCount)
        {
            var pbkdf2 = new Rfc2898DeriveBytes(password, salt);
            pbkdf2.IterationCount = iterations;
            return pbkdf2.GetBytes(byteCount);
        }

        // Encrypt using password
        public byte[] encryptWithPassword(byte[] data, string password)
        {
            byte[] salt = new byte[16];
            using (RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider())
            {
                // Fill the array with a random value.
                rngCsp.GetBytes(salt);
            }
            byte[] key = getPbkdf2BytesFromPassphrase(password, salt, PBKDF2_iterations, 16);
            byte[] ret_data = encryptDataAES(data, key);

            List<byte> tmpList = new List<byte>();
            tmpList.AddRange(salt);
            tmpList.AddRange(ret_data);

            return tmpList.ToArray();
        }

        // Decrypt using password
        public byte[] decryptWithPassword(byte[] data, string password)
        {
            byte[] salt = new byte[16];
            for(int i = 0; i < 16; i++)
            {
                salt[i] = data[i];
            }
            byte[] key = getPbkdf2BytesFromPassphrase(password, salt, PBKDF2_iterations, 16);
            return decryptDataAES(data, key, 16);
        }

        // Encrypt data using Chacha engine
        public byte[] encryptWithChacha(byte[] input, byte[] key)
        {
            // Create a buffer that will contain the encrypted output and an 8 byte nonce
            byte[] outData = new byte[input.Length + 8];

            // Generate the 8 byte nonce
            Random rnd = new Random();
            byte[] nonce = new byte[8];
            rnd.NextBytes(nonce);

            // Prevent leading 0 to avoid edge cases
            if (nonce[0] == 0)
                nonce[0] = 1;
            
            // Generate the Chacha engine
            var parms = new ParametersWithIV(new KeyParameter(key), nonce);
            var chacha = new ChaChaEngine(chacha_rounds);
            chacha.Init(true, parms);

            // Encrypt the input data while maintaing an 8 byte offset at the start
            chacha.ProcessBytes(input, 0, input.Length, outData, 8);

            // Copy the 8 byte nonce to the start of outData buffer
            Buffer.BlockCopy(nonce, 0, outData, 0, 8);

            // Return the encrypted data buffer
            return outData;
        }

        // Decrypt data using Chacha engine
        public byte[] decryptWithChacha(byte[] input, byte[] key)
        {
            // Extract the nonce from the input
            byte[] nonce = input.Take(8).ToArray();

            // Generate the Chacha engine
            var parms = new ParametersWithIV(new KeyParameter(key), nonce);
            var chacha = new ChaChaEngine(chacha_rounds);
            chacha.Init(false, parms);

            // Create a buffer that will contain the decrypted output
            byte[] outData = new byte[input.Length - 8];

            // Decrypt the input data
            chacha.ProcessBytes(input, 8, input.Length - 8, outData, 0);

            // Return the decrypted data buffer
            return outData;
        }

        public byte[] generateChildKey(byte[] parentKey, int seed = 0)
        {
            /*RSACryptoServiceProvider origRsa = rsaKeyFromBytes(parentKey);
            if(origRsa.PublicOnly)
            {
                Logging.error("Child key cannot be generated from a public key! Private key is also required.");
                return null;
            }
            RSAParameters origKey = origRsa.ExportParameters(true);
            RsaKeyPairGenerator kpGenerator = new RsaKeyPairGenerator();
            int seed_len = origKey.P.Length + origKey.Q.Length;
            if (seed != 0)
            {
                seed_len += 4;
            }
            byte[] child_seed = new byte[seed_len];
            Array.Copy(origKey.P, 0, child_seed, 0, origKey.P.Length);
            Array.Copy(origKey.Q, 0, child_seed, origKey.P.Length, origKey.Q.Length);
            if(seed != 0)
            {
                Array.Copy(BitConverter.GetBytes(seed), 0, child_seed, origKey.P.Length + origKey.Q.Length, 4);
            }

            Org.BouncyCastle.Crypto.Digests.Sha512Digest key_digest = new Org.BouncyCastle.Crypto.Digests.Sha512Digest();
            Org.BouncyCastle.Crypto.Prng.DigestRandomGenerator digest_rng = new Org.BouncyCastle.Crypto.Prng.DigestRandomGenerator(key_digest);
            digest_rng.AddSeedMaterial(child_seed);
            // TODO: Check if certainty of 80 is good enough for us
            RsaKeyGenerationParameters keyParams = new RsaKeyGenerationParameters(BigInteger.ValueOf(0x10001), new Org.BouncyCastle.Security.SecureRandom(digest_rng), 4096, 80);
            RsaKeyPairGenerator keyGen = new RsaKeyPairGenerator();
            keyGen.Init(keyParams);
            AsymmetricCipherKeyPair keyPair = keyGen.GenerateKeyPair();
            //
            RSACryptoServiceProvider newRsa = (RSACryptoServiceProvider)DotNetUtilities.ToRSA((RsaPrivateCrtKeyParameters)keyPair.Private);
            return rsaKeyToBytes(newRsa, true);*/
            return null;
        }

    }
}