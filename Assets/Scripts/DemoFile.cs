using GLTFast;
using QFSW.QC;
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DemoFile : MonoBehaviour
{
    long FILE_SIZE_THRESHOLD => FileSystem.FILE_SIZE_THRESHOLD;

    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Button _SerializeBtn, _ClearBtn;
    [SerializeField] private TMP_Dropdown _EncryptionOption;
    [SerializeField] private TMP_InputField _AssetPathInputField;
    [SerializeField] private TextMeshProUGUI _SaveTimeText, _LoadTimeText;
    [SerializeField] private TextMeshProUGUI _AssetRawDataText, _LoadedSaveDataText;
    public int StringLiteralsPerLine = 10;
    public bool useBinaryFormat = true;

    private long saveTime;
    private long loadTime;
    private FileInfo fileInfo;
    private Color defaultColor;
    private FileSystem.EncrptionType encrptionType;
    private string[] EncryptionTypes => Enum.GetNames(typeof(FileSystem.EncrptionType));

    private void Awake()
    {
        defaultColor = _AssetPathInputField.textComponent.color;

        _SerializeBtn.onClick.AddListener(() => SerializeFile());
        _ClearBtn.onClick.AddListener(() => ClearData());

        _EncryptionOption.ClearOptions();
        _EncryptionOption.AddOptions(EncryptionTypes.ToList());
        _EncryptionOption.onValueChanged.AddListener((val) => ChangeEncryption(val));

        _AssetPathInputField.onEndEdit.AddListener((val) => CheckValidAssetPath(val));
        _AssetPathInputField.onSelect.AddListener((val) => _AssetPathInputField.textComponent.color = defaultColor);

        /*
        var arr = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
        Debug.Log($"arr:[{string.Join(",", arr)}]");
        FileSystem.SwapCrpyto(ref arr, false, 4, 2, 2);
        Debug.Log($"arr:[{string.Join(",", arr)}]");
        FileSystem.SwapCrpyto(ref arr, true, 4, 2, 2);
        Debug.Log($"arr:[{string.Join(",", arr)}]");
        */
    }

    [Command]
    public void SetEncryptionTypeSwapParam(long size, int iters, int loops) 
    { FileSystem.test_size = size; FileSystem.test_iters = iters; FileSystem.test_loops = loops; }

    public void CheckValidAssetPath(string path)
    {
#if UNITY_ANDROID
        if (!File.Exists(path))
        {
            Debug.Log($"File not found! abs-path: {path}");

            path = Path.Combine(GetAndroidDCIMDirectory.GetDCIMDirectory(), _AssetPathInputField.text);
            if (!File.Exists(path))
            {
                Debug.Log($"File not found! dcim-path: {path}");

                path = FileSystem.GetDataPath(_AssetPathInputField.text); ;
                if (!File.Exists(path))
                {
                    Debug.Log($"File not found! persistent-path: {path}");
                }
            }
        }
#endif
        fileInfo = new FileInfo(path);
        _AssetPathInputField.textComponent.color = fileInfo.Exists ? Color.green : Color.red;

        if (fileInfo.Exists)
        {
            //var accessCtrl = fileInfo.GetAccessControl();
            //var accessRules = accessCtrl.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));

            Debug.Log(
                $"File Found, FileInfo => Name: {fileInfo.Name}, " +
                //$"\nAccess Ctrl: {accessCtrl}, " +
                $"\nFullName: {fileInfo.FullName}, " +
                $"\nExtension: {fileInfo.Extension}, " +
                $"\nAttributes: {fileInfo.Attributes}, " +
                $"\nLength: {fileInfo.Length}, " +
                $"\nDirectory: {fileInfo.Directory}" +
                $"\nDirName: {fileInfo.DirectoryName}, " +
                $"\nCreationTime: {fileInfo.CreationTime}, " +
                $"\nCreationTimeUtc: {fileInfo.CreationTimeUtc}, " +
                $"\nLastAccessTime: {fileInfo.LastAccessTime}, " +
                $"\nLastAccessTimeUtc: {fileInfo.LastAccessTimeUtc}, "
                );
        }
        else
            Debug.LogWarning($"File not found any possible paths! for given relative path: {path}");
    }

    public void ChangeEncryption(int EncryptionType)
    {
        encrptionType = Enum.Parse<FileSystem.EncrptionType>(EncryptionTypes[EncryptionType]);
    }

    public void SerializeFile()
    {
        if (fileInfo == null)
            return;

        if (!fileInfo.Exists)
        {
            Debug.LogError("Asset Data Path given is not vaild! Cant Serialize a file that doesn't exist!");
            _AssetRawDataText.text = "<color=#ff0000>Error asset path not valid!</color>";
            return;
        }

        if (fileInfo.Length > FILE_SIZE_THRESHOLD)
        {
            Debug.Log("File Size is Large, will be serialized through streams!");
            SerializeLargeFile();
        }
        else
        {
            Debug.Log("File Size is Small, will be serialized through data occuping memory!");
            SerializeSmallFile();
        }
    }

    private void SerializeSmallFile()
    {
        byte[] saveData = LoadDataFromPath(_AssetPathInputField.text);
        _AssetRawDataText.text = "Loaded from Asset file (in binary converted string format):\r\n" + FormatedDataString(saveData);

        long startTime = DateTime.Now.Ticks;
        if (FileSystem.Save(saveData, fileInfo.Name, encrptionType))
        {
            saveTime = DateTime.Now.Ticks - startTime;
            _SaveTimeText.SetText($"Save Time: {(saveTime / TimeSpan.TicksPerMillisecond):N4}ms");

            startTime = DateTime.Now.Ticks;
            try
            {
                byte[] data = FileSystem.Load<byte[]>(fileInfo.Name, encrptionType);
                _LoadedSaveDataText.text = "Loaded from Saved file (in binary converted string format):\r\n" + FormatedDataString(data);
                
                loadTime = DateTime.Now.Ticks - startTime;
                _LoadTimeText.SetText($"Load Time: {(loadTime / TimeSpan.TicksPerMillisecond):N4}ms");
                //return data;
            }
            catch (Exception)
            {
                Debug.LogError($"Could not read Saved file!");
                _LoadedSaveDataText.text = "<color=#ff0000>Error reading save file!</color>";
                //return default;
            }
        }
        else
        {
            Debug.LogError("Could not save Asset file!");
            _AssetRawDataText.text = "<color=#ff0000>Error saving asset data!</color>";
            //return default;
        }
    }

    private void SerializeLargeFile()
    {
        _AssetRawDataText.text = "Partially Loaded from Asset file (in binary converted string format):\r\n" + 
            LoadDataFromStream(fileInfo.OpenRead(), useBinaryFormat);

        long startTime = DateTime.Now.Ticks;
        if (FileSystem.Save(fileInfo.OpenRead(), fileInfo.Name, encrptionType))
        {
            saveTime = DateTime.Now.Ticks - startTime;
            _SaveTimeText.SetText($"Save Time: {(saveTime / TimeSpan.TicksPerMillisecond):N4}ms");

            startTime = DateTime.Now.Ticks;
            try
            {
                string formatted;
                /*
                try // try load using stream (returns crypto-stream if encrypted) // its not working exactyly the way its supposed to
                {
                    using (var outStream = FileSystem.Load(fileInfo.Name, EncrptionType))
                        formatted = LoadDataFromStream(outStream, useBinaryFormat);

                    loadTime = DateTime.Now.Ticks - startTime;

                    if (fileInfo.Extension.ToLower().Contains("glb"))
                        using (var outStream = FileSystem.Load(fileInfo.Name, EncrptionType))
                            LoadGltfBinaryFromStream(outStream);
                }
                catch // try load using byte[]
                */
                {
                    var outData = FileSystem.Load<byte[]>(fileInfo.Name, encrptionType);
                    formatted = FormatedDataString(outData);
                    loadTime = DateTime.Now.Ticks - startTime;

                    LoadGltfBinaryFromBData(outData);
                }

                _LoadedSaveDataText.text = "Partially Loaded from Save file (in binary converted string format):\r\n" + formatted;
                //outStream.Close();
                _LoadTimeText.SetText($"Load Time: {(loadTime / TimeSpan.TicksPerMillisecond):N4}ms");

            }
            catch (Exception)
            {
                Debug.LogError($"Could not read Saved file!");
                _LoadedSaveDataText.text = "<color=#ff0000>Error reading save file!</color>";
            }
        }
        else
        {
            Debug.LogError("Could not save Asset file!");
            _AssetRawDataText.text = "<color=#ff0000>Error saving asset data!</color>";
        }
    }

    private string LoadDataFromStream(Stream stream, bool useBinary)
    {
        string formatted = string.Empty;
        try
        {
            if (useBinary)
            {
                byte[] data = new byte[FILE_SIZE_THRESHOLD];
                stream.Read(data, 0, data.Length);
                formatted = FormatedDataString(data);
            }
            else // this approach not working
            {
                char[] data = new char[FILE_SIZE_THRESHOLD];
                using StreamReader reader = new StreamReader(stream);
                reader.Read(data, 0, data.Length);
                formatted = FormattedDataString(data);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Stream Read Error: [CanRead:{stream.CanRead}] " + e.Message + "\t Source: " + e.Source + "\n\n" + e.StackTrace);
        }
        return formatted;
    }
    private byte[] LoadDataFromPath(string path) => File.ReadAllBytes(path);

    private string FormattedDataString(char[] data) => string.Concat(data);
    private string FormatedDataString(byte[] data)
    {
        byte[] trucatedDat;
        if (data.Length > FILE_SIZE_THRESHOLD)
            trucatedDat = data[0..(int)FILE_SIZE_THRESHOLD];
        else
            trucatedDat = data;
        string rawData = BitConverter.ToString(trucatedDat).Replace('-', ' ');
        int count = 0;
        for (int i = 0; i < rawData.Length; i++)
        {
            if (rawData[i] == ' ')
                count++;
            if (count >= StringLiteralsPerLine)
            {
                count = 0;
                rawData.Insert(i + 1, "\n");
            }
        }

        return rawData;
    }

    public void ClearData()
    {
        if (fileInfo == null)
            return;

        // get the data path of this save data
        string path = FileSystem.GetDataPath(fileInfo.Name, encrptionType);
        
        if (File.Exists(path))
        {
            File.Delete(path);
            _LoadedSaveDataText.text = "Loaded Saved Data:\nin binary converted string format here...";
        }
    }

    void LoadGltfBinaryFromStream(Stream stream)
    {
        // clear previous models
        for (int i = 0; i < spawnPoint.childCount; i++)
            Destroy(spawnPoint.GetChild(i).gameObject);

        // load new model
        byte[] data;
        try
        {
            using (var buffer = new BufferedStream(stream))
            {
                using var mStream = new MemoryStream();
                buffer.CopyTo(mStream);
                data = mStream.ToArray();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Stream Read Error: [CanRead:{stream.CanRead}] " + e.Message + "\t Source: " + e.Source + "\n\n" + e.StackTrace);
            return;
        }

        LoadGltfBinaryFromBData(data);
    }
    async void LoadGltfBinaryFromBData(byte[] data)
    {
        var gltf = new GltfImport();
        bool success = await gltf.LoadGltfBinary(
            data,
            // The URI of the original data is important for resolving relative URIs within the glTF
            new Uri(fileInfo.FullName)
            );
        if (success)
            success = await gltf.InstantiateMainSceneAsync(spawnPoint);

        if (!success)
            Debug.LogError($"Load GLTF not successfull for the given data!");
    }
}



internal class GetAndroidDCIMDirectory
{
    static AndroidJavaClass unityClass;
    static AndroidJavaObject unityActivity;
    static AndroidJavaObject pluginInstance;
    public static string GetDCIMDirectory()
    {
        unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        unityActivity = unityClass.GetStatic<AndroidJavaObject>("currentActivity");
        pluginInstance = new AndroidJavaObject("com.example.getexternaldirpath.GetExternalDirectories");
        if (pluginInstance == null)
        {
            Debug.LogError("Plugin Instance not found");
        }
        pluginInstance.CallStatic("receiveUnityActivity", unityActivity);
        Debug.Log("Got location: " + pluginInstance.Call<string>("GetExternalDirectory"));
        return pluginInstance.Call<string>("GetExternalDirectory");
    }
}