using Firebase.Extensions;
using Firebase.RemoteConfig;
using Firebase.Storage;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class RemoteConfiguration : MonoBehaviour
{
    public Text textToUpdate;

    private Dictionary<string, object> defaults = new Dictionary<string, object>();

    // Start is called before the first frame update
    void Start()
    {
        //This us the RemoteConfig example, not the JSON load that was required
        defaults.Add("config_test_string", "default local string");
        defaults.Add("config_test_int", 1);
        defaults.Add("config_test_float", 1.0);
        defaults.Add("config_test_bool", false);

        FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(defaults)
          .ContinueWithOnMainThread(task => { FetchDataAsync(); });


        //This is the exercise requirement, to load a JSON file with data and unpack it (uses FirebaseStorage)
        FirebaseStorage storage = FirebaseStorage.GetInstance("gs://communix-exercise.appspot.com");
        StorageReference reference = storage.GetReference("mainConfigurations.json");

        // Fetch the download URL
        reference.GetDownloadUrlAsync().ContinueWithOnMainThread(task => {
            if (!task.IsFaulted && !task.IsCanceled)
            {
                Debug.Log("Download URL: " + task.Result);
                // ... now download the file via WWW or UnityWebRequest.

                StartCoroutine(GetRequest(task.Result.AbsoluteUri));
            }

            else
                Debug.Log(task.Exception);
        });
    }

    public Task FetchDataAsync()
    {
        Debug.Log("Fetching data...");
        Task fetchTask = FirebaseRemoteConfig.DefaultInstance.FetchAsync(TimeSpan.Zero);
        return fetchTask.ContinueWithOnMainThread(FetchComplete);
    }

    private void FetchComplete(Task fetchTask)
    {
        if (!fetchTask.IsCompleted)
        {
            Debug.LogError("Retrieval hasn't finished.");
            return;
        }

        var remoteConfig = FirebaseRemoteConfig.DefaultInstance;
        var info = remoteConfig.Info;
        if (info.LastFetchStatus != LastFetchStatus.Success)
        {
            Debug.LogError($"{nameof(FetchComplete)} was unsuccessful\n{nameof(info.LastFetchStatus)}: {info.LastFetchStatus}");
            return;
        }

        // Fetch successful. Parameter values must be activated to use.
        remoteConfig.ActivateAsync().ContinueWithOnMainThread(
            task => {
                Debug.Log($"Remote data loaded and ready for use. Last fetch time {info.FetchTime}.");

                //Imports all values in firebase database from the configuration and logs them
                foreach (var item in FirebaseRemoteConfig.DefaultInstance.AllValues)
                    Debug.LogFormat("Key:{0} : Value:{1}", item.Key, item.Value.StringValue);
            });
    }

    //Get request for the downloaded data
    IEnumerator GetRequest(string uri)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            string[] pages = uri.Split('/');
            int page = pages.Length - 1;

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError(pages[page] + ": Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.LogError(pages[page] + ": HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:
                    Debug.Log(pages[page] + ":\nReceived: " + webRequest.downloadHandler.text);

                    //Parse the JSON data
                    JObject jsonData = JObject.Parse(webRequest.downloadHandler.text);
                    textToUpdate.text += "\nText from remote config file: " + jsonData["text"]; //I know there's a 'text' field with information in the json file
                    break;
            }
        }
    }

}
