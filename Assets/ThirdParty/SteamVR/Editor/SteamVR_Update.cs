//======= Copyright (c) Valve Corporation, All rights reserved. ===============
//
// Purpose: Notify developers when a new version of the plugin is available.
//
//=============================================================================

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine.Networking;

[InitializeOnLoad]
public class SteamVR_Update : EditorWindow
{
    // Update this when you ship a new plugin version
    const string currentVersion = "1.2.3";

    const string versionUrl = "http://media.steampowered.com/apps/steamvr/unitypluginversion.txt";
    const string notesUrl = "http://media.steampowered.com/apps/steamvr/unityplugin-v{0}.txt";
    const string pluginUrl = "http://u3d.as/content/valve-corporation/steam-vr-plugin";
    const string doNotShowKey = "SteamVR.DoNotShow.v{0}";

    static bool gotVersion = false;
    static UnityWebRequest reqVersion, reqNotes;
    static string version = string.Empty, notes = string.Empty;
    static SteamVR_Update window;

    static SteamVR_Update()
    {
        EditorApplication.update += Update;
    }

    static void Update()
    {
        if (!gotVersion)
        {
            if (reqVersion == null)
            {
                reqVersion = UnityWebRequest.Get(versionUrl);
                reqVersion.SendWebRequest();
            }

            if (!reqVersion.isDone)
                return;

            if (UrlSuccess(reqVersion))
                version = reqVersion.downloadHandler.text?.Trim();

            reqVersion.Dispose();
            reqVersion = null;
            gotVersion = true;

            if (ShouldDisplay())
            {
                var url = string.Format(notesUrl, version);
                reqNotes = UnityWebRequest.Get(url);
                reqNotes.SendWebRequest();

                window = GetWindow<SteamVR_Update>(true);
                window.titleContent = new GUIContent("SteamVR");
                window.minSize = new Vector2(320, 440);
            }
        }

        if (reqNotes != null)
        {
            if (!reqNotes.isDone)
                return;

            if (UrlSuccess(reqNotes))
                notes = reqNotes.downloadHandler.text ?? string.Empty;

            reqNotes.Dispose();
            reqNotes = null;

            if (!string.IsNullOrEmpty(notes))
                window.Repaint();
        }

        // Done with our polling
        EditorApplication.update -= Update;
    }

    static bool UrlSuccess(UnityWebRequest req)
    {
#if UNITY_2020_2_OR_NEWER
        if (req.result != UnityWebRequest.Result.Success)
            return false;
#else
        if (req.isNetworkError || req.isHttpError)
            return false;
#endif
        var text = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
        if (!string.IsNullOrEmpty(text) && Regex.IsMatch(text, "404 not found", RegexOptions.IgnoreCase))
            return false;
        return true;
    }

    static bool ShouldDisplay()
    {
        if (string.IsNullOrEmpty(version))
            return false;
        if (version == currentVersion)
            return false;
        if (EditorPrefs.HasKey(string.Format(doNotShowKey, version)))
            return false;

        // Compare semantic-ish versions (e.g., 1.0.4 vs 1.0.3)
        var vA = version.Split('.');
        var vB = currentVersion.Split('.');

        for (int i = 0; i < vA.Length && i < vB.Length; i++)
        {
            if (int.TryParse(vA[i], out var a) && int.TryParse(vB[i], out var b))
            {
                if (a > b) return true;
                if (a < b) return false;
            }
        }

        // If equal up to shared length, prefer the one with more segments (1.0.4.1 > 1.0.4)
        return vA.Length > vB.Length;
    }

    Vector2 scrollPosition;
    bool toggleState;

    string GetResourcePath()
    {
        var ms = MonoScript.FromScriptableObject(this);
        var path = AssetDatabase.GetAssetPath(ms);
        path = Path.GetDirectoryName(path);
        return path.Substring(0, path.Length - "Editor".Length) + "Textures/";
    }

    public void OnGUI()
    {
        EditorGUILayout.HelpBox("A new version of the SteamVR plugin is available!", MessageType.Warning);

        var resourcePath = GetResourcePath();
        var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(resourcePath + "logo.png");
        var rect = GUILayoutUtility.GetRect(position.width, 150, GUI.skin.box);
        if (logo)
            GUI.DrawTexture(rect, logo, ScaleMode.ScaleToFit);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("Current version: " + currentVersion);
        GUILayout.Label("New version: " + version);

        if (!string.IsNullOrEmpty(notes))
        {
            GUILayout.Label("Release notes:");
            EditorGUILayout.HelpBox(notes, MessageType.Info);
        }

        GUILayout.EndScrollView();

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Get Latest Version"))
        {
            Application.OpenURL(pluginUrl);
        }

        EditorGUI.BeginChangeCheck();
        var doNotShow = GUILayout.Toggle(toggleState, "Do not prompt for this version again.");
        if (EditorGUI.EndChangeCheck())
        {
            toggleState = doNotShow;
            var key = string.Format(doNotShowKey, version);
            if (doNotShow)
                EditorPrefs.SetBool(key, true);
            else
                EditorPrefs.DeleteKey(key);
        }
    }
}
