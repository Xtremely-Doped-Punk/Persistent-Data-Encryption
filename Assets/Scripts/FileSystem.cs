using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Saves, loads and deletes all data in the game
/// </summary>
public static class FileSystem
{
    public const long FILE_SIZE_THRESHOLD = 10000; // file sizes more than appox 10kb will dealt through Steams
    public static long FILE_SERIALIZE_THRESHOLD => FILE_SIZE_THRESHOLD / 100; // to reduce the time take to encrypt

    /// <summary>
    /// Key (Secret Key): The key is a secret cryptographic value that is used for both encryption and decryption. 
    /// It's the main ingredient in the AES encryption process. AES operates on fixed block sizes, and 
    /// the strength of the encryption depends on the secrecy and randomness of the key.
    /// </summary>
    public static string KeyPlain = "<<<= $-$ Alter_Games_Proto_Planet $-$ =>>>"; // this type of key string should not be directly shared
    public static string Key64Encoded => Convert.ToBase64String(Encoding.UTF8.GetBytes(KeyPlain)); // this type of encoded string must only be shared in build
    public static byte[] KEY => GetNearestHash(Convert.FromBase64String(Key64Encoded), 32); // need to change


    /// <summary>
    /// IV (Initialization Vector): The IV is a random or pseudorandom value that is used along with the key 
    /// to initialize the encryption process. It helps ensure that identical plaintext messages 
    /// encrypt to different ciphertexts, adding an extra layer of security.     * 
    /// </summary>
    public static string IvPlain = "(-: <Secret> :-)"; // this type of key string should not be directly shared
    public static string Iv64Encoded => Convert.ToBase64String(Encoding.UTF8.GetBytes(IvPlain)); // this type of encoded string must only be shared in build
    public static byte[] IV => GetNearestHash(Convert.FromBase64String(Iv64Encoded), 16, 16); // IV must of 16bytes len (optional parameter)

    public enum EncrptionType
    {
        None,
        Aes,
        Swap
    }

    // for getting byte[] length in powers of 2
    private static byte[] GetNearestHash(byte[] k, int startLen = 16, int endLen = 256)
    {
        int power = startLen;
        if (k.Length < startLen)
        {
            Debug.LogWarning("FileSystem:: Given byte[] is too short to generate min 2^n hash key " +
                $"(for given 'n'-Range: [{startLen},{endLen}] ), padding 0s with min len...");
        }
        else if (k.Length > endLen)
        {
            power = endLen;
            Debug.LogWarning("FileSystem:: Given byte[] is too long to generate max 2^n hash key " +
                $"(for given 'n'-Range: [{startLen},{endLen}] ), truncating with max len...");
        }
        else
        {
            while (power < k.Length)
            {
                power <<= 1;
                if (power >= endLen)
                    break;
            }
            if (power > k.Length)
                power >>= 1;
        }
        //Debug.Log($"truncated len:{power}, actual len:{k.Length}");
        var p = new byte[power];
        Array.Copy(k, 0, p, 0, Mathf.Min(k.Length, p.Length));
        k = p;
        return k;
    }

    /// <summary>
    /// Save data to a file (overwrite completely) in .json converted form
    /// Provide data as byte[] inorder to not convert into .json format
    /// ex. for web-req data files directly pass byte[] and resp file.extention
    /// </summary>
    /// <typeparam name="T">typeof(data)</typeparam>
    /// <param name="data">data to be saved</param>
    /// <param name="RelativePath">relative path to persistant data path of file.extention</param>
    /// <param name="encrption">type of encrption</param>
    /// <param name="addExtension">should add extention if encrpted or not</param>
    /// <returns>bool successfully saved or not</returns>
    public static bool Save<T>(T data, string RelativePath, EncrptionType encrption = EncrptionType.None, bool addExtension = true)
    {
        if (data is Stream stream)
        {
            Debug.Log("FileSystem:: Given Stream based input data, So output will also be done through Streams");
            return Save(stream, RelativePath, encrption);
        }

        // get the data path of this save data
        string dataPath = GetDataPath(RelativePath, encrption, addExtension);

        byte[] byteData;

        if (data is byte[])
            byteData = data as byte[];
        else
        {
            string jsonData = JsonUtility.ToJson(data, true);
            byteData = Encoding.ASCII.GetBytes(jsonData);
        }

        // create the file in the path if it doesn't exist
        // if the file path or name does not exist, return the default SO
        if (!Directory.Exists(Path.GetDirectoryName(dataPath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dataPath));
        }
        //Debug.Log("ByteArray Write:\n" + Convert.ToBase64String(byteData));

        // attempt to save here data
        try
        {
            // save datahere
            ToggleEncryptDecryptData(ref byteData, encrption, false);
            File.WriteAllBytes(dataPath, byteData);
            Debug.Log("FileSystem:: <color=green>Save data to: </color>" + dataPath);
            return true;
        }
        catch (Exception e)
        {
            // write out error here
            Debug.LogError("FileSystem:: <color=red>Failed to save data to: </color>" + dataPath);
            Debug.LogWarning("FileSystem:: Error: " + e.Message + "\t Source: " + e.Source + "\n\n" + e.StackTrace);
            return false;
        }
    }

