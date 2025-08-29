using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;

namespace VRTK
{
    public sealed class VRTK_SupportInfoWindow : EditorWindow
    {
        private const string Separator = "  ";

        private readonly StringBuilder stringBuilder = new StringBuilder();
        private int section;
        private Vector2 scrollPosition;

        [MenuItem("Window/VRTK/Support Info")]
        public static void ShowWindow()
        {
            GetWindow<VRTK_SupportInfoWindow>(true, "VRTK Support Info").RefreshData();
        }

        private void OnGUI()
        {
            GUIContent buttonContent = new GUIContent("Copy to clipboard");
            minSize = Vector2.Max(minSize, GUI.skin.button.CalcSize(buttonContent) + new Vector2(10, 0));

            using (EditorGUILayout.ScrollViewScope scrollViewScope = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scrollViewScope.scrollPosition;

                using (new EditorGUILayout.VerticalScope(EditorStyles.textArea))
                {
                    EditorGUILayout.LabelField(
                        stringBuilder.ToString(),
                        new GUIStyle(EditorStyles.label) { wordWrap = true },
                        GUILayout.ExpandHeight(true)
                    );
                }
            }

            if (GUILayout.Button(buttonContent))
            {
                EditorGUIUtility.systemCopyBuffer = stringBuilder.ToString();
            }
        }

        private void RefreshData()
        {
            stringBuilder.Length = 0;

            Assembly editorAssembly = typeof(VRTK_SDKManagerEditor).Assembly;
            Assembly assembly = typeof(VRTK_SDKManager).Assembly;

            Append(
                "Versions",
                () =>
                {
                    Append("Unity", UnityEditorInternal.InternalEditorUtility.GetFullUnityVersion());
                    Append("VRTK", VRTK_Defines.CurrentVersion + " (may not be correct if source is GitHub)");

                    Type steamVRUpdateType = editorAssembly.GetType("SteamVR_Update");
                    if (steamVRUpdateType != null)
                    {
                        FieldInfo currentVersionField = steamVRUpdateType.GetField("currentVersion", BindingFlags.NonPublic | BindingFlags.Static);
                        if (currentVersionField != null)
                        {
                            string currentVersion = (string)currentVersionField.GetValue(null);
                            Append("SteamVR", currentVersion);
                        }
                    }

                    Type ovrPluginType = assembly.GetType("OVRPlugin");
                    if (ovrPluginType != null)
                    {
                        Append(
                            "OVRPlugin (Oculus Utilities)",
                            () =>
                            {
                                FieldInfo wrapperVersionField = ovrPluginType.GetField("wrapperVersion", BindingFlags.Public | BindingFlags.Static);
                                if (wrapperVersionField != null)
                                {
                                    Version wrapperVersion = (Version)wrapperVersionField.GetValue(null);
                                    Append("wrapperVersion", wrapperVersion);
                                }

                                PropertyInfo versionField = ovrPluginType.GetProperty("version", BindingFlags.Public | BindingFlags.Static);
                                if (versionField != null)
                                {
                                    Version version = (Version)versionField.GetGetMethod().Invoke(null, null);
                                    Append("version", version);
                                }

                                PropertyInfo nativeSDKVersionField = ovrPluginType.GetProperty("nativeSDKVersion", BindingFlags.Public | BindingFlags.Static);
                                if (nativeSDKVersionField != null)
                                {
                                    Version nativeSDKVersion = (Version)nativeSDKVersionField.GetGetMethod().Invoke(null, null);
                                    Append("nativeSDKVersion", nativeSDKVersion);
                                }
                            }
                        );
                    }
                }
            );

            Append(
                "VR Settings",
                () =>
                {
                    foreach (BuildTargetGroup targetGroup in VRTK_SharedMethods.GetValidBuildTargetGroups())
                    {
                        // XR Plug-in Management replacement for legacy VREditor checks
                        var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(targetGroup);
                        var loaders = settings?.AssignedSettings?.activeLoaders;

                        bool isVREnabled = loaders != null && loaders.Count > 0;
                        if (!isVREnabled)
                        {
                            continue;
                        }

                        // Gather enabled "devices" from loader names
                        string[] vrEnabledDevices = loaders.Select(l => l.name).ToArray();

                        // Optional: normalize to legacy-style identifiers some VRTK code expects
                        for (int i = 0; i < vrEnabledDevices.Length; i++)
                        {
                            string name = vrEnabledDevices[i];

                            if (name.IndexOf("Oculus", StringComparison.OrdinalIgnoreCase) >= 0)
                                vrEnabledDevices[i] = "Oculus";
                            else if (name.IndexOf("OpenXR", StringComparison.OrdinalIgnoreCase) >= 0)
                                vrEnabledDevices[i] = "OpenXR"; // change to "OpenVR" if your downstream logic expects that
                            else if (name.IndexOf("WindowsMR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("WMR", StringComparison.OrdinalIgnoreCase) >= 0)
                                vrEnabledDevices[i] = "WindowsMR";
                        }

                        Append(targetGroup, string.Join(", ", vrEnabledDevices));
                    }
                }
            );

            Append(
                "Scripting Define Symbols",
                () =>
                {
                    foreach (BuildTargetGroup targetGroup in VRTK_SharedMethods.GetValidBuildTargetGroups())
                    {
                        string symbols = string.Join(
                            ";",
                            PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup)
                                          .Split(';')
                                          .Where(symbol => !symbol.StartsWith(VRTK_Defines.VersionScriptingDefineSymbolPrefix, StringComparison.Ordinal))
                                          .ToArray());
                        if (!string.IsNullOrEmpty(symbols))
                        {
                            Append(targetGroup, symbols);
                        }
                    }
                }
            );

            stringBuilder.Length--;
        }

        private void Append(string value, Action sectionContentAction = null)
        {
            for (int index = 0; index < section; index++)
            {
                stringBuilder.Append(Separator);
            }

            stringBuilder.AppendLine(value);

            if (sectionContentAction != null)
            {
                section++;
                sectionContentAction();
                section--;
            }
        }

        private void Append(object tag, object value)
        {
            Append(string.Format("{0}: {1}", tag, value));
        }
    }
}
