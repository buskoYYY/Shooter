// Designed by KINEMATION, 2024.

using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KINEMATION.Shared.KAnimationCore.Editor.Misc
{
    public class KEditorUtility
    {
        public const string EditorToolsPath = "Window/KINEMATION/Tools";

        public static GUIStyle boldLabel => EditorStyles.boldLabel;

        public static string GetProjectActiveFolder() => GetProjectWindowFolder();

        public static string GetProjectWindowFolder()
        {
            Type projectWindowUtilType = typeof(ProjectWindowUtil);
            
            MethodInfo getActiveFolderPathMethod = projectWindowUtilType.GetMethod("GetActiveFolderPath", 
                BindingFlags.Static | BindingFlags.NonPublic);
            
            if (getActiveFolderPathMethod != null)
            {
                object result = getActiveFolderPathMethod.Invoke(null, null);
                if (result != null)
                {
                    return result.ToString();
                }
            }

            return "No folder is currently opened.";
        }
        
        public static void SaveAsset(Object asset, string directory, string nameWithExtension)
        {
            string filePath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, nameWithExtension));
            
            AssetDatabase.CreateAsset(asset, filePath);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }
    }
}