    private static bool Save(Stream dataStream, string RelativePath, EncrptionType encrption = EncrptionType.None, bool addExtension = true)
    {
        // get the data path of this save data
        string outputPath = GetDataPath(RelativePath, encrption, addExtension);

        // create the file in the path if it doesn't exist
        // if the file path or name does not exist, return the default SO
        if (!Directory.Exists(Path.GetDirectoryName(outputPath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        }

        Stream outputFile = new FileStream(outputPath, // path as string
            FileMode.Create, // create new or overwrite existing file
            FileAccess.Write, // write mode
            FileShare.None); // To decline the sharing of the file until the file is closed

        // attempt to save here data
        try
        {
            // save datahere
            WriteEncryptingData(dataStream, outputFile, encrption);
            Debug.Log("FileSystem:: <color=green>Save data to: </color>" + outputPath);
            return true;
        }
        catch (Exception e)
        {
            // write out error here
            Debug.LogError("FileSystem:: <color=red>Failed to save data to: </color>" + outputPath);
            Debug.LogWarning("FileSystem:: Error: " + e.Message + "\t Source: " + e.Source + "\n\n" + e.StackTrace);
            return false;
        }
    }


    /// <summary>
    /// Encrpyt or Decrypt byte array for the resp type
    /// </summary>
    /// <param name="data">relative path of file name along with .extension</param>
    /// <param name="type">type of encryption / decryption algorithm to use</param>
    /// <param name="isEncrypted">true => will return decryption the given data; false => will return encryption the given data</param>
    /// <returns>encrypted / decrypted byte[] of the given data</returns>
    private static void ToggleEncryptDecryptData(ref byte[] data, EncrptionType type, bool isEncrypted)
    {
        switch (type)
        {
            case EncrptionType.None:
                return;

            case EncrptionType.Aes:
                {
                    using ICryptoTransform cryptoTransform = GetAesCrypto(isEncrypted);

                    using var ms = new MemoryStream();
                    using (var cryptoStream = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(data, 0, data.Length);
                        cryptoStream.FlushFinalBlock();
                    }
                    data = ms.ToArray();
                    return;
                }
            case EncrptionType.Swap:
                {
                    //SwapCrpyto(ref data, isEncrypted, data.LongLength %File_Serialize_Threshold, 2, 2); // for randomness
                    SwapCrpyto(ref data, isEncrypted, test_size, test_iters, test_loops);
                    return;
                }
            default:
                throw new NotImplementedException($"FileSystem:: ToggleEncryptDecryptData not implemented for given type: {type}!");
        }
    }
    public static long test_size = FILE_SERIALIZE_THRESHOLD; public static int test_iters =2, test_loops =2;

    private static void WriteEncryptingData(Stream source, Stream destination, EncrptionType type)
    {
        if (!(source.CanRead && destination.CanWrite))
        {
            Debug.LogError($"Source stream:{source} and Destination stream:{destination} doesnt have the proper permissions!");
            return;
        }

        switch (type)
        {
            case EncrptionType.None:
                source.CopyTo(destination);
                break;

            case EncrptionType.Aes:
                {
                    using ICryptoTransform cryptoTransform = GetAesCrypto(false);
                    using (var cryptoStream = new CryptoStream(destination, cryptoTransform, CryptoStreamMode.Write))
                        source.CopyTo(cryptoStream);
                }
                break;

            case EncrptionType.Swap:
                {
                    byte[] data;
                    using (var buffer = new BufferedStream(source))
                    {
                        using var mStream = new MemoryStream();
                        buffer.CopyTo(mStream);
                        data = mStream.ToArray();
                    }
                    ToggleEncryptDecryptData(ref data, type, false);
                    destination.Write(data);
                }
                break;

            default:
                throw new NotImplementedException($"FileSystem:: ToggleEncryptDecryptData not implemented for given type: {type}!");
        }

        source.Close(); destination.Close();
    }
    private static Stream ReadDecryptingData(Stream stream, EncrptionType type)
    {
        if (!stream.CanRead)
        {
            Debug.LogError($"Source stream:{stream} doesnt have the proper permissions!");
            return default;
        }

        switch (type)
        {
            case EncrptionType.None:
                return stream;

            case EncrptionType.Aes:
                {
                    ICryptoTransform cryptoTransform = GetAesCrypto(true);
                    return new CryptoStream(stream, cryptoTransform, CryptoStreamMode.Read);
                }
            case EncrptionType.Swap:
                {
                    byte[] data;
                    using (var buffer = new BufferedStream(stream))
                    {
                        using var mStream = new MemoryStream();
                        buffer.CopyTo(mStream);
                        data = mStream.ToArray();
                    }
                    ToggleEncryptDecryptData(ref data, type, false);
                    return new MemoryStream(data);
                }

            default:
                throw new NotImplementedException($"FileSystem:: ToggleEncryptDecryptData not implemented for given type: {type}!");
        }
    }

    /// <summary>
    /// Load all data at a specified file and folder location
    /// </summary>
    /// <typeparam name="T">typeof(data)</typeparam>
    /// <param name="RelativePath">relative path to persistant data path of the file 
    /// (if encrpted extention is not used, keep addExtention paramenter = true)</param>
    /// <param name="encrption">type of encrption</param>
    /// <param name="addExtension">if encrption file extention is included in filename, then keep this parameter false</param>
    /// <returns></returns>
    public static T Load<T>(string RelativePath, EncrptionType encrption = EncrptionType.None, bool addExtension = true)
    {
        //Debug.Log(typeof(T) + "==" + typeof(Stream) + "->" + (typeof(T) == typeof(Stream)));
        if (typeof(T) == typeof(Stream)) // this comparison fails if comparing base-class vs derived-class
        {
            Debug.LogWarning("For Getiing Stream from Load() dont using <T> type parameter, rather use the main static fn");
            return (T)Convert.ChangeType(Load(RelativePath, encrption), typeof(T));
        }

        // get the data path of this save data
        string dataPath = GetDataPath(RelativePath, encrption, addExtension);

        // if the file path or name does not exist, return the default SO
        if (!Directory.Exists(Path.GetDirectoryName(dataPath)))
        {
            Debug.LogWarning("FileSystem:: File or path does not exist! " + dataPath);
            return default(T);
        }

        // load in the save data as byte array
        byte[] data = null;

        try
        {
            data = File.ReadAllBytes(dataPath);
            ToggleEncryptDecryptData(ref data, encrption, true);
            Debug.Log("FileSystem:: <color=green>Loaded all data from: </color>" + dataPath);
        }
        catch (Exception e)
        {
            Debug.LogError("FileSystem:: <color=red>Failed to load data from: </color>" + dataPath);
            Debug.LogWarning("FileSystem:: Error: " + e.Message + "\t Source: " + e.Source + "\n\n" + e.StackTrace);
            return default(T);
        }

        //Debug.Log("ByteArray Read:\n" + Convert.ToBase64String(data));

        if (data == null)
            return default(T);

        if (typeof(T) == typeof(byte[]))
            return (T)Convert.ChangeType(data, typeof(T));

        // convert the byte array to json
        string jsonData;

        // convert the byte array to json
        jsonData = Encoding.ASCII.GetString(data);

        // convert to the specified object type
        T returnedData;
        try
        {
            returnedData = JsonUtility.FromJson<T>(jsonData);
        }
        catch (Exception e)
        {
            Debug.LogError("FileSystem:: <color=red>Failed to convert data to given type: </color>" + typeof(T));
            Debug.LogWarning("FileSystem:: Error: " + e.Message + "\t Source: " + e.Source + "\n\n" + e.StackTrace);
            return default(T);
        }

        // return the casted json object to use
        return (T)Convert.ChangeType(returnedData, typeof(T));
    }

    public static Stream Load(string RelativePath, EncrptionType encrption = EncrptionType.None, bool addExtension = true)
    {
        // get the data path of this save data
        string inputPath = GetDataPath(RelativePath, encrption, addExtension);

        // if the file path or name does not exist, return the default SO
        if (!Directory.Exists(Path.GetDirectoryName(inputPath)))
        {
            Debug.LogWarning("FileSystem:: File or path does not exist! " + inputPath);
            return default;
        }

        Stream intputFile = new FileStream(inputPath, // path as string
            FileMode.Open, // open existing file
            FileAccess.Read, // read mode
            FileShare.Read); // To allow only subsequent reading of the file until the file is closed

        Stream outStream = default;
        try
        {
            //var outStream = ToggleEncryptDecryptData(File.ReadAllBytes(inputPath), encrption, true);
            outStream = ReadDecryptingData(intputFile, encrption);
            Debug.Log("FileSystem:: <color=green>Loaded all data from: </color>" + inputPath);
        }
        catch (Exception e)
        {
            Debug.LogError("FileSystem:: <color=red>Failed to load data from: </color>" + inputPath);
            Debug.LogWarning("FileSystem:: Error: " + e.Message + "\t Source: " + e.Source + "\n\n" + e.StackTrace);
        }
        return outStream;
    }
    private static string AddEncrptionExtention(string path, EncrptionType type)
    {
        if (type == EncrptionType.None)
            return path;
        else
            return Path.ChangeExtension(path, Path.GetExtension(path) + type.ToString());
    }

    /// <summary>
    /// Get absolute path of a relative path is stored on the specific platform's root path
    /// </summary>
    /// <param name="RelativePath">relative path of file name along with .extension</param>
    /// <returns>absolute path w.r.t persistant data path</returns>
    public static string GetDataPath(string RelativePath = "", EncrptionType type = EncrptionType.None, bool extension = true)
    {
        string filePath;

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
    // mac
    filePath = Application.streamingAssetsPath;

#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        // windows
        filePath = Application.persistentDataPath;

#elif UNITY_ANDROID
    // android
    filePath = Application.persistentDataPath;

#elif UNITY_IOS
    // ios
    filePath = Application.persistentDataPath;

#endif

        if (RelativePath != "")
            filePath = Path.Combine(filePath, RelativePath);

        if (extension)
            filePath = AddEncrptionExtention(filePath, type);
        return filePath;
    }

    private static ICryptoTransform GetAesCrypto(bool isEncrypted)
    {
        Aes aes = Aes.Create();
        aes.Key = KEY;
        aes.IV = IV;

        Debug.Log($"KeySize: {aes.KeySize}, Key: {BitConverter.ToString(aes.Key).Replace('-', '.')}");
        Debug.Log($"IvLen: {aes.IV.Length}, IV: {BitConverter.ToString(aes.IV).Replace('-', '.')}");

        return (!isEncrypted) ? aes.CreateEncryptor(aes.Key, aes.IV) : aes.CreateDecryptor(aes.Key, aes.IV);
    }


    private static void SwapCrpyto<T>(ref T[] data, bool isEncrypted, long size = FILE_SIZE_THRESHOLD, int iter = 1, int maxLoop = 5)
    {
        if (size > FILE_SIZE_THRESHOLD)
            size = FILE_SIZE_THRESHOLD;
        Debug.Log("FileSystem:: SwapCrypto - size given:" + size);
        SwapCrpyto(ref data, isEncrypted, (int)(data.Length / size), iter, maxLoop);
    }
    private static void SwapCrpyto<T>(ref T[] data, bool isEncrypted, int parts = 2, int iter = 1, int maxLoop=5, bool debug = false)
    {
        if (iter < 1) iter = 1;
        if (iter > data.Length / parts)
            iter = data.Length / parts;
        Debug.Log("FileSystem:: SwapCrypto - parts given:" + parts);

        int i = (!isEncrypted) ? 0 : iter-1;
        while (true)
        {
            var part = parts - i;
            Debug.Log($"in for (iter:{i+1}, part:{part})"); LogData(data);
            int loop = 0, start = 0, end = data.Length, len = end - start, window = len / part;

            while (window > 0)
            {
                int crem = window + (len % part);
                T[] temp = data[start..(start + window)];

                Array.Copy(data, end - window, data, start, window); // copy 2nd half to 1st half
                Array.Copy(temp, 0, data, end - window, window); // copy 1st half to 2nd half
               Debug.Log($"in while loop:{loop+1} (start:{start}, end:{end}, window[len/part:{len}/{part}]={window}, crem:{crem})"); LogData(data);

                start += crem;
                end -= crem;
                len = end - start;
                window = len / part;
                if ((++loop) >= maxLoop)
                    break;
            }

            if (!isEncrypted)
            {
                if (++i >= iter)
                    break;
            }
            else
            {
                if (--i < 0)
                    break;
            }
        }

        void LogData(T[] data)
        {
            if (debug)
                Debug.Log($"data:[{string.Join(",", data)}]");
        }
    }
}