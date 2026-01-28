using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace nexoZapytanie
{
    class Szyfrowanie
    {
        public static byte[] Zaszyfruj(string plainText, string password)
        {
            return Zaszyfruj(plainText, CreateKey(password), CreateIV(password)); 
        }

        public static byte[] Zaszyfruj(string plainText, byte[] Key, byte[] IV)
        {
            // Check arguments. 
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException(nameof(plainText));
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException(nameof(Key));
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException(nameof(Key));
            byte[] encrypted;
            // Create an Rijndael object 
            // with the specified key and IV. 
            using (Rijndael rijAlg = Rijndael.Create())
            {
                rijAlg.Key = Key;
                rijAlg.IV = IV;

                // Create a decrytor to perform the stream transform.
                ICryptoTransform encryptor = rijAlg.CreateEncryptor(rijAlg.Key, rijAlg.IV);

                // Create the streams used for encryption. 
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {

                            //Write all data to the stream.
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }


            // Return the encrypted bytes from the memory stream. 
            return encrypted;

        }

        public static string Odszyfruj(byte[] cipherText, string password)
        {
            return Odszyfruj(cipherText, CreateKey(password), CreateIV(password));
        }

        public static string Odszyfruj(byte[] cipherText, byte[] Key, byte[] IV)
        {
            // Check arguments. 
            if (cipherText == null || cipherText.Length < 0)
                throw new ArgumentNullException(nameof(cipherText));
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException(nameof(Key));
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException(nameof(Key));
            if (cipherText.Length == 0)
                return string.Empty;

            // Declare the string used to hold 
            // the decrypted text. 
            string plaintext = null;

            // Create an Rijndael object 
            // with the specified key and IV. 
            using (Rijndael rijAlg = Rijndael.Create())
            {
                rijAlg.Key = Key;
                rijAlg.IV = IV;

                // Create a decrytor to perform the stream transform.
                ICryptoTransform decryptor = rijAlg.CreateDecryptor(rijAlg.Key, rijAlg.IV);

                // Create the streams used for decryption. 
                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {

                            // Read the decrypted bytes from the decrypting stream 
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }

            }

            return plaintext;

        }

        public static byte[] CreateIV(string strPassword)
        {
            byte[] bytIV;
            byte[] bytSalt = System.Text.Encoding.ASCII.GetBytes("saltisthekey");
            Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(strPassword, bytSalt);

            bytIV = pdb.GetBytes(16);

            return bytIV;
        }

        public static byte[] CreateKey(string strPassword)
        {
            byte[] bytKey;
            byte[] bytSalt = System.Text.Encoding.ASCII.GetBytes("saltisthekey");
            Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(strPassword, bytSalt);

            bytKey = pdb.GetBytes(32);

            return bytKey;
        }
    }
}
