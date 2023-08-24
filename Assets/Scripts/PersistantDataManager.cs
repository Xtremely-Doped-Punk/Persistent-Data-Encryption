using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class PersistantDataManager : MonoBehaviour
{
    public enum SaveMode
    {
        Pref,
        // Player Prefs - these are not designed for storing game state.
        // Only...Player Preferences such as graphic & audio settings.

        BFile,
        // BinaryFormatter - this class is dangerous and insecure.
        // Use of this class can allow an attacker to take over the system.
        // https://docs.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-security-guide

    }

    [Header("Data")]
    public SaveMode mode;
    public TestData data;

    [Header("References")]
    public TextMeshProUGUI keyPressCountText;

    void Start()
    {
        LoadAndUpdate();
    }

    void Update()
    {
        if (!Input.anyKeyDown) return;

        UpdateAndSave();
    }

    void UpdateAndSave()
    {
        data.pressCount++;
        keyPressCountText.text = "No. of Keys Pressed \ntill now: " + data.pressCount.ToString();

        switch (mode)
        {
            case SaveMode.Pref:
                PlayerPrefs.SetInt(TestData.hashPressCount, data.pressCount);
                break;

            case SaveMode.BFile:
                BinaryFormatter bf = new BinaryFormatter();
                FileStream file = File.Open(Path.Combine(Application.persistentDataPath, TestData.hashFile), FileMode.Create);
                bf.Serialize(file, data);
                file.Close();
                break;
        }
    }

    void LoadAndUpdate()
    {
        data = new();
        switch (mode)
        {
            case SaveMode.Pref:
                if (PlayerPrefs.HasKey(TestData.hashPressCount))
                    data.pressCount = PlayerPrefs.GetInt(TestData.hashPressCount);
                break;

            case SaveMode.BFile:
                var path = Path.Combine(Application.persistentDataPath, TestData.hashFile);
                if (File.Exists(path))
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    FileStream file = File.Open(path, FileMode.Open);
                    data = (TestData)bf.Deserialize(file);
                    file.Close();
                }
                break;
        }

        keyPressCountText.text = "No. of Keys Pressed \ntill now: " + data.pressCount.ToString();
    }

    public static IEnumerator DownloadAndSave(string url, string _saveat, Action<bool, string> callback, Action<float> downloadProgress = null)
    {
        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();
        try
        {
            if (downloadProgress != null)
            {
                while (!request.isDone)
                {
                    downloadProgress.Invoke(request.downloadProgress);
                }
            }
            if (request.responseCode < 400)
            {
                var contentType = request.GetResponseHeader("Content-Type");
                Debug.Log("Content Type :: " + contentType);
                if (!Directory.Exists(Path.GetDirectoryName(_saveat)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_saveat));
                }

                try
                {
                    File.WriteAllBytes(_saveat, request.downloadHandler.data);
                    callback?.Invoke(true, _saveat);
                }
                catch (Exception e)
                {
                    Debug.LogError("File-Error: " + e.Message);
                    throw e;
                }
            }
            else
            {
                var exception = new Exception($"UnityWebRequest-Error:{request.error}, code:{request.responseCode}");
                throw exception;
            }
        }
        catch (Exception exception)
        {
            Debug.Log("UnityWebRequest-Error: " + request.error);
            callback?.Invoke(false, exception.Message);
        }
    }
}

[Serializable]
public class TestData
{
    public const string hashFile = "TestData.dat";
    public const string hashPressCount = "PressCount";

    public int pressCount;

    public TestData()
    {
        pressCount = 0;
    }
